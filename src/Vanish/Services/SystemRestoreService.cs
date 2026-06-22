using System.Diagnostics;
using System.IO;

namespace Vanish.Services;

/// <summary>
/// Creates restore points (via the SRClient/PowerShell Checkpoint-Computer cmdlet)
/// and exports registry keys with reg.exe before Vanish deletes anything.
/// </summary>
public sealed class SystemRestoreService : ISystemRestoreService
{
    private static readonly string BackupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Vanish", "Backups");

    public async Task<bool> CreateRestorePointAsync(string description, CancellationToken ct = default)
    {
        // Checkpoint-Computer is the supported way to create a restore point.
        // RestorePointType "APPLICATION_UNINSTALL" matches our use-case.
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"Checkpoint-Computer -Description '{Sanitize(description)}' -RestorePointType 'APPLICATION_UNINSTALL'\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch
        {
            // System Restore may be disabled; treat as non-fatal.
            return false;
        }
    }

    public async Task<string?> BackupRegistryKeyAsync(string registryPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(BackupDir);
        var fileName = $"{SafeFileName(registryPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.reg";
        var fullPath = Path.Combine(BackupDir, fileName);

        var psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"export \"{registryPath}\" \"{fullPath}\" /y",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0 && File.Exists(fullPath) ? fullPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Sanitize(string s) => s.Replace("'", "''");

    private static string SafeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Length > 60 ? s[^60..] : s;
    }
}
