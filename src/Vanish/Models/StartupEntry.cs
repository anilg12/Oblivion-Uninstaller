using CommunityToolkit.Mvvm.ComponentModel;

namespace Vanish.Models;

/// <summary>An auto-start program (Run key entry or Startup-folder shortcut).</summary>
public sealed partial class StartupEntry : ObservableObject
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required StartupLocation Location { get; init; }

    /// <summary>The registry path or folder the entry was found in (for delete/toggle).</summary>
    public required string Source { get; init; }

    [ObservableProperty]
    private bool _isEnabled = true;

    public string LocationLabel => Location switch
    {
        StartupLocation.HklmRun => "All users · Run",
        StartupLocation.HkcuRun => "Current user · Run",
        StartupLocation.HklmRunOnce => "All users · RunOnce",
        StartupLocation.HkcuRunOnce => "Current user · RunOnce",
        StartupLocation.CommonStartupFolder => "All users · Startup folder",
        StartupLocation.UserStartupFolder => "Current user · Startup folder",
        _ => "Unknown"
    };
}

public enum StartupLocation
{
    HklmRun,
    HkcuRun,
    HklmRunOnce,
    HkcuRunOnce,
    CommonStartupFolder,
    UserStartupFolder
}
