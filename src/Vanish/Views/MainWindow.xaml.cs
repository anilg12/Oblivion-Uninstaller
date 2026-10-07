using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vanish.Controls;
using Vanish.Helpers;
using Vanish.Services;
using Vanish.ViewModels;
using Vanish.ViewModels.Pages;
using Vanish.Views.Pages;
using Wpf.Ui.Controls;

namespace Vanish.Views;

public partial class MainWindow : FluentWindow
{
    private const double RightPanelMinWindowWidth = 1300;

    private readonly MainWindowViewModel _vm;
    private readonly NavigationService _navigation;
    private readonly SettingsService _settings;
    private readonly ThemeService _theme;
    private FrameworkElement? _currentPage;

    public MainWindow(MainWindowViewModel vm, NavigationService navigation, DialogService dialogs, ToastService toast,
        SettingsService settings, ThemeService theme)
    {
        _vm = vm;
        _navigation = navigation;
        _settings = settings;
        _theme = theme;
        DataContext = vm;
        InitializeComponent();

        dialogs.Attach(this, DialogLayer, DialogBackdrop, DialogHost);
        toast.Attach(ToastHost, ToastText, ToastIcon, ToastBadge);

        navigation.Navigated += NavigateTo;
        theme.Changed += SyncThemeIcon;
        settings.Current.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.ShowLivePanel)) UpdateRightPanel();
        };

        SizeChanged += (_, _) => UpdateRightPanel();
        StateChanged += (_, _) => UpdateRightPanel();
        Loaded += (_, _) =>
        {
            SyncThemeIcon();
            UpdateRightPanel();
            if (_currentPage is null) NavigateTo("Dashboard");
            CompositionTarget.Rendering += OnFirstFrame;
        };
    }

    // tag of the page currently shown
    public string CurrentTag { get; private set; } = "";

    // milliseconds from process start to the first composed frame (self-test)
    public static long FirstRenderMs { get; private set; }

    // ContentRendered doesn't fire for this window, use the first composition frame instead
    private static void OnFirstFrame(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnFirstFrame;
        if (FirstRenderMs != 0) return;
        try
        {
            FirstRenderMs = (long)(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        }
        catch
        {
            FirstRenderMs = App.Clock.ElapsedMilliseconds;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.EnableElevatedFileDrop(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        }
        catch { /* drag & drop is a convenience only */ }
    }

    // files dropped from Explorer go to the shredder queue (only while that page is open)
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != Native.WM_DROPFILES) return IntPtr.Zero;
        var files = Native.DroppedFiles(wParam);
        if (CurrentTag == "Shredder" && files.Count > 0) Ioc.Resolve<ShredderViewModel>().Add(files);
        handled = true;
        return IntPtr.Zero;
    }

    // live panel only on wide windows, sampling stops when it's hidden or minimized
    private void UpdateRightPanel()
    {
        bool show = _settings.Current.ShowLivePanel && ActualWidth >= RightPanelMinWindowWidth;
        RightColumn.Width = new GridLength(show ? 284 : 0);
        RightPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show && WindowState != WindowState.Minimized) _vm.Live.Start();
        else _vm.Live.Stop();
    }

    private void SyncThemeIcon() => ThemeIcon.Symbol = _theme.IsDark ? SymbolRegular.WeatherSunny24 : SymbolRegular.WeatherMoon24;

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag }) NavigateTo(tag);
    }

    private static FrameworkElement? ResolvePage(string tag) => tag switch
    {
        "Dashboard" => Ioc.Resolve<DashboardPage>(),
        "Uninstaller" => Ioc.Resolve<UninstallerPage>(),
        "WindowsApps" => Ioc.Resolve<WindowsAppsPage>(),
        "BrowserExt" => Ioc.Resolve<BrowserExtensionsPage>(),
        "Startup" => Ioc.Resolve<StartupPage>(),
        "Junk" => Ioc.Resolve<JunkCleanerPage>(),
        "SystemMonitor" => Ioc.Resolve<SystemMonitorPage>(),
        "Tools" => Ioc.Resolve<ToolsPage>(),
        "LargeFiles" => Ioc.Resolve<LargeFilesPage>(),
        "Shredder" => Ioc.Resolve<ShredderPage>(),
        "History" => Ioc.Resolve<HistoryPage>(),
        "Evidence" => Ioc.Resolve<EvidencePage>(),
        "Backups" => Ioc.Resolve<BackupsPage>(),
        "Monitored" => Ioc.Resolve<MonitoredPage>(),
        "Hunter" => Ioc.Resolve<HunterPage>(),
        "Logs" => Ioc.Resolve<LogsPage>(),
        "Settings" => Ioc.Resolve<SettingsPage>(),
        _ => null
    };

    // which sidebar item to highlight (tool pages -> Tools)
    private static string RailGroup(string tag) => tag switch
    {
        "Monitored" or "Hunter" => "Uninstaller",
        "Junk" or "Startup" or "LargeFiles" or "Shredder" or "History" or "Evidence" or "Backups" or "BrowserExt" => "Tools",
        _ => tag
    };

    private static string NavGroup(string tag) => tag switch
    {
        "Junk" or "Startup" or "LargeFiles" or "Shredder" or "History" or "Evidence" or "Backups" => "Tools",
        _ => tag
    };

    public void NavigateTo(string tag)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => NavigateTo(tag));
            return;
        }
        if (tag == CurrentTag && _currentPage is not null) return;
        var page = ResolvePage(tag);
        if (page is null) return;

        (_currentPage?.DataContext as IPageAware)?.OnHidden();
        _currentPage = page;
        CurrentTag = tag;
        _navigation.SetCurrent(tag);
        PageHost.Content = page;
        (page.DataContext as IPageAware)?.OnShown();

        SetActive(RailBottomPanel, NavGroup(tag));
        SetActive(NavPanel, NavGroup(tag));
        AnimatePage();
    }

    private static void SetActive(Panel panel, string tag)
    {
        foreach (var child in panel.Children)
            if (child is FrameworkElement { Tag: string t } fe)
                Nav.SetIsActive(fe, t == tag);
    }

    private void AnimatePage()
    {
        if (!Reveal.AnimationsEnabled) return;
        var transform = new TranslateTransform(0, 6);
        PageHost.RenderTransform = transform;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
        var slide = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        slide.Completed += (_, _) =>
        {
            PageHost.BeginAnimation(OpacityProperty, null);
            PageHost.Opacity = 1;
            PageHost.RenderTransform = Transform.Identity;
        };
        PageHost.BeginAnimation(OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void OtherCommands_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } element)
        {
            menu.DataContext = DataContext;
            menu.PlacementTarget = element;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
