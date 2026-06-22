using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Install monitor (Revo-style) using a baseline/compare snapshot. Take a baseline
/// before installing something, run the installer, then compare to see exactly
/// which programs, folders and registry keys were added.
/// </summary>
public sealed class MonitorService
{
    private static readonly string BaselineFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Oblivion", "baseline.json");

    private sealed class Snapshot
    {
        public HashSet<string> Programs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RegistryRun { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public bool HasBaseline => File.Exists(BaselineFile);

    public Task<(int Programs, int Folders)> TakeBaselineAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var snap = Capture(ct);
            Directory.CreateDirectory(Path.GetDirectoryName(BaselineFile)!);
            File.WriteAllText(BaselineFile, JsonSerializer.Serialize(snap));
            return (snap.Programs.Count, snap.Folders.Count);
        }, ct);

    public Task<IReadOnlyList<MonitorChange>> CompareAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<MonitorChange>>(() =>
        {
            if (!File.Exists(BaselineFile))
                return Array.Empty<MonitorChange>();

            var baseline = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(BaselineFile)) ?? new Snapshot();
            var current = Capture(ct);
            var changes = new List<MonitorChange>();

            foreach (var p in current.Programs.Except(baseline.Programs))
                changes.Add(new MonitorChange { Kind = "Program", Value = p });
            foreach (var f in current.Folders.Except(baseline.Folders))
                changes.Add(new MonitorChange { Kind = "Folder", Value = f });
            foreach (var r in current.RegistryRun.Except(baseline.RegistryRun))
                changes.Add(new MonitorChange { Kind = "Registry", Value = r });

            return changes
                .OrderBy(c => c.Kind)
                .ThenBy(c => c.Value, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);

    private static Snapshot Capture(CancellationToken ct)
    {
        var snap = new Snapshot();

        // Installed programs from the uninstall hives.
        foreach (var (hive, view) in new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default),
        })
        {
            ct.ThrowIfCancellationRequested();
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

        // Top-level program folders.
        foreach (var root in new[]
        {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
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

        // Run keys.
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var run = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (run is null) continue;
            foreach (var name in run.GetValueNames())
                snap.RegistryRun.Add($"{name} = {run.GetValue(name)}");
        }

        return snap;
    }
}
