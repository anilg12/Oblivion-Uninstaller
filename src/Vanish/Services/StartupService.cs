using System.IO;
using Microsoft.Win32;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Auto-start programs from the Run/RunOnce keys (HKLM, HKLM 32-bit, HKCU) and the
/// Startup folders. Entries can be switched off the same way Task Manager does it
/// (StartupApproved), so nothing is lost; deleting is also possible.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string Run = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnce = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string Approved = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private readonly ISystemRestoreService _restore;

    public StartupService(ISystemRestoreService restore) => _restore = restore;

    public Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            var entries = new List<StartupEntry>();
            ReadRunKey(Registry.LocalMachine, Run, StartupLocation.HklmRun, entries);
            ReadRunKey(Registry.LocalMachine, Run32, StartupLocation.HklmRun32, entries);
            ReadRunKey(Registry.CurrentUser, Run, StartupLocation.HkcuRun, entries);
            ReadRunKey(Registry.LocalMachine, RunOnce, StartupLocation.HklmRunOnce, entries);
            ReadRunKey(Registry.CurrentUser, RunOnce, StartupLocation.HkcuRunOnce, entries);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupLocation.CommonStartupFolder, entries);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupLocation.UserStartupFolder, entries);

            return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }, ct);

    private static void ReadRunKey(RegistryKey hive, string path, StartupLocation location, List<StartupEntry> sink)
    {
        using var key = hive.OpenSubKey(path);
        if (key is null) return;
        foreach (var name in key.GetValueNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            var command = key.GetValue(name)?.ToString() ?? "";
            var entry = new StartupEntry
            {
                Name = name,
                Command = command,
                Location = location,
                Source = $@"{HiveShort(hive)}\{path}",
                ExecutablePath = ParseExecutable(command)
            };
            entry.IsEnabled = ReadApproved(location, name);
            sink.Add(entry);
        }
    }

    private static void ReadStartupFolder(string folder, StartupLocation location, List<StartupEntry> sink)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            var entry = new StartupEntry
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Command = file,
                Location = location,
                Source = folder,
                ExecutablePath = file
            };
            entry.IsEnabled = ReadApproved(location, fileName);
            sink.Add(entry);
        }
    }

    private static (RegistryKey Hive, string Sub)? ApprovedKey(StartupLocation location) => location switch
    {
        StartupLocation.HkcuRun => (Registry.CurrentUser, Approved + @"\Run"),
        StartupLocation.HklmRun => (Registry.LocalMachine, Approved + @"\Run"),
        StartupLocation.HklmRun32 => (Registry.LocalMachine, Approved + @"\Run32"),
        StartupLocation.UserStartupFolder => (Registry.CurrentUser, Approved + @"\StartupFolder"),
        StartupLocation.CommonStartupFolder => (Registry.LocalMachine, Approved + @"\StartupFolder"),
        _ => null
    };

    private static bool ReadApproved(StartupLocation location, string valueName)
    {
        if (ApprovedKey(location) is not { } ak) return true;
        try
        {
            using var key = ak.Hive.OpenSubKey(ak.Sub);
            if (key?.GetValue(valueName) is byte[] { Length: > 0 } data)
                return (data[0] & 1) == 0; // 02/06 = on, 03/07 = off
        }
        catch { /* ignore */ }
        return true;
    }

    public Task SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken ct = default)
        => Task.Run(() =>
        {
            if (ApprovedKey(entry.Location) is not { } ak) return;
            var valueName = entry.Location is StartupLocation.UserStartupFolder or StartupLocation.CommonStartupFolder
                ? Path.GetFileName(entry.Command)
                : entry.Name;
            var data = new byte[12];
            data[0] = enabled ? (byte)0x02 : (byte)0x03;
            if (!enabled)
                BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            using var key = ak.Hive.CreateSubKey(ak.Sub, writable: true);
            key.SetValue(valueName, data, RegistryValueKind.Binary);
        }, ct);

    public async Task DeleteAsync(StartupEntry entry, CancellationToken ct = default)
    {
        switch (entry.Location)
        {
            case StartupLocation.CommonStartupFolder:
            case StartupLocation.UserStartupFolder:
                await Task.Run(() =>
                {
                    if (File.Exists(entry.Command)) RecycleBin.Delete(entry.Command);
                    RemoveApproved(entry.Location, Path.GetFileName(entry.Command));
                }, ct);
                break;

            default:
                await _restore.BackupRegistryKeyAsync(entry.Source, ct);
                await Task.Run(() =>
                {
                    var (hive, path) = ParseSource(entry.Source);
                    using (var key = hive.OpenSubKey(path, writable: true))
                        key?.DeleteValue(entry.Name, throwOnMissingValue: false);
                    RemoveApproved(entry.Location, entry.Name);
                }, ct);
                break;
        }
    }

    private static void RemoveApproved(StartupLocation location, string valueName)
    {
        if (ApprovedKey(location) is not { } ak) return;
        try
        {
            using var key = ak.Hive.OpenSubKey(ak.Sub, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch { /* ignore */ }
    }

    /// <summary>"\"C:\App\app.exe\" --tray" -> C:\App\app.exe</summary>
    public static string? ParseExecutable(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var c = Environment.ExpandEnvironmentVariables(command.Trim());
        if (c.StartsWith('"'))
        {
            int end = c.IndexOf('"', 1);
            return end > 1 ? c[1..end] : null;
        }
        int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? c[..(exe + 4)] : c.Split(' ')[0];
    }

    private static (RegistryKey Hive, string Path) ParseSource(string source)
    {
        int slash = source.IndexOf('\\');
        var hivePart = source[..slash];
        var rest = source[(slash + 1)..];
        var hive = hivePart.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
        return (hive, rest);
    }

    private static string HiveShort(RegistryKey hive) =>
        hive.Name.StartsWith("HKEY_CURRENT_USER", StringComparison.Ordinal) ? "HKCU" : "HKLM";
}
