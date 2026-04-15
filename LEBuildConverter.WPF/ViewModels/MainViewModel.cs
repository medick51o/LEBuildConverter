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
                "Click the button below to open a fresh maxroll Last Epoch planner in your browser. " +
                "Leave that tab open — you'll paste data into it on each of the next steps.\n\n" +
                "When the planner page loads, click the \"Export/Import\" button near the top right.",
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
                "Click \"Copy to Clipboard\" below. Then in maxroll, click Export/Import → \"All Equipment\" tab, " +
                "paste into the textarea, and click \"Import All Equipment\".\n\n" +
                "This imports gear, idols, blessings, and the woven echo altar all in one shot.",
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
                "Once the correct mastery is selected, click \"Copy to Clipboard\" below. Then in maxroll, " +
                "click Export/Import → \"Passives\" tab, paste, and click Import.\n\n" +
                "The passive tree should fill in and 0 unspent points should show.",
            jsonToCopy: blobs.Passives.ToJsonString(),
            screenshotName: "step04_paste_passives.png"));

        // Step 5 — Weaver Tree (only if populated)
        if (summary.WeaverPointsSpent > 0)
        {
            Steps.Add(new WizardStep(
                title: $"{stepNum++}. Paste Weaver Tree",
                instructions:
                    "Click \"Copy to Clipboard\" below. Then in maxroll, click Export/Import → \"Weaver Tree\" tab, " +
                    "paste, and click Import.",
                jsonToCopy: blobs.WeaverTree.ToJsonString(),
                screenshotName: "step05_paste_weaver.png"));
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
                    $"Click \"Copy to Clipboard\" below, then in maxroll's Export/Import dialog click the " +
                    $"\"{skill.SkillName}\" button and paste.",
                jsonToCopy: skillBlob.ToJsonString(),
                screenshotName: $"step_skill_{skill.TreeId}.png"));
        }
    }
}
