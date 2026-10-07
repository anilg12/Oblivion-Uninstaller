using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;

namespace Vanish.Models;

// an auto-start program (Run key value or Startup-folder shortcut)
public sealed partial class StartupEntry : ObservableObject
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required StartupLocation Location { get; init; }

    // the registry path (HKCU\...\Run) or folder the entry was found in
    public required string Source { get; init; }

    // executable parsed from the command (for the icon and "open location")
    public string? ExecutablePath { get; init; }

    [ObservableProperty] private bool _isEnabled = true;

    // RunOnce entries can only be deleted, not toggled
    public bool CanToggle => Location is not (StartupLocation.HklmRunOnce or StartupLocation.HkcuRunOnce);

    public bool IsMachineWide => Location is StartupLocation.HklmRun or StartupLocation.HklmRun32
        or StartupLocation.HklmRunOnce or StartupLocation.CommonStartupFolder;

    public string LocationLabel => Location switch
    {
        StartupLocation.HklmRun => Loc.I["Startup_AllUsersRun"],
        StartupLocation.HklmRun32 => Loc.I["Startup_AllUsersRun32"],
        StartupLocation.HkcuRun => Loc.I["Startup_UserRun"],
        StartupLocation.HklmRunOnce => Loc.I["Startup_AllUsersOnce"],
        StartupLocation.HkcuRunOnce => Loc.I["Startup_UserOnce"],
        StartupLocation.CommonStartupFolder => Loc.I["Startup_AllUsersFolder"],
        StartupLocation.UserStartupFolder => Loc.I["Startup_UserFolder"],
        _ => ""
    };
}

public enum StartupLocation
{
    HklmRun,
    HklmRun32,
    HkcuRun,
    HklmRunOnce,
    HkcuRunOnce,
    CommonStartupFolder,
    UserStartupFolder
}
