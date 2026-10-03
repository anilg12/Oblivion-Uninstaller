using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

internal sealed class MonitorSnapshot
{
    public HashSet<string> Programs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RegistryRun { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[JsonSerializable(typeof(MonitorSnapshot))]
internal sealed partial class MonitorJsonContext : JsonSerializerContext { }

/// <summary>
/// Install monitor (baseline / compare). Take a baseline before installing something,
/// run the installer, then compare to see which programs, folders and autostart
/// entries were added — and remove them if wanted. Nothing is pre-selected.
/// </summary>
public sealed class MonitorService
{
    private static readonly string BaselineFile = Path.Combine(SettingsService.DataDir, "baseline.json");
    private const char Sep = '\u001F';

    public bool HasBaseline => File.Exists(BaselineFile);

    public DateTime? BaselineTime => HasBaseline ? File.GetLastWriteTime(BaselineFile) : null;

    public Task<(int Programs, int Folders)> TakeBaselineAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var snap = Capture(ct);
            Directory.CreateDirectory(SettingsService.DataDir);
            File.WriteAllText(BaselineFile, JsonSerializer.Serialize(snap, MonitorJsonContext.Default.MonitorSnapshot));
            return (snap.Programs.Count, snap.Folders.Count);
        }, ct);

    public Task<IReadOnlyList<MonitorChange>> CompareAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<MonitorChange>>(() =>
        {
            if (!File.Exists(BaselineFile)) return Array.Empty<MonitorChange>();
            var baseline = JsonSerializer.Deserialize(File.ReadAllText(BaselineFile), MonitorJsonContext.Default.MonitorSnapshot)
                           ?? new MonitorSnapshot();
            var current = Capture(ct);
            var changes = new List<MonitorChange>();

            foreach (var p in current.Programs.Except(baseline.Programs, StringComparer.OrdinalIgnoreCase))
                changes.Add(new MonitorChange { Kind = "Program", Value = p });
            // Baselines from older versions covered fewer roots: only compare roots they knew.
            var baselineRoots = new HashSet<string>(
                baseline.Folders.Select(f => Path.GetDirectoryName(f) ?? "").Where(r => r.Length > 0),
                StringComparer.OrdinalIgnoreCase);
            foreach (var f in current.Folders.Except(baseline.Folders, StringComparer.OrdinalIgnoreCase))
            {
                if (baselineRoots.Count > 0 && !baselineRoots.Contains(Path.GetDirectoryName(f) ?? "")) continue;
                changes.Add(new MonitorChange { Kind = "Folder", Value = f });
            }

            bool legacyRun = baseline.RegistryRun.Count > 0 && !baseline.RegistryRun.Any(r => r.Contains(Sep));
            foreach (var r in legacyRun ? Enumerable.Empty<string>() : current.RegistryRun.Except(baseline.RegistryRun, StringComparer.OrdinalIgnoreCase))
            {
                var parts = r.Split(Sep);
                if (parts.Length < 3) continue;
                changes.Add(new MonitorChange
                {
                    Kind = "Registry",
                    Value = $"{parts[1]} = {parts[2]}",
                    RegistryKey = parts[0],
                    RegistryValue = parts[1]
                });
            }

            return changes
                .OrderBy(c => c.Kind)
                .ThenBy(c => c.Value, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);

    /// <summary>Removes folders (to the Recycle Bin) and autostart values. Programs must be uninstalled.</summary>
    public Task<int> RemoveAsync(IReadOnlyList<MonitorChange> changes, ISystemRestoreService restore, CancellationToken ct = default)
        => Task.Run(async () =>
        {
            int removed = 0;
            foreach (var c in changes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (c.Kind == "Folder")
                    {
                        if (LeftoverScanService.IsProtectedPath(c.Value)) continue;
                        if (Directory.Exists(c.Value)) RecycleBin.Delete(c.Value);
                        removed++;
                    }
                    else if (c.Kind == "Registry" && c.RegistryKey is { } key && c.RegistryValue is { } value)
                    {
                        await restore.BackupRegistryKeyAsync(key, ct);
                        var hive = key.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                        using var k = hive.OpenSubKey(key[(key.IndexOf('\\') + 1)..], writable: true);
                        k?.DeleteValue(value, throwOnMissingValue: false);
                        removed++;
                    }
                }
                catch { /* skip */ }
            }
            return removed;
        }, ct);

    private static MonitorSnapshot Capture(CancellationToken ct)
    {
        var snap = new MonitorSnapshot();

        foreach (var (hive, view) in new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default),
        })
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var sub in uninstall.GetSubKeyNames())
                {
                    using var entry = uninstall.OpenSubKey(sub);
                    if (entry?.GetValue("DisplayName") is string dn && !string.IsNullOrWhiteSpace(dn))
                        snap.Programs.Add(dn);
                }
            }
            catch { /* ignore */ }
        }

        foreach (var root in new[]
        {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Environment.GetEnvironmentVariable("ProgramData"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                    snap.Folders.Add(dir);
            }
            catch { /* ignore */ }
        }

        foreach (var (hive, hiveName, path) in new[]
        {
            (Registry.CurrentUser, "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
        })
        {
            try
            {
                using var run = hive.OpenSubKey(path);
                if (run is null) continue;
                foreach (var name in run.GetValueNames())
                    snap.RegistryRun.Add($"{hiveName}\\{path}{Sep}{name}{Sep}{run.GetValue(name)}");
            }
            catch { /* ignore */ }
        }
        return snap;
    }
}
