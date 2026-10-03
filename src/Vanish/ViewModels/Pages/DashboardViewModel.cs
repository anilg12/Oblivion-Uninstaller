using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;
using Wpf.Ui.Controls;

namespace Vanish.ViewModels.Pages;

/// <summary>One suggestion on the dashboard ("Drive C: is almost full" + a button).</summary>
public sealed class HealthTip
{
    public required string Text { get; init; }
    public required string ActionText { get; init; }
    public required string Target { get; init; }
    public SymbolRegular Symbol { get; init; } = SymbolRegular.Lightbulb24;
}

public sealed partial class DashboardViewModel : PageViewModel
{
    private readonly UninstallerViewModel _apps;
    private readonly WindowsAppsViewModel _storeApps;
    private readonly StartupViewModel _startup;
    private readonly BrowserExtensionsViewModel _extensions;
    private readonly SystemMonitorService _monitor;
    private IDisposable? _subscription;
    private DateTime _loadedAt = DateTime.MinValue;
    private SystemSnapshot? _lastSnapshot;

    public DashboardViewModel(UninstallerViewModel apps, WindowsAppsViewModel storeApps, StartupViewModel startup,
        BrowserExtensionsViewModel extensions, SystemMonitorService monitor)
    {
        _apps = apps;
        _storeApps = storeApps;
        _startup = startup;
        _extensions = extensions;
        _monitor = monitor;
        Log.Changed += () => System.Windows.Application.Current?.Dispatcher.InvokeAsync(RefreshActivity);
    }

    public string Greeting => DateTime.Now.Hour switch
    {
        < 6 => T("Dash_GreetNight"),
        < 12 => T("Dash_GreetMorning"),
        < 18 => T("Dash_GreetDay"),
        _ => T("Dash_GreetEvening")
    };

    // ---- stats -----------------------------------------------------------------

    [ObservableProperty] private double _programCount = double.NaN;
    [ObservableProperty] private string _programSizeText = "";
    [ObservableProperty] private double _storeAppCount = double.NaN;
    [ObservableProperty] private double _startupCount = double.NaN;
    [ObservableProperty] private string _startupDetail = "";
    [ObservableProperty] private double _extensionCount = double.NaN;
    [ObservableProperty] private string _extensionDetail = "";

    // ---- health ----------------------------------------------------------------

    [ObservableProperty] private double _healthScore = double.NaN;
    [ObservableProperty] private double _healthFraction;
    [ObservableProperty] private string _healthTitle = "";
    [ObservableProperty] private string _healthText = "";

    public ObservableCollectionEx<HealthTip> Tips { get; } = new();

    public ObservableCollectionEx<LogEntry> Activity { get; } = new();

    public override void OnShown()
    {
        base.OnShown();
        OnPropertyChanged(nameof(Greeting));
        RefreshActivity();
        _subscription ??= _monitor.Subscribe(s => { _lastSnapshot = s; UpdateHealth(); }, detailed: false);
        if (DateTime.Now - _loadedAt > TimeSpan.FromMinutes(2)) _ = LoadAsync(force: false);
    }

    public override void OnHidden()
    {
        base.OnHidden();
        _subscription?.Dispose();
        _subscription = null;
    }

    public override void RefreshTexts()
    {
        OnPropertyChanged(nameof(Greeting));
        UpdateStats();
        UpdateHealth();
        RefreshActivity();
    }

    private void RefreshActivity() => Activity.Reset(Log.GetRecent(6));

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(force: true);

    private async Task LoadAsync(bool force)
    {
        _loadedAt = DateTime.Now;
        var programs = _apps.EnsureLoadedAsync(force);
        var startup = _startup.EnsureLoadedAsync(force);
        var extensions = _extensions.EnsureLoadedAsync(force);
        try { await Task.WhenAll(programs, startup, extensions); } catch { /* each page shows its own errors */ }
        UpdateStats();
        UpdateHealth();

        // The Store app list comes from PowerShell; give the UI a moment first.
        await Task.Delay(force ? 0 : 1500);
        try { await _storeApps.EnsureLoadedAsync(force); } catch { /* ignore */ }
        UpdateStats();
    }

    private void UpdateStats()
    {
        if (_apps.IsLoaded)
        {
            ProgramCount = _apps.TotalCount;
            ProgramSizeText = F("Dash_TotalSizeFmt", _apps.TotalSizeText);
        }
        if (_storeApps.IsLoaded) StoreAppCount = _storeApps.UserAppCount;
        if (_startup.IsLoaded)
        {
            StartupCount = _startup.EnabledCount;
            StartupDetail = F("Dash_StartupDetailFmt", _startup.EnabledCount, _startup.TotalCount);
        }
        if (_extensions.IsLoaded)
        {
            ExtensionCount = _extensions.TotalCount;
            ExtensionDetail = F("Dash_ExtensionsDetailFmt", _extensions.BrowserCount);
        }
    }

    /// <summary>Simple, explainable score: points off for a full system drive, high memory use, heat and many startup apps.</summary>
    private void UpdateHealth()
    {
        var s = _lastSnapshot;
        if (s is null) return;
        int score = 100;
        var tips = new List<HealthTip>();

        var sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = s.Drives.FirstOrDefault(d => string.Equals(d.Name, sysRoot, StringComparison.OrdinalIgnoreCase));
        if (drive is not null)
        {
            if (drive.UsedFraction > 0.9) score -= 30;
            else if (drive.UsedFraction > 0.8) score -= 15;
            if (drive.UsedFraction > 0.8)
                tips.Add(new HealthTip
                {
                    Text = F("Tip_DiskFullFmt", drive.Name.TrimEnd('\\'), ByteSize.Humanize(drive.Free)),
                    ActionText = T("Tip_CleanJunk"),
                    Target = "Junk",
                    Symbol = SymbolRegular.HardDrive24
                });
        }

        if (s.MemPercent > 90) score -= 15;
        else if (s.MemPercent > 80) score -= 8;
        if (s.MemPercent > 80)
            tips.Add(new HealthTip { Text = T("Tip_MemoryHigh"), ActionText = T("Tip_SeeProcesses"), Target = "SystemMonitor", Symbol = SymbolRegular.DeveloperBoard24 });

        if (s.TemperatureC is > 85)
        {
            score -= 10;
            tips.Add(new HealthTip { Text = T("Tip_Hot"), ActionText = T("Tip_SeeProcesses"), Target = "SystemMonitor", Symbol = SymbolRegular.Temperature24 });
        }

        if (_startup.IsLoaded)
        {
            if (_startup.EnabledCount > 12) score -= 12;
            else if (_startup.EnabledCount > 7) score -= 6;
            if (_startup.EnabledCount > 7)
                tips.Add(new HealthTip
                {
                    Text = F("Tip_StartupFmt", _startup.EnabledCount),
                    ActionText = T("Tip_ManageStartup"),
                    Target = "Startup",
                    Symbol = SymbolRegular.Power24
                });
        }

        score = Math.Clamp(score, 0, 100);
        HealthScore = score;
        HealthFraction = score / 100.0;
        (HealthTitle, HealthText) = score switch
        {
            >= 85 => (T("Health_Great"), T("Health_GreatText")),
            >= 65 => (T("Health_Good"), T("Health_GoodText")),
            _ => (T("Health_Attention"), T("Health_AttentionText"))
        };
        if (tips.Count == 0)
            tips.Add(new HealthTip { Text = T("Tip_AllGood"), ActionText = T("Tip_ScanJunk"), Target = "Junk", Symbol = SymbolRegular.ShieldCheckmark24 });
        if (Tips.Count != tips.Count || !Tips.Select(t => t.Text).SequenceEqual(tips.Select(t => t.Text)))
            Tips.Reset(tips);
    }

    [RelayCommand]
    private void Go(string tag) => Navigation.Navigate(tag);
}
