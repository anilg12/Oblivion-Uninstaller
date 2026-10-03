using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Vanish.Controls;
using Vanish.Helpers;
using Vanish.SelfTest;
using Vanish.Services;
using Vanish.ViewModels;
using Vanish.ViewModels.Pages;
using Vanish.Views;
using Vanish.Views.Pages;

namespace Vanish;

public partial class App : Application
{
    /// <summary>Process start, for the startup-time measurement in the self-test.</summary>
    public static readonly Stopwatch Clock = Stopwatch.StartNew();

    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Animations never need more than 60 fps; on 120/144 Hz screens this halves the
        // render work. Without GPU acceleration (or when the user asks), entrance
        // animations are skipped entirely.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 60 });

        _services = ConfigureServices();
        Ioc.Configure(_services);

        var settings = _services.GetRequiredService<SettingsService>();
        SettingsViewModel.ApplyAnimationSetting(settings.Current);
        Loc.I.Language = settings.Current.Language;
        _services.GetRequiredService<ThemeService>().Apply();
        // Read the activity log in the background, before any page needs it.
        _ = _services.GetRequiredService<OperationLogService>().WarmUpAsync();

        var snapshotDir = Environment.GetEnvironmentVariable("OBLIVION_SNAPSHOT_DIR");
        bool selfTest = !string.IsNullOrWhiteSpace(snapshotDir);
        if (selfTest) SnapshotRunner.Prepare();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        if (selfTest) _ = SnapshotRunner.RunAsync(window, snapshotDir!);
    }

    private static ServiceProvider ConfigureServices()
    {
        var s = new ServiceCollection();

        // Engine
        s.AddSingleton<SettingsService>();
        s.AddSingleton<ThemeService>();
        s.AddSingleton<DialogService>();
        s.AddSingleton<ToastService>();
        s.AddSingleton<NavigationService>();
        s.AddSingleton<OperationLogService>();
        s.AddSingleton<SystemMonitorService>();
        s.AddSingleton<IInstalledProgramsService, InstalledProgramsService>();
        s.AddSingleton<IUninstallService, UninstallService>();
        s.AddSingleton<ILeftoverScanService, LeftoverScanService>();
        s.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        s.AddSingleton<IStartupService, StartupService>();
        s.AddSingleton<IJunkCleanerService, JunkCleanerService>();
        s.AddSingleton<IWindowsAppsService, WindowsAppsService>();
        s.AddSingleton<BrowserExtensionsService>();
        s.AddSingleton<HunterService>();
        s.AddSingleton<MonitorService>();
        s.AddSingleton<LargeFilesService>();
        s.AddSingleton<ShredderService>();
        s.AddSingleton<HistoryCleanerService>();
        s.AddSingleton<EvidenceService>();

        // View models
        s.AddSingleton<MainWindowViewModel>();
        s.AddSingleton<LiveStatsViewModel>();
        s.AddSingleton<DashboardViewModel>();
        s.AddSingleton<UninstallerViewModel>();
        s.AddSingleton<WindowsAppsViewModel>();
        s.AddSingleton<BrowserExtensionsViewModel>();
        s.AddSingleton<StartupViewModel>();
        s.AddSingleton<JunkCleanerViewModel>();
        s.AddSingleton<SystemMonitorViewModel>();
        s.AddSingleton<ToolsViewModel>();
        s.AddSingleton<LargeFilesViewModel>();
        s.AddSingleton<ShredderViewModel>();
        s.AddSingleton<HistoryViewModel>();
        s.AddSingleton<EvidenceViewModel>();
        s.AddSingleton<BackupsViewModel>();
        s.AddSingleton<MonitoredViewModel>();
        s.AddSingleton<HunterViewModel>();
        s.AddSingleton<LogsViewModel>();
        s.AddSingleton<SettingsViewModel>();

        // Views (pages are created on first visit and then kept)
        s.AddSingleton<MainWindow>();
        s.AddSingleton<DashboardPage>();
        s.AddSingleton<UninstallerPage>();
        s.AddSingleton<WindowsAppsPage>();
        s.AddSingleton<BrowserExtensionsPage>();
        s.AddSingleton<StartupPage>();
        s.AddSingleton<JunkCleanerPage>();
        s.AddSingleton<SystemMonitorPage>();
        s.AddSingleton<ToolsPage>();
        s.AddSingleton<LargeFilesPage>();
        s.AddSingleton<ShredderPage>();
        s.AddSingleton<HistoryPage>();
        s.AddSingleton<EvidencePage>();
        s.AddSingleton<BackupsPage>();
        s.AddSingleton<MonitoredPage>();
        s.AddSingleton<HunterPage>();
        s.AddSingleton<LogsPage>();
        s.AddSingleton<SettingsPage>();

        return s.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            Ioc.Resolve<ToastService>().Show(e.Exception.Message, ToastKind.Error);
        }
        catch
        {
            MessageBox.Show(e.Exception.Message, "Oblivion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

/// <summary>Minimal service locator for code that is not created through DI.</summary>
public static class Ioc
{
    private static IServiceProvider? _provider;
    public static void Configure(IServiceProvider provider) => _provider = provider;
    public static T Resolve<T>() where T : notnull =>
        (_provider ?? throw new InvalidOperationException("IoC not configured")).GetRequiredService<T>();
}
