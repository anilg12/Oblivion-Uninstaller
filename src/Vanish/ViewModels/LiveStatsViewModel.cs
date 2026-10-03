using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels;

/// <summary>
/// Live CPU / RAM / temperature / disk / network figures for the right-hand panel.
/// Sampling only runs while the panel is visible and the window is not minimized.
/// </summary>
public sealed partial class LiveStatsViewModel : ObservableObject
{
    private readonly SystemMonitorService _monitor;
    private IDisposable? _subscription;

    public LiveStatsViewModel(SystemMonitorService monitor) => _monitor = monitor;

    [ObservableProperty] private double _cpu;
    [ObservableProperty] private double _cpuPercent = double.NaN;
    [ObservableProperty] private string _tempText = "—";
    [ObservableProperty] private bool _hasTemp;
    [ObservableProperty] private bool _tempHot;
    [ObservableProperty] private double _ram;
    [ObservableProperty] private double _ramPercent = double.NaN;
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private double _disk;
    [ObservableProperty] private string _diskTitle = "C:";
    [ObservableProperty] private string _diskText = "—";
    [ObservableProperty] private string _netDownText = "—";
    [ObservableProperty] private string _netUpText = "—";
    [ObservableProperty] private IReadOnlyList<double>? _netHistory;
    [ObservableProperty] private IReadOnlyList<double>? _cpuHistory;
    [ObservableProperty] private bool _hasBattery;
    [ObservableProperty] private double _battery;
    [ObservableProperty] private string _batteryText = "";

    public bool IsRunning => _subscription is not null;

    public void Start()
    {
        _subscription ??= _monitor.Subscribe(Apply, detailed: false);
    }

    public void Stop()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void Apply(SystemSnapshot s)
    {
        Cpu = s.Cpu / 100.0;
        CpuPercent = s.Cpu;
        CpuHistory = s.CpuHistory;

        HasTemp = s.TemperatureC is not null;
        TempText = s.TemperatureC is { } t ? $"{t.ToString("0", CultureInfo.CurrentCulture)} °C" : Loc.I["Live_TempNA"];
        TempHot = s.TemperatureC is > 80;

        Ram = s.MemPercent / 100.0;
        RamPercent = s.MemPercent;
        RamText = $"{ByteSize.Humanize((long)s.MemUsed)} / {ByteSize.Humanize((long)s.MemTotal)}";

        var sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = s.Drives.FirstOrDefault(d => string.Equals(d.Name, sysRoot, StringComparison.OrdinalIgnoreCase))
                    ?? s.Drives.FirstOrDefault();
        if (drive is not null)
        {
            Disk = drive.UsedFraction;
            DiskTitle = drive.Name.TrimEnd('\\');
            DiskText = string.Format(Loc.I["Live_DiskFreeFmt"], ByteSize.Humanize(drive.Free), ByteSize.Humanize(drive.Total));
        }

        NetDownText = ByteSize.Rate(s.NetDown);
        NetUpText = ByteSize.Rate(s.NetUp);
        NetHistory = s.NetDownHistory.Zip(s.NetUpHistory, (a, b) => a + b).ToArray();

        HasBattery = s.Battery is not null;
        if (s.Battery is { } b)
        {
            Battery = b.Percent / 100.0;
            BatteryText = b.Charging
                ? string.Format(Loc.I["Live_BatteryChargingFmt"], b.Percent)
                : string.Format(Loc.I["Live_BatteryFmt"], b.Percent);
        }
    }
}
