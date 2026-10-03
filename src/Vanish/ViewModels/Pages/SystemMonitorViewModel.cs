using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

/// <summary>
/// Live system monitor: CPU, memory, temperature, drives, network, battery and the
/// busiest processes. Samples every second only while the page is open.
/// </summary>
public sealed partial class SystemMonitorViewModel : PageViewModel
{
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "memory compression", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "smss", "svchost", "dwm", "explorer", "fontdrvhost", "sihost", "spoolsv", "audiodg", "ctfmon", "conhost",
        "runtimebroker", "searchhost", "startmenuexperiencehost", "textinputhost", "shellexperiencehost", "msmpeng",
        "securityhealthservice", "nissrv", "wudfhost", "dashost", "oblivion"
    };

    private readonly SystemMonitorService _monitor;
    private IDisposable? _subscription;
    private SystemSnapshot? _last;

    public SystemMonitorViewModel(SystemMonitorService monitor) => _monitor = monitor;

    // ---- static ---------------------------------------------------------------
    [ObservableProperty] private string _cpuName = "—";
    [ObservableProperty] private string _cpuDetail = "";
    [ObservableProperty] private string _osName = "—";
    [ObservableProperty] private string _machine = "";
    [ObservableProperty] private string _gpu = "—";
    [ObservableProperty] private string _totalRamText = "—";
    [ObservableProperty] private string _uptimeText = "—";

    // ---- live -----------------------------------------------------------------
    [ObservableProperty] private double _cpu;
    [ObservableProperty] private double _cpuPercent = double.NaN;
    [ObservableProperty] private IReadOnlyList<double>? _cpuHistory;
    [ObservableProperty] private double _ram;
    [ObservableProperty] private double _ramPercent = double.NaN;
    [ObservableProperty] private string _ramUsedText = "—";
    [ObservableProperty] private IReadOnlyList<double>? _ramHistory;
    [ObservableProperty] private bool _hasTemp;
    [ObservableProperty] private double _temp;
    [ObservableProperty] private string _tempText = "—";
    [ObservableProperty] private string _tempState = "";
    [ObservableProperty] private string _netDownText = "—";
    [ObservableProperty] private string _netUpText = "—";
    [ObservableProperty] private IReadOnlyList<double>? _netDownHistory;
    [ObservableProperty] private IReadOnlyList<double>? _netUpHistory;
    [ObservableProperty] private bool _hasBattery;
    [ObservableProperty] private double _battery;
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private string _batteryDetail = "";
    [ObservableProperty] private string _processMode = "cpu";   // cpu | mem

    public ObservableCollectionEx<DriveRow> Drives { get; } = new();
    public ObservableCollectionEx<ProcessRow> Processes { get; } = new();

    partial void OnProcessModeChanged(string value) => ApplyProcesses();

    public override void OnShown()
    {
        base.OnShown();
        _ = LoadStaticAsync();
        _subscription ??= _monitor.Subscribe(Apply, detailed: true);
    }

    public override void OnHidden()
    {
        base.OnHidden();
        _subscription?.Dispose();
        _subscription = null;
    }

    public override void RefreshTexts()
    {
        _ = LoadStaticAsync();
        if (_last is not null) Apply(_last);
    }

    private async Task LoadStaticAsync()
    {
        var info = await _monitor.GetStaticInfoAsync();
        CpuName = info.CpuName;
        var parts = new List<string>();
        if (info.Cores > 0) parts.Add(F("Mon_CoresFmt", info.Cores));
        parts.Add(F("Mon_ThreadsFmt", info.Threads));
        if (info.MaxGhz > 0) parts.Add($"{info.MaxGhz.ToString("0.0#", CultureInfo.CurrentCulture)} GHz");
        CpuDetail = string.Join(" · ", parts);
        OsName = info.OsName;
        Machine = info.Machine;
        Gpu = string.IsNullOrWhiteSpace(info.Gpu) ? "—" : info.Gpu;
        TotalRamText = ByteSize.Humanize((long)info.TotalRam);
    }

    private void Apply(SystemSnapshot s)
    {
        _last = s;
        Cpu = s.Cpu / 100;
        CpuPercent = s.Cpu;
        CpuHistory = s.CpuHistory;

        Ram = s.MemPercent / 100;
        RamPercent = s.MemPercent;
        RamUsedText = F("Mon_RamUsedFmt", ByteSize.Humanize((long)s.MemUsed), ByteSize.Humanize((long)s.MemTotal));
        RamHistory = s.MemHistory;

        HasTemp = s.TemperatureC is not null;
        if (s.TemperatureC is { } t)
        {
            Temp = Math.Clamp(t / 100.0, 0, 1);
            TempText = $"{t.ToString("0", CultureInfo.CurrentCulture)} °C";
            TempState = T(t switch { < 60 => "Mon_TempCool", < 80 => "Mon_TempWarm", _ => "Mon_TempHot" });
        }
        else
        {
            Temp = 0;
            TempText = "—";
            TempState = T("Mon_TempUnavailable");
        }

        NetDownText = ByteSize.Rate(s.NetDown);
        NetUpText = ByteSize.Rate(s.NetUp);
        NetDownHistory = s.NetDownHistory;
        NetUpHistory = s.NetUpHistory;

        if (Drives.Count != s.Drives.Count || !Drives.SequenceEqual(s.Drives)) Drives.Reset(s.Drives);

        HasBattery = s.Battery is not null;
        if (s.Battery is { } b)
        {
            Battery = b.Percent / 100.0;
            BatteryText = b.Charging ? F("Live_BatteryChargingFmt", b.Percent) : F("Live_BatteryFmt", b.Percent);
            var details = new List<string>();
            if (b.Remaining is { } r && !b.OnAc) details.Add(F("Mon_BatteryRemainingFmt", (int)r.TotalHours, r.Minutes));
            if (b.OnAc) details.Add(T("Mon_OnAc"));
            if (b.HealthPercent is { } h) details.Add(F("Mon_BatteryHealthFmt", h));
            if (b.Cycles is { } c) details.Add(F("Mon_BatteryCyclesFmt", c));
            BatteryDetail = string.Join(" · ", details);
        }

        var up = s.Uptime;
        UptimeText = up.TotalDays >= 1 ? F("Mon_UptimeDaysFmt", (int)up.TotalDays, up.Hours, up.Minutes) : F("Mon_UptimeFmt", up.Hours, up.Minutes);

        ApplyProcesses();
    }

    private void ApplyProcesses()
    {
        if (_last is null) return;
        var list = ProcessMode == "mem" ? _last.TopMemory : _last.TopCpu;
        if (list.Count > 0) Processes.Reset(list);
    }

    [RelayCommand]
    private async Task EndProcessAsync(ProcessRow? row)
    {
        if (row is null) return;
        if (Critical.Contains(row.Name))
        {
            Toast.Show(F("Mon_CriticalFmt", row.Name), ToastKind.Warning);
            return;
        }
        bool ok = await Dialogs.ConfirmAsync(F("Mon_EndTitleFmt", row.Name),
            row.Count > 1 ? F("Mon_EndManyTextFmt", row.Count) : T("Mon_EndText"),
            T("Act_EndTask"), DialogTone.Danger, row.Path is null ? null : new[] { row.Path });
        if (!ok) return;

        int killed = 0;
        foreach (var p in Process.GetProcessesByName(row.Name))
        {
            using (p)
            {
                try
                {
                    p.Kill();
                    killed++;
                }
                catch { /* access denied / exited */ }
            }
        }
        if (killed > 0)
        {
            Log.Append("Log_EndTask", row.Name);
            Toast.Show(F("Mon_EndedFmt", row.Name), ToastKind.Success);
        }
        else
        {
            Toast.Show(F("Mon_EndFailedFmt", row.Name), ToastKind.Error);
        }
    }

    [RelayCommand]
    private void RevealProcess(ProcessRow? row)
    {
        if (row?.Path is { } p) Shell.Reveal(p);
    }

    [RelayCommand]
    private void OpenTaskManager() => Shell.Start("taskmgr.exe");
}
