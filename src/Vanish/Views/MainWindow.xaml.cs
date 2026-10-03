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

        navigation.Navigated += SelectAndNavigate;

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

    /// <summary>Rail / external navigation: also sync the labelled list selection.</summary>
    private void SelectAndNavigate(string tag)
    {
        foreach (var item in NavList.Items)
        {
            if (item is ListBoxItem { Tag: string t } li && t == tag)
            {
                if (!ReferenceEquals(NavList.SelectedItem, li))
                {
                    NavList.SelectedItem = li; // triggers NavList_SelectionChanged -> NavigateTo
                    return;
                }
                break;
            }
        }
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
        _viewModel.RefreshActivity();
        AnimatePage();
    }

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

    private void Rail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
            SelectAndNavigate(tag);
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        => _viewModel.ToggleThemeCommand.Execute(null);

    private void ToggleLanguage_Click(object sender, RoutedEventArgs e)
        => _viewModel.ToggleLanguageCommand.Execute(null);

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
