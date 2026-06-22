using System.IO;
using Microsoft.Win32;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Enumerates and manages auto-start programs from the Run/RunOnce registry keys
/// (HKLM + HKCU) and the per-user / all-users Startup folders.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string Run = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnce = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

    public Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            var entries = new List<StartupEntry>();

            ReadRunKey(Registry.LocalMachine, Run, StartupLocation.HklmRun, entries);
            ReadRunKey(Registry.CurrentUser, Run, StartupLocation.HkcuRun, entries);
            ReadRunKey(Registry.LocalMachine, RunOnce, StartupLocation.HklmRunOnce, entries);
            ReadRunKey(Registry.CurrentUser, RunOnce, StartupLocation.HkcuRunOnce, entries);

            ReadStartupFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                StartupLocation.CommonStartupFolder, entries);
            ReadStartupFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                StartupLocation.UserStartupFolder, entries);

            return entries
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);

    private static void ReadRunKey(RegistryKey hive, string path, StartupLocation location, List<StartupEntry> sink)
    {
        using var key = hive.OpenSubKey(path);
        if (key is null) return;

        foreach (var name in key.GetValueNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            var command = key.GetValue(name)?.ToString() ?? "";
            sink.Add(new StartupEntry
            {
                Name = name,
                Command = command,
                Location = location,
                Source = $@"{HiveShort(hive)}\{path}"
            });
        }
    }

    private static void ReadStartupFolder(string folder, StartupLocation location, List<StartupEntry> sink)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            sink.Add(new StartupEntry
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Command = file,
                Location = location,
                Source = folder
            });
        }
    }

    public Task DeleteAsync(StartupEntry entry, CancellationToken ct = default)
        => Task.Run(() =>
        {
            switch (entry.Location)
            {
                case StartupLocation.CommonStartupFolder:
                case StartupLocation.UserStartupFolder:
                    if (File.Exists(entry.Command))
                        Helpers.RecycleBin.Delete(entry.Command);
                    break;

                default:
                    var (hive, path) = ParseSource(entry.Source);
                    using (var key = hive.OpenSubKey(path, writable: true))
                        key?.DeleteValue(entry.Name, throwOnMissingValue: false);
                    break;
            }
        }, ct);

    private static (RegistryKey Hive, string Path) ParseSource(string source)
    {
        int slash = source.IndexOf('\\');
        var hivePart = source[..slash];
        var rest = source[(slash + 1)..];
        var hive = hivePart.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
            ? Registry.CurrentUser
            : Registry.LocalMachine;
        return (hive, rest);
    }

    private static string HiveShort(RegistryKey hive) =>
        hive.Name.StartsWith("HKEY_CURRENT_USER", StringComparison.Ordinal) ? "HKCU" : "HKLM";
}
