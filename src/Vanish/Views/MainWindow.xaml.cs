using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vanish.Services;
using Vanish.ViewModels;
using Vanish.Views.Pages;
using Wpf.Ui.Controls;

namespace Vanish.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel, NavigationService navigation)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeComponent();

        // Tools hub and other pages can request navigation by tag.
        navigation.Navigated += NavigateTo;

        Loaded += (_, _) =>
        {
            if (NavList.SelectedIndex < 0)
                NavList.SelectedIndex = 0;
        };
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is ListBoxItem { Tag: string tag })
            NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        object? page = tag switch
        {
            "Dashboard" => Ioc.Resolve<DashboardPage>(),
            "Uninstaller" => Ioc.Resolve<UninstallerPage>(),
            "Monitored" => Ioc.Resolve<MonitoredPage>(),
            "WindowsApps" => Ioc.Resolve<WindowsAppsPage>(),
            "BrowserExt" => Ioc.Resolve<BrowserExtensionsPage>(),
            "Logs" => Ioc.Resolve<LogsPage>(),
            "Hunter" => Ioc.Resolve<HunterPage>(),
            "Tools" => Ioc.Resolve<ToolsPage>(),
            "Settings" => Ioc.Resolve<SettingsPage>(),
            "Startup" => Ioc.Resolve<StartupPage>(),
            "Junk" => Ioc.Resolve<JunkCleanerPage>(),
            _ => null
        };

        if (page is null) return;
        PageHost.Content = page;
        AnimatePage();
    }

    /// <summary>Subtle fade + slide-up entrance for each page swap.</summary>
    private void AnimatePage()
    {
        var transform = new TranslateTransform(0, 16);
        PageHost.RenderTransform = transform;
        PageHost.Opacity = 0;

        PageHost.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));

        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void OtherCommands_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } element)
        {
            menu.DataContext = DataContext;
            menu.PlacementTarget = element;
            menu.IsOpen = true;
        }
    }
}
