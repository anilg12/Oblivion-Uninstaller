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
    // for the startup time measurement in the self-test
    public static readonly Stopwatch Clock = Stopwatch.StartNew();

    private ServiceProvider? _services;

    private static string? SnapshotDir => Environment.GetEnvironmentVariable("OBLIVION_SNAPSHOT_DIR");

    private static bool SelfTest => !string.IsNullOrWhiteSpace(SnapshotDir);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            if (a.ExceptionObject is Exception ex) CrashLog.Write("AppDomain", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            CrashLog.Write("Task", a.Exception);
            a.SetObserved();
        };
        if (SelfTest) SnapshotRunner.Prepare(SnapshotDir!);

        try
        {
            CrashLog.Mark("startup: begin");
            // 60 fps is enough for the animations, halves the work on 120/144 Hz screens.
            // no gpu acceleration (or turned off in settings) -> no entrance animations at all
            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 60 });
            // software rendering instead of d3d. the ui is mostly static so it costs almost nothing, and it
            // fixes the desktop / other windows flickering on some displays (gpu overlay / VRR switching)
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            _services = ConfigureServices();
            Ioc.Configure(_services);

            var settings = _services.GetRequiredService<SettingsService>();
            SettingsViewModel.ApplyAnimationSetting(settings.Current);
            Loc.I.Language = settings.Current.Language;
            _services.GetRequiredService<ThemeService>().Apply();
            CrashLog.Mark("startup: theme applied");
            // load the activity log in the background early
            _ = _services.GetRequiredService<OperationLogService>().WarmUpAsync();

            var window = _services.GetRequiredService<MainWindow>();
            CrashLog.Mark("startup: window created");
            MainWindow = window;
            window.Show();
            CrashLog.Mark("startup: window shown");

            if (SelfTest) _ = SnapshotRunner.RunAsync(window, SnapshotDir!);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Startup", ex);
            if (SelfTest)
            {
                SnapshotRunner.WriteFailure(SnapshotDir!, ex);
                Environment.Exit(4);
            }
            MessageBox.Show($"Oblivion could not start:\n\n{ex.Message}\n\n{CrashLog.FilePath}", "Oblivion",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var s = new ServiceCollection();

        // engine
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

        // view models
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

        // views (pages get created on first visit, then kept)
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
        CrashLog.Write("UI", e.Exception);
        if (SelfTest)
        {
            SnapshotRunner.WriteFailure(SnapshotDir!, e.Exception);
            Environment.Exit(5);
        }
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

// tiny service locator for things not created through DI
public static class Ioc
{
    private static IServiceProvider? _provider;
    public static void Configure(IServiceProvider provider) => _provider = provider;
    public static T Resolve<T>() where T : notnull =>
        (_provider ?? throw new InvalidOperationException("IoC not configured")).GetRequiredService<T>();
}
