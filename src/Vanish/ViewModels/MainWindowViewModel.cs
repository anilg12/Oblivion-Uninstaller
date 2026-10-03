using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;
using Vanish.ViewModels.Pages;

namespace Vanish.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly OperationLogService _log;

    public MainWindowViewModel(UninstallerViewModel uninstaller, SettingsViewModel settings, OperationLogService log)
    {
        Uninstaller = uninstaller;
        Settings = settings;
        _log = log;

        WindowsVersion = GetWindowsVersion();
        FreeSpace = GetFreeSpace();
        RefreshActivity();
    }

    /// <summary>Shared singleton so the sidebar action buttons act on the All-apps selection.</summary>
    public UninstallerViewModel Uninstaller { get; }

    public SettingsViewModel Settings { get; }

    public Loc Loc => Loc.I;

    // ---- right info panel ---------------------------------------------------

    [ObservableProperty] private string _windowsVersion = "Windows 11";
    [ObservableProperty] private string _freeSpace = "—";

    public ObservableCollectionEx<LogEntry> RecentActivity { get; } = new();

    /// <summary>Reloads the recent-activity list shown in the right panel.</summary>
    public void RefreshActivity() => RecentActivity.Reset(_log.GetAll().Take(6));

    [RelayCommand]
    private void ToggleTheme() => Settings.ToggleThemeCommand.Execute(null);

    [RelayCommand]
    private void ToggleLanguage() => Settings.ToggleLanguageCommand.Execute(null);

    private static string GetFreeSpace()
    {
        try
        {
            var sys = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            var drive = new DriveInfo(sys);
            return $"{ByteSize.Humanize(drive.AvailableFreeSpace)} / {ByteSize.Humanize(drive.TotalSize)}";
        }
        catch
        {
            return "—";
        }
    }

    private static string GetWindowsVersion()
    {
        try
        {
            var v = Environment.OSVersion.Version;
            var name = v.Build >= 22000 ? "Windows 11" : "Windows 10";
            return $"{name} · {v.Build}";
        }
        catch
        {
            return "Windows 11";
        }
    }
}
