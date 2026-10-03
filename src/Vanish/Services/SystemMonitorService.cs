using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using Vanish.Helpers;

namespace Vanish.Services;

public sealed record ProcessRow(string Name, int Count, double CpuPercent, long MemoryBytes, string? Path, int Pid)
{
    public string CpuText => $"{CpuPercent:0.0}%";
    public string MemoryText => ByteSize.Humanize(MemoryBytes);
    public string Title => Count > 1 ? $"{Name}  ({Count})" : Name;
}

public sealed record DriveRow(string Name, string Label, long Total, long Free)
{
    public double UsedFraction => Total > 0 ? (Total - Free) / (double)Total : 0;
    public string Title => string.IsNullOrWhiteSpace(Label) ? Name : $"{Label} ({Name.TrimEnd('\\')})";
    public string Text => $"{ByteSize.Humanize(Free)} / {ByteSize.Humanize(Total)}";
}

public sealed record BatteryInfo(int Percent, bool Charging, bool OnAc, TimeSpan? Remaining, int? HealthPercent, int? Cycles);

public sealed class SystemSnapshot
{
    public double Cpu { get; init; }
    public IReadOnlyList<double> CpuHistory { get; init; } = Array.Empty<double>();
    public double? TemperatureC { get; init; }
    public ulong MemTotal { get; init; }
    public ulong MemUsed { get; init; }
    public double MemPercent { get; init; }
    public IReadOnlyList<double> MemHistory { get; init; } = Array.Empty<double>();
    public double NetDown { get; init; }
    public double NetUp { get; init; }
    public IReadOnlyList<double> NetDownHistory { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> NetUpHistory { get; init; } = Array.Empty<double>();
    public IReadOnlyList<DriveRow> Drives { get; init; } = Array.Empty<DriveRow>();
    public BatteryInfo? Battery { get; init; }
    public TimeSpan Uptime { get; init; }
    public IReadOnlyList<ProcessRow> TopCpu { get; init; } = Array.Empty<ProcessRow>();
    public IReadOnlyList<ProcessRow> TopMemory { get; init; } = Array.Empty<ProcessRow>();
}

public sealed class StaticSystemInfo
{
    public string CpuName { get; init; } = "—";
    public int Cores { get; init; }
    public int Threads { get; init; }
    public double MaxGhz { get; init; }
    public string OsName { get; init; } = "Windows";
    public string Machine { get; init; } = "";
    public string Gpu { get; init; } = "";
    public ulong TotalRam { get; init; }
}

/// <summary>
/// Live system statistics. Sampling runs on a background timer only while something
/// is subscribed (the right panel and/or the System Monitor page), so it costs
/// nothing when the window is hidden. Expensive readings (temperature, processes,
/// drives) run at a lower rate.
/// </summary>
public sealed class SystemMonitorService
{
    private const int HistoryLength = 60;

    private readonly object _gate = new();
    private readonly List<Subscription> _subs = new();
    private Timer? _timer;
    private int _busy;
    private int _tick;

    private ulong _prevIdle, _prevKernel, _prevUser;
    private long _prevNetIn = -1, _prevNetOut = -1;
    private long _prevNetStamp;
    private readonly Queue<double> _cpuHist = new(), _memHist = new(), _netDownHist = new(), _netUpHist = new();
    private Dictionary<int, TimeSpan> _prevProcCpu = new();
    private long _prevProcStamp;
    private readonly Dictionary<int, string?> _pathCache = new();

    private double? _temperature;
    private int _tempFailures;
    private IReadOnlyList<DriveRow> _drives = Array.Empty<DriveRow>();
    private IReadOnlyList<ProcessRow> _topCpu = Array.Empty<ProcessRow>(), _topMem = Array.Empty<ProcessRow>();
    private (int? Health, int? Cycles)? _batteryHealth;

    private Task<StaticSystemInfo>? _static;

    public SystemSnapshot? Last { get; private set; }

    /// <summary>Hardware/OS facts that never change while running (computed once, in the background).</summary>
    public Task<StaticSystemInfo> GetStaticInfoAsync() => _static ??= Task.Run(ReadStaticInfo);

    private sealed class Subscription : IDisposable
    {
        public required SystemMonitorService Owner { get; init; }
        public required Action<SystemSnapshot> Callback { get; init; }
        public required Dispatcher Dispatcher { get; init; }
        public required bool Detailed { get; init; }
        public void Dispose() => Owner.Unsubscribe(this);
    }

    /// <summary>
    /// Receive snapshots on the calling (UI) thread. <paramref name="detailed"/> adds the
    /// per-process lists and a 1 s refresh rate; otherwise every 2 s.
    /// </summary>
    public IDisposable Subscribe(Action<SystemSnapshot> callback, bool detailed)
    {
        var sub = new Subscription { Owner = this, Callback = callback, Dispatcher = Dispatcher.CurrentDispatcher, Detailed = detailed };
        lock (_gate)
        {
            _subs.Add(sub);
            Reschedule();
        }
        if (Last is { } last) callback(last);
        return sub;
    }

    private void Unsubscribe(Subscription sub)
    {
        lock (_gate)
        {
            _subs.Remove(sub);
            Reschedule();
        }
    }

    private void Reschedule()
    {
        if (_subs.Count == 0)
        {
            _timer?.Dispose();
            _timer = null;
            return;
        }
        int period = _subs.Any(s => s.Detailed) ? 1000 : 2000;
        if (_timer is null)
            _timer = new Timer(_ => Sample(), null, 0, period);
        else
            _timer.Change(0, period);
    }

    private void Sample()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            bool detailed;
            lock (_gate) detailed = _subs.Any(s => s.Detailed);
            int tick = _tick++;

            double cpu = SampleCpu();
            var mem = new Native.MEMORYSTATUSEX();
            Native.GlobalMemoryStatusEx(mem);
            ulong used = mem.ullTotalPhys - mem.ullAvailPhys;
            double memPct = mem.ullTotalPhys > 0 ? used * 100.0 / mem.ullTotalPhys : 0;
            var (down, up) = SampleNetwork();

            if (tick % 3 == 0) _temperature = ReadTemperature();
            if (tick % 10 == 0 || _drives.Count == 0) _drives = ReadDrives();
            if (detailed && (tick % 2 == 0 || _topCpu.Count == 0)) SampleProcesses();

            Push(_cpuHist, cpu);
            Push(_memHist, memPct);
            Push(_netDownHist, down);
            Push(_netUpHist, up);

            var snap = new SystemSnapshot
            {
                Cpu = cpu,
                CpuHistory = _cpuHist.ToArray(),
                TemperatureC = _temperature,
                MemTotal = mem.ullTotalPhys,
                MemUsed = used,
                MemPercent = memPct,
                MemHistory = _memHist.ToArray(),
                NetDown = down,
                NetUp = up,
                NetDownHistory = _netDownHist.ToArray(),
                NetUpHistory = _netUpHist.ToArray(),
                Drives = _drives,
                Battery = ReadBattery(),
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                TopCpu = detailed ? _topCpu : Array.Empty<ProcessRow>(),
                TopMemory = detailed ? _topMem : Array.Empty<ProcessRow>()
            };
            Last = snap;

            List<Subscription> subs;
            lock (_gate) subs = _subs.ToList();
            foreach (var s in subs)
                s.Dispatcher.InvokeAsync(() => s.Callback(snap), DispatcherPriority.Background);
        }
        catch { /* never let sampling crash the app */ }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static void Push(Queue<double> q, double v)
    {
        q.Enqueue(double.IsFinite(v) ? v : 0);
        while (q.Count > HistoryLength) q.Dequeue();
    }

    private double SampleCpu()
    {
        if (!Native.GetSystemTimes(out var idleFt, out var kernelFt, out var userFt)) return 0;
        ulong idle = idleFt.Value, kernel = kernelFt.Value, user = userFt.Value;
        double pct = 0;
        if (_prevKernel != 0)
        {
            ulong dIdle = idle - _prevIdle, dKernel = kernel - _prevKernel, dUser = user - _prevUser;
            ulong total = dKernel + dUser; // kernel time includes idle time
            if (total > 0) pct = Math.Clamp((1.0 - dIdle / (double)total) * 100.0, 0, 100);
        }
        _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
        return pct;
    }

    private (double Down, double Up) SampleNetwork()
    {
        try
        {
            long inBytes = 0, outBytes = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var st = nic.GetIPStatistics();
                inBytes += st.BytesReceived;
                outBytes += st.BytesSent;
            }
            long now = Stopwatch.GetTimestamp();
            double down = 0, up = 0;
            if (_prevNetIn >= 0)
            {
                double secs = (now - _prevNetStamp) / (double)Stopwatch.Frequency;
                if (secs > 0)
                {
                    down = Math.Max(0, (inBytes - _prevNetIn) / secs);
                    up = Math.Max(0, (outBytes - _prevNetOut) / secs);
                }
            }
            _prevNetIn = inBytes; _prevNetOut = outBytes; _prevNetStamp = now;
            return (down, up);
        }
        catch { return (0, 0); }
    }

    private double? ReadTemperature()
    {
        if (_tempFailures >= 3) return null;
        try
        {
            using var s = new ManagementObjectSearcher(@"root\CIMV2",
                "SELECT HighPrecisionTemperature, Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
            double best = double.NaN;
            foreach (ManagementBaseObject mo in s.Get())
            {
                using (mo)
                {
                    double kelvin = 0;
                    if (mo["HighPrecisionTemperature"] is { } hp && Convert.ToDouble(hp) > 0) kelvin = Convert.ToDouble(hp) / 10.0;
                    else if (mo["Temperature"] is { } t) kelvin = Convert.ToDouble(t);
                    double c = kelvin - 273.15;
                    if (c is > 5 and < 125) best = double.IsNaN(best) ? c : Math.Max(best, c);
                }
            }
            if (!double.IsNaN(best)) { _tempFailures = 0; return best; }
        }
        catch { /* try the ACPI class next */ }

        try
        {
            using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            double best = double.NaN;
            foreach (ManagementBaseObject mo in s.Get())
            {
                using (mo)
                {
                    if (mo["CurrentTemperature"] is not { } raw) continue;
                    double c = Convert.ToDouble(raw) / 10.0 - 273.15;
                    if (c is > 5 and < 125) best = double.IsNaN(best) ? c : Math.Max(best, c);
                }
            }
            if (!double.IsNaN(best)) { _tempFailures = 0; return best; }
        }
        catch { /* not supported on this machine */ }

        _tempFailures++;
        return null;
    }

    private static IReadOnlyList<DriveRow> ReadDrives()
    {
        var list = new List<DriveRow>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    list.Add(new DriveRow(d.Name, d.VolumeLabel, d.TotalSize, d.AvailableFreeSpace));
                }
                catch { /* not ready */ }
            }
        }
        catch { /* ignore */ }
        return list;
    }

    private BatteryInfo? ReadBattery()
    {
        if (!Native.GetSystemPowerStatus(out var st)) return null;
        if (st.BatteryFlag == 128 || st.BatteryFlag == 255 || st.BatteryLifePercent == 255) return null;
        _batteryHealth ??= ReadBatteryHealth();
        bool charging = (st.BatteryFlag & 8) != 0;
        TimeSpan? remaining = st.BatteryLifeTime > 0 ? TimeSpan.FromSeconds(st.BatteryLifeTime) : null;
        return new BatteryInfo(st.BatteryLifePercent, charging, st.ACLineStatus == 1, remaining,
            _batteryHealth?.Health, _batteryHealth?.Cycles);
    }

    private static (int? Health, int? Cycles) ReadBatteryHealth()
    {
        int? health = null, cycles = null;
        try
        {
            long design = 0, full = 0;
            using (var s = new ManagementObjectSearcher(@"root\WMI", "SELECT DesignedCapacity FROM BatteryStaticData"))
                foreach (ManagementBaseObject mo in s.Get()) using (mo) design += Convert.ToInt64(mo["DesignedCapacity"] ?? 0);
            using (var s = new ManagementObjectSearcher(@"root\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity"))
                foreach (ManagementBaseObject mo in s.Get()) using (mo) full += Convert.ToInt64(mo["FullChargedCapacity"] ?? 0);
            if (design > 0 && full > 0) health = (int)Math.Clamp(Math.Round(full * 100.0 / design), 1, 100);
        }
        catch { /* not exposed */ }
        try
        {
            using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT CycleCount FROM BatteryCycleCount");
            foreach (ManagementBaseObject mo in s.Get()) using (mo) { var c = Convert.ToInt32(mo["CycleCount"] ?? 0); if (c > 0) cycles = c; }
        }
        catch { /* not exposed */ }
        return (health, cycles);
    }

    private void SampleProcesses()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsedMs = _prevProcStamp == 0 ? 0 : (now - _prevProcStamp) * 1000.0 / Stopwatch.Frequency;
        var cpuNow = new Dictionary<int, TimeSpan>();
        var groups = new Dictionary<string, (int Count, double Cpu, long Mem, int Pid)>(StringComparer.OrdinalIgnoreCase);
        int logical = Math.Max(1, Environment.ProcessorCount);

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    int pid = p.Id;
                    if (pid <= 4) continue; // Idle / System
                    string name = p.ProcessName;
                    long mem = p.WorkingSet64;
                    double cpu = 0;
                    try
                    {
                        var t = p.TotalProcessorTime;
                        cpuNow[pid] = t;
                        if (elapsedMs > 0 && _prevProcCpu.TryGetValue(pid, out var prev))
                            cpu = Math.Max(0, (t - prev).TotalMilliseconds / elapsedMs / logical * 100.0);
                    }
                    catch { /* protected process */ }

                    groups.TryGetValue(name, out var g);
                    groups[name] = (g.Count + 1, g.Cpu + cpu, g.Mem + mem, g.Pid == 0 || mem > g.Mem ? pid : g.Pid);
                }
                catch { /* exited */ }
            }
        }
        _prevProcCpu = cpuNow;
        _prevProcStamp = now;

        ProcessRow Row(KeyValuePair<string, (int Count, double Cpu, long Mem, int Pid)> kv)
        {
            if (!_pathCache.TryGetValue(kv.Value.Pid, out var path))
            {
                path = Native.ProcessPath(kv.Value.Pid);
                _pathCache[kv.Value.Pid] = path;
            }
            return new ProcessRow(kv.Key, kv.Value.Count, Math.Min(100, kv.Value.Cpu), kv.Value.Mem, path, kv.Value.Pid);
        }

        _topCpu = groups.OrderByDescending(g => g.Value.Cpu).Take(7).Select(Row).ToList();
        _topMem = groups.OrderByDescending(g => g.Value.Mem).Take(7).Select(Row).ToList();
        if (_pathCache.Count > 400) _pathCache.Clear();
    }

    private static StaticSystemInfo ReadStaticInfo()
    {
        string cpu = "—";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpu = (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "—";
            while (cpu.Contains("  ")) cpu = cpu.Replace("  ", " ");
        }
        catch { /* ignore */ }

        int cores = 0;
        string machine = "", gpu = "";
        try
        {
            using var s = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
            foreach (ManagementBaseObject mo in s.Get()) using (mo) cores += Convert.ToInt32(mo["NumberOfCores"] ?? 0);
        }
        catch { /* ignore */ }
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            foreach (ManagementBaseObject mo in s.Get())
                using (mo) machine = $"{mo["Manufacturer"]} {mo["Model"]}".Trim();
        }
        catch { /* ignore */ }
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementBaseObject mo in s.Get())
                using (mo)
                {
                    var n = mo["Name"] as string;
                    if (string.IsNullOrWhiteSpace(n) || n.Contains("Basic", StringComparison.OrdinalIgnoreCase)) continue;
                    gpu = n.Trim();
                    break;
                }
        }
        catch { /* ignore */ }

        double ghz = 0;
        try
        {
            int n = Environment.ProcessorCount;
            var buf = new Native.PROCESSOR_POWER_INFORMATION[n];
            uint size = (uint)(Marshal.SizeOf<Native.PROCESSOR_POWER_INFORMATION>() * n);
            if (Native.CallNtPowerInformation(11, IntPtr.Zero, 0, buf, size) == 0 && buf.Length > 0)
                ghz = buf.Max(b => b.MaxMhz) / 1000.0;
        }
        catch { /* ignore */ }

        var mem = new Native.MEMORYSTATUSEX();
        Native.GlobalMemoryStatusEx(mem);

        return new StaticSystemInfo
        {
            CpuName = cpu,
            Cores = cores,
            Threads = Environment.ProcessorCount,
            MaxGhz = ghz,
            OsName = OsDescription(),
            Machine = machine,
            Gpu = gpu,
            TotalRam = mem.ullTotalPhys
        };
    }

    /// <summary>"Windows 11 Pro 24H2 (26100.2033)". ProductName still says "Windows 10" on 11.</summary>
    public static string OsDescription()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName") as string ?? "Windows";
            var display = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string ?? "";
            var buildText = key?.GetValue("CurrentBuildNumber") as string ?? key?.GetValue("CurrentBuild") as string ?? "";
            var ubr = key?.GetValue("UBR") is int u ? u : 0;
            if (int.TryParse(buildText, out var build) && build >= 22000)
                product = product.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
            var version = string.IsNullOrWhiteSpace(display) ? "" : $" {display}";
            var buildPart = string.IsNullOrWhiteSpace(buildText) ? "" : $" ({buildText}{(ubr > 0 ? "." + ubr : "")})";
            return $"{product}{version}{buildPart}";
        }
        catch
        {
            return Environment.OSVersion.VersionString;
        }
    }
}
