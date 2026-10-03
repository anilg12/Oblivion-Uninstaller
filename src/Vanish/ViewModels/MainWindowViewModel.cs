using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;
using Vanish.ViewModels.Pages;
using Vanish.Views;

namespace Vanish.ViewModels;

/// <summary>Implemented by view models whose computed texts must be re-read after a language switch.</summary>
public interface ILocalizable
{
    void RefreshTexts();
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly OperationLogService _log;
    private readonly SettingsService _settings;
    private readonly ThemeService _theme;
    private readonly DialogService _dialogs;
    private readonly NavigationService _navigation;

    public MainWindowViewModel(UninstallerViewModel uninstaller, LiveStatsViewModel live, OperationLogService log,
        SettingsService settings, ThemeService theme, DialogService dialogs, NavigationService navigation)
    {
        Uninstaller = uninstaller;
        Live = live;
        _log = log;
        _settings = settings;
        _theme = theme;
        _dialogs = dialogs;
        _navigation = navigation;

        _log.Changed += () => Application.Current?.Dispatcher.InvokeAsync(RefreshActivity);
        _theme.Changed += () => OnPropertyChanged(nameof(IsDark));
        _ = LoadActivityAsync();
    }

    /// <summary>Shared with the All-applications page so the sidebar buttons act on its selection.</summary>
    public UninstallerViewModel Uninstaller { get; }

    public LiveStatsViewModel Live { get; }

    public AppSettings Settings => _settings.Current;

    public string VersionText => "v" + AppInfo.Version;

    public bool IsDark => _theme.IsDark;

    public ObservableCollectionEx<LogEntry> RecentActivity { get; } = new();

    private async Task LoadActivityAsync()
    {
        await _log.WarmUpAsync();
        RefreshActivity();
    }

    private void RefreshActivity() => RecentActivity.Reset(_log.GetRecent(5));

    [RelayCommand]
    private void ToggleTheme() => _settings.Current.Theme = _theme.IsDark ? "light" : "dark";

    [RelayCommand]
    private void ToggleLanguage()
    {
        Loc.I.Toggle();
        _settings.Current.Language = Loc.I.Language;
        RefreshAllTexts();
    }

    /// <summary>Re-reads localized texts that are computed in code (lists, chips, counts).</summary>
    public void RefreshAllTexts()
    {
        RefreshActivity();
        foreach (var vm in new object[]
                 {
                     Ioc.Resolve<DashboardViewModel>(), Uninstaller, Ioc.Resolve<JunkCleanerViewModel>(), Ioc.Resolve<StartupViewModel>(),
                     Ioc.Resolve<HistoryViewModel>(), Ioc.Resolve<MonitoredViewModel>(), Ioc.Resolve<LogsViewModel>(),
                     Ioc.Resolve<SystemMonitorViewModel>(), Ioc.Resolve<WindowsAppsViewModel>(), Ioc.Resolve<BrowserExtensionsViewModel>(),
                     Ioc.Resolve<LargeFilesViewModel>(), Ioc.Resolve<ShredderViewModel>(), Ioc.Resolve<EvidenceViewModel>(),
                     Ioc.Resolve<BackupsViewModel>(), Ioc.Resolve<HunterViewModel>()
                 })
        {
            if (vm is ILocalizable l) l.RefreshTexts();
        }
    }

    [RelayCommand]
    private Task ShowAboutAsync() => _dialogs.ShowAsync(new AboutView());

    [RelayCommand]
    private void Navigate(string tag) => _navigation.Navigate(tag);
}

/// <summary>Version and links shown in the About dialog and the sidebar.</summary>
public static class AppInfo
{
    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "3.0.0";

    public const string GitHub = "https://github.com/anilg12";
    public const string Repository = "https://github.com/anilg12/Oblivion-Uninstaller";
    public const string Releases = "https://github.com/anilg12/Oblivion-Uninstaller/releases";
}
