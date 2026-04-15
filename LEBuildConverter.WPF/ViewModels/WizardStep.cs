using CommunityToolkit.Mvvm.ComponentModel;

namespace LEBuildConverter.WPF.ViewModels;

/// <summary>
/// One step in the wizard — a labelled instruction + JSON payload to copy.
/// </summary>
public partial class WizardStep : ObservableObject
{
    [ObservableProperty]
    private string title = "";

    [ObservableProperty]
    private string instructions = "";

    /// <summary>
    /// The JSON blob the user pastes into maxroll for this step.
    /// Empty string means this step is informational only (no paste).
    /// </summary>
    [ObservableProperty]
    private string jsonToCopy = "";

    /// <summary>
    /// Relative path inside the Assets/screenshots folder, e.g. "step01.png".
    /// The view resolves this to pack:// URIs.
    /// </summary>
    [ObservableProperty]
    private string screenshotName = "";

    /// <summary>
    /// True if this step has no JSON to copy (e.g. "Open maxroll" or
    /// "Change class manually").
    /// </summary>
    public bool HasJson => !string.IsNullOrEmpty(JsonToCopy);

    public WizardStep(string title, string instructions, string jsonToCopy = "", string screenshotName = "")
    {
        Title = title;
        Instructions = instructions;
        JsonToCopy = jsonToCopy;
        ScreenshotName = screenshotName;
    }
}
