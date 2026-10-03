using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace Vanish.Services;

public sealed record RestorePointInfo(int Sequence, string Description, DateTime Created, string Type)
{
    public string CreatedText => Created.ToString("dd.MM.yyyy HH:mm");
    public string TypeText => Helpers.Loc.I["RP_" + Type];
}

public sealed record RegistryBackupInfo(string FilePath, string KeyPath, DateTime Created, long SizeBytes)
{
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string CreatedText => Created.ToString("dd.MM.yyyy HH:mm");
    public string SizeText => Helpers.ByteSize.Humanize(SizeBytes);
}

/// <summary>
/// System Restore points (Checkpoint-Computer) and .reg backups of registry keys
/// taken before Oblivion deletes anything; also lists and restores them (Backup manager).
/// </summary>
public sealed class SystemRestoreService : ISystemRestoreService
{
    /// <summary>Where new backups go.</summary>
    public static readonly string BackupDir = Path.Combine(SettingsService.DataDir, "Backups");

    /// <summary>Backups made by Oblivion 1.x/2.x (still listed and restorable).</summary>
    private static readonly string LegacyBackupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vanish", "Backups");

    private const string SrKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string FrequencyValue = "SystemRestorePointCreationFrequency";

    public async Task<bool> CreateRestorePointAsync(string description, CancellationToken ct = default)
    {
        // Windows only allows one restore point per 24 h by default. Lift that limit
        // just for this call and put the original setting back afterwards.
        object? previous = null;
        bool changed = false;
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(SrKey, writable: true);
            previous = key?.GetValue(FrequencyValue);
            key?.SetValue(FrequencyValue, 0, RegistryValueKind.DWord);
            changed = key is not null;
        }
        catch { /* not allowed -> Windows decides */ }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"Checkpoint-Computer -Description '{Sanitize(description)}' -RestorePointType 'APPLICATION_UNINSTALL' -ErrorAction Stop\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; } // System Restore may be disabled
        finally
        {
            if (changed)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(SrKey, writable: true);
                    if (previous is null) key?.DeleteValue(FrequencyValue, throwOnMissingValue: false);
                    else key?.SetValue(FrequencyValue, previous, RegistryValueKind.DWord);
                }
                catch { /* best effort */ }
            }
        }
    }

    public async Task<string?> BackupRegistryKeyAsync(string registryPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(BackupDir);
        var fileName = $"{SafeFileName(registryPath)}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.reg";
        var fullPath = Path.Combine(BackupDir, fileName);
        var exit = await RunRegAsync($"export \"{registryPath}\" \"{fullPath}\" /y", ct);
        return exit == 0 && File.Exists(fullPath) ? fullPath : null;
    }

    public Task<IReadOnlyList<RegistryBackupInfo>> ListRegistryBackupsAsync() => Task.Run<IReadOnlyList<RegistryBackupInfo>>(() =>
    {
        var list = new List<RegistryBackupInfo>();
        foreach (var dir in new[] { BackupDir, LegacyBackupDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.reg"))
            {
                try
                {
                    var fi = new FileInfo(file);
                    list.Add(new RegistryBackupInfo(file, ReadKeyPath(file) ?? fi.Name, fi.CreationTime, fi.Length));
                }
                catch { /* unreadable */ }
            }
        }
        return list.OrderByDescending(b => b.Created).ToList();
    });

    /// <summary>The first "[HKEY_...]" line of a .reg file.</summary>
    private static string? ReadKeyPath(string file)
    {
        try
        {
            foreach (var line in File.ReadLines(file).Take(12))
            {
                var t = line.Trim().TrimStart('﻿');
                if (t.StartsWith("[") && t.EndsWith("]")) return t[1..^1];
            }
        }
        catch { /* ignore */ }
        return null;
    }

    public async Task<bool> RestoreRegistryBackupAsync(string file, CancellationToken ct = default)
        => await RunRegAsync($"import \"{file}\"", ct) == 0;

    public Task<IReadOnlyList<RestorePointInfo>> ListRestorePointsAsync() => Task.Run<IReadOnlyList<RestorePointInfo>>(() =>
    {
        var list = new List<RestorePointInfo>();
        try
        {
            using var s = new ManagementObjectSearcher(@"root\default", "SELECT SequenceNumber, Description, CreationTime, RestorePointType FROM SystemRestore");
            foreach (ManagementBaseObject mo in s.Get())
            {
                using (mo)
                {
                    var created = ManagementDateTimeConverter.ToDateTime(mo["CreationTime"] as string ?? "");
                    var type = Convert.ToInt32(mo["RestorePointType"] ?? 0) switch
                    {
                        0 => "APPLICATION_INSTALL",
                        1 => "APPLICATION_UNINSTALL",
                        10 => "DEVICE_DRIVER_INSTALL",
                        12 => "MODIFY_SETTINGS",
                        13 => "CANCELLED_OPERATION",
                        _ => "SYSTEM"
                    };
                    list.Add(new RestorePointInfo(Convert.ToInt32(mo["SequenceNumber"] ?? 0), mo["Description"] as string ?? "", created, type));
                }
            }
        }
        catch { /* System Restore off or not available */ }
        return list.OrderByDescending(r => r.Created).ToList();
    });

    private static async Task<int> RunRegAsync(string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return -1;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode;
        }
        catch (OperationCanceledException) { throw; }
        catch { return -1; }
    }

    private static string Sanitize(string s) => s.Replace("'", "''").Replace("\"", "");

    private static string SafeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Length > 80 ? s[^80..] : s;
    }
}
