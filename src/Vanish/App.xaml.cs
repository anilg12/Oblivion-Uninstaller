using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vanish.Services;
using Vanish.ViewModels;
using Vanish.ViewModels.Pages;
using Vanish.Views;
using Vanish.Views.Pages;

namespace Vanish;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Engine services
                services.AddSingleton<IInstalledProgramsService, InstalledProgramsService>();
                services.AddSingleton<IUninstallService, UninstallService>();
                services.AddSingleton<ILeftoverScanService, LeftoverScanService>();
                services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
                services.AddSingleton<IStartupService, StartupService>();
                services.AddSingleton<IJunkCleanerService, JunkCleanerService>();
                services.AddSingleton<IWindowsAppsService, WindowsAppsService>();
                services.AddSingleton<NavigationService>();
                services.AddSingleton<BrowserExtensionsService>();
                services.AddSingleton<HunterService>();
                services.AddSingleton<MonitorService>();
                services.AddSingleton<OperationLogService>();

                // ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<UninstallerViewModel>();
                services.AddSingleton<StartupViewModel>();
                services.AddSingleton<JunkCleanerViewModel>();
                services.AddSingleton<WindowsAppsViewModel>();
                services.AddSingleton<ToolsViewModel>();
                services.AddSingleton<BrowserExtensionsViewModel>();
                services.AddSingleton<HunterViewModel>();
                services.AddSingleton<MonitoredViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<SettingsViewModel>();

                // Views / pages
                services.AddSingleton<MainWindow>();
                services.AddSingleton<DashboardPage>();
                services.AddSingleton<UninstallerPage>();
                services.AddSingleton<StartupPage>();
                services.AddSingleton<JunkCleanerPage>();
                services.AddSingleton<WindowsAppsPage>();
                services.AddSingleton<ToolsPage>();
                services.AddSingleton<MonitoredPage>();
                services.AddSingleton<BrowserExtensionsPage>();
                services.AddSingleton<LogsPage>();
                services.AddSingleton<HunterPage>();
                services.AddSingleton<SettingsPage>();
            })
            .Build();

        // Service locator used by the NavigationView to materialise pages.
        Ioc.Configure(_host.Services);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Vanish — unexpected error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

/// <summary>Minimal service locator so XAML-instantiated pages can resolve their VMs.</summary>
public static class Ioc
{
    private static IServiceProvider? _provider;
    public static void Configure(IServiceProvider provider) => _provider = provider;
    public static T Resolve<T>() where T : notnull =>
        (_provider ?? throw new InvalidOperationException("IoC not configured")).GetRequiredService<T>();
}
