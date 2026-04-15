using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LEBuildConverter.Core;

namespace LEBuildConverter.WPF.ViewModels;

public enum AppState
{
    Input,
    Loading,
    Summary,
    Wizard,
    Done,
}

public partial class MainViewModel : ObservableObject
{
    // ── App state ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputState))]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsSummaryState))]
    [NotifyPropertyChangedFor(nameof(IsWizardState))]
    [NotifyPropertyChangedFor(nameof(IsDoneState))]
    private AppState state = AppState.Input;

    public bool IsInputState => State == AppState.Input;
    public bool IsLoadingState => State == AppState.Loading;
    public bool IsSummaryState => State == AppState.Summary;
    public bool IsWizardState => State == AppState.Wizard;
    public bool IsDoneState => State == AppState.Done;

    // ── Input ──
    [ObservableProperty]
    private string letUrl = "https://www.lastepochtools.com/planner/BakypDvx";

    [ObservableProperty]
    private string statusMessage = "";

    // ── Summary ──
    [ObservableProperty]
    private string headerLine = "";

    [ObservableProperty]
    private string passivesLine = "";

    public ObservableCollection<string> EquipmentLines { get; } = new();
    public ObservableCollection<string> IdolLines { get; } = new();
    public ObservableCollection<string> BlessingLines { get; } = new();
    public ObservableCollection<string> SkillLines { get; } = new();

    // ── Wizard ──
    public ObservableCollection<WizardStep> Steps { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStep))]
    [NotifyPropertyChangedFor(nameof(StepProgressText))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(NextButtonText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousStepCommand))]
    private int currentStepIndex;

    public WizardStep? CurrentStep =>
        (CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count) ? Steps[CurrentStepIndex] : null;

    public string StepProgressText =>
        Steps.Count == 0 ? "" : $"Step {CurrentStepIndex + 1} of {Steps.Count}";

    public bool CanGoBack => CurrentStepIndex > 0;
    public bool CanGoNext => CurrentStepIndex < Steps.Count - 1;
    public string NextButtonText => CanGoNext ? "Next →" : "Finish";

    // ── Commands ──

    [RelayCommand]
    private async Task FetchAsync()
    {
        try
        {
            StatusMessage = "Fetching lastepochtools build...";
            State = AppState.Loading;

            string slug = LetFetcher.ExtractSlug(LetUrl);
            if (string.IsNullOrWhiteSpace(slug))
            {
                StatusMessage = "Invalid URL — please paste a lastepochtools.com/planner/... link.";
                State = AppState.Input;
                return;
            }

            using var doc = await LetFetcher.FetchBuildAsync(slug);
            var blobs = LetToMaxroll.Convert(doc);
            var names = NameLookup.Instance;
            var summary = BuildSummary.FromLetBuild(doc, slug, names, blobs);

            HeaderLine = summary.HeaderLine;
            PassivesLine = $"Passives: {summary.PassivePointsSpent}   Weaver Tree: {summary.WeaverPointsSpent}";

            EquipmentLines.Clear();
            foreach (var e in summary.Equipment)
                EquipmentLines.Add(e.Display);

            IdolLines.Clear();
            foreach (var i in summary.Idols)
                IdolLines.Add($"{i.ItemName}" + (i.Affixes.Count > 0 ? "  (" + string.Join(", ", i.Affixes) + ")" : ""));

            BlessingLines.Clear();
            foreach (var b in summary.Blessings)
                BlessingLines.Add(b);

            SkillLines.Clear();
            foreach (var s in summary.Skills)
                SkillLines.Add($"Slot {s.SlotNumber + 1}: {s.SkillName} ({s.PointsSpent} pts)");

            // Build wizard steps
            BuildWizardSteps(summary, blobs, names);

            StatusMessage = "";
            State = AppState.Summary;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            State = AppState.Input;
        }
    }

    [RelayCommand]
    private void StartWizard()
    {
        CurrentStepIndex = 0;
        State = AppState.Wizard;

        // NOTE: If CurrentStepIndex was already 0 (default on first run), the
        // source-generated setter skipped the PropertyChanged event, so the
        // wizard view would render blank until the user clicked Next.  Force
        // the computed properties to re-notify so the bindings always pick
        // up the freshly-built Steps list.
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(StepProgressText));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(NextButtonText));
    }

    [RelayCommand]
    private void BackToInput()
    {
        State = AppState.Input;
        StatusMessage = "";
    }

    [RelayCommand]
    private void CopyCurrentJson()
    {
        var step = CurrentStep;
        if (step is null || string.IsNullOrEmpty(step.JsonToCopy)) return;
        try
        {
            Clipboard.SetText(step.JsonToCopy);
            StatusMessage = $"Copied {step.JsonToCopy.Length} chars to clipboard.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clipboard error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenMaxroll()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://maxroll.gg/last-epoch/planner/",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open browser: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousStep()
    {
        if (CurrentStepIndex > 0)
            CurrentStepIndex--;
    }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStepIndex < Steps.Count - 1)
        {
            CurrentStepIndex++;
        }
        else
        {
            State = AppState.Done;
        }
    }

    [RelayCommand]
    private void Restart()
    {
        State = AppState.Input;
        StatusMessage = "";
        EquipmentLines.Clear();
        IdolLines.Clear();
        BlessingLines.Clear();
        SkillLines.Clear();
        Steps.Clear();
        CurrentStepIndex = 0;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private void BuildWizardSteps(BuildSummary summary, MaxrollBlobs blobs, NameLookup names)
    {
        Steps.Clear();
        int stepNum = 1;

        // Step 1 — Open maxroll
        Steps.Add(new WizardStep(
            title: $"{stepNum++}. Open maxroll planner",
            instructions:
                "⚠ BEFORE YOU START — you need a free maxroll.gg account and you need to be LOGGED IN.\n\n" +
                "Without a login, any build you import into maxroll is temporary and disappears when you " +
                "close the browser tab. With a login, you can save the imported build as a profile you " +
                "can revisit, share, or pull into Ash's LE_hud mod in-game. Sign up at maxroll.gg " +
                "(top right \"Sign In / Sign Up\") if you haven't already.\n\n" +
                "Once you're logged in:\n" +
                "1. Click the \"Open maxroll\" button below — it opens a fresh Last Epoch planner in your browser.\n" +
                "2. Leave that tab open — you'll paste data into it on each of the next steps.\n" +
                "3. When the planner page loads, look for the \"Export/Import\" button near the top right.",
            jsonToCopy: "",
            screenshotName: "step01_open_maxroll.png"));

        // Step 2 — Set class / mastery
        Steps.Add(new WizardStep(
            title: $"{stepNum++}. Set class and mastery",
            instructions:
                $"Before pasting passives, you MUST manually switch maxroll's planner to the correct class and mastery.\n\n" +
                $"Change it to:  {summary.ClassName} → {summary.MasteryName}\n\n" +
                "Maxroll does NOT auto-switch based on imported data, so this step is required or the passives and skills will not import correctly.",
            jsonToCopy: "",
            screenshotName: "step02_set_mastery.png"));

        // Step 3 — All Equipment
        Steps.Add(new WizardStep(
            title: $"{stepNum++}. Paste All Equipment",
            instructions:
                "1. Click \"Copy to Clipboard\" below.\n" +
                "2. In maxroll, click the \"Export/Import\" button near the top right.\n" +
                "3. Paste directly into the empty text box in the dialog.\n" +
                "4. Click the \"Import\" button at the bottom.\n\n" +
                "⚠ DO NOT click the \"All Equipment\" / \"Equipment\" / \"Idols\" / \"Blessings\" / " +
                "\"Weaver Tree\" / \"Passives\" buttons at the TOP of the dialog — those are EXPORT buttons " +
                "and will overwrite your paste with blank data from the empty planner.\n\n" +
                "Maxroll auto-detects what you pasted and imports gear, idols, blessings, and the woven " +
                "echo altar all in one shot.",
            jsonToCopy: blobs.AllEquipment.ToJsonString(),
            screenshotName: "step03_paste_equipment.png"));

        // Step 4 — Passives
        Steps.Add(new WizardStep(
            title: $"{stepNum++}. Paste Passives",
            instructions:
                $"⚠ STOP — before pasting passives, verify maxroll is set to:\n" +
                $"    {summary.ClassName} → {summary.MasteryName}\n\n" +
                "If you're still on the default Primalist / base class, change it NOW — maxroll will " +
                "silently reject the passives otherwise (the node IDs are namespaced per class).\n\n" +
                "Steps (same pattern as All Equipment):\n" +
                "1. Click \"Copy to Clipboard\" below.\n" +
                "2. In maxroll, click Export/Import.\n" +
                "3. Paste directly into the text box.\n" +
                "4. Click Import at the bottom.\n\n" +
                "Maxroll auto-detects the passives JSON — no need to click the \"Passives\" tab button. " +
                "The passive tree should fill in and 0 unspent points should show.",
            jsonToCopy: blobs.Passives.ToJsonString(),
            screenshotName: "step03_paste_equipment.png"));

        // Step 5 — Weaver Tree (only if populated)
        if (summary.WeaverPointsSpent > 0)
        {
            Steps.Add(new WizardStep(
                title: $"{stepNum++}. Paste Weaver Tree",
                instructions:
                    "Same flow as the previous paste steps:\n" +
                    "1. Click \"Copy to Clipboard\" below.\n" +
                    "2. In maxroll, click Export/Import.\n" +
                    "3. Paste into the text box.\n" +
                    "4. Click Import.\n\n" +
                    "Maxroll auto-detects the weaver tree JSON and applies it.",
                jsonToCopy: blobs.WeaverTree.ToJsonString(),
                screenshotName: "step03_paste_equipment.png"));
        }

        // Step 6 — Specialize skills (informational)
        var skillNameList = string.Join(", ", summary.Skills.Select(s => s.SkillName));
        Steps.Add(new WizardStep(
            title: $"{stepNum++}. Specialize skills",
            instructions:
                "Before pasting skill tree data, you need to specialize each skill in maxroll's skill bar.\n\n" +
                $"This build uses these 5 skills:\n    {skillNameList}\n\n" +
                "In maxroll's skill bar at the bottom, click each slot and specialize the matching skill. " +
                "Once all 5 are specialized, the Export/Import dialog will show a button for each skill.",
            jsonToCopy: "",
            screenshotName: "step06_specialize_skills.png"));

        // Per-skill steps
        foreach (var skill in summary.Skills)
        {
            if (!blobs.Skills.TryGetValue(skill.TreeId, out var skillBlob)) continue;
            Steps.Add(new WizardStep(
                title: $"{stepNum++}. Paste {skill.SkillName}",
                instructions:
                    $"Same flow as previous paste steps:\n" +
                    $"1. Click \"Copy to Clipboard\" below.\n" +
                    $"2. In maxroll, click Export/Import.\n" +
                    $"3. Paste into the text box.\n" +
                    $"4. Click Import.\n\n" +
                    $"Maxroll auto-detects the skill tree JSON for {skill.SkillName} and applies it " +
                    $"to that specialized skill (from the previous Specialize Skills step).",
                jsonToCopy: skillBlob.ToJsonString(),
                screenshotName: "step03_paste_equipment.png"));
        }

        // Final step — Save the build
        Steps.Add(new WizardStep(
            title: $"{stepNum++}. Save your build",
            instructions:
                "The build is now fully imported into maxroll — but it won't stick around unless " +
                "you save it to your maxroll account.\n\n" +
                "1. In the maxroll planner, click the ⚙ settings cog near the top right (yellow arrow " +
                "in the screenshot).\n" +
                "2. In the Save dialog that opens, give the build a name and click Save.\n" +
                "3. Maxroll generates a shareable URL that stays tied to your account.\n\n" +
                "Once saved, you can:\n" +
                "• Archive the build in your maxroll profile for future use\n" +
                "• Paste the URL into Ash's LE_hud mod gear spawner to load it in-game\n" +
                "• Generate a maxroll loot filter from the build — shoutout to BinaQc and the " +
                "maxroll team for the loot filter system that made this whole tool worth building",
            jsonToCopy: "",
            screenshotName: "step_save_build.png"));
    }
}
