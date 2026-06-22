using System.Diagnostics;
using System.IO;
using System.Text;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Lists and removes packaged (Store / UWP / MSIX) apps through PowerShell's
/// Appx cmdlets. The listing script is written to a temp .ps1 file so there are
/// no nested-quote problems, and its pipe-delimited output is parsed here.
/// </summary>
public sealed class WindowsAppsService : IWindowsAppsService
{
    private const char Sep = '|';

    // Note the doubled "" inside the script: it is written verbatim to the .ps1 file.
    private const string ListScript = @"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
Get-AppxPackage | ForEach-Object {
    $loc = $_.InstallLocation
    Write-Output (""{0}|{1}|{2}|{3}|{4}|{5}"" -f $_.Name, $_.PackageFullName, $_.Publisher, $_.Version, $loc, $_.IsFramework)
}
";

    public async Task<IReadOnlyList<WindowsApp>> GetWindowsAppsAsync(CancellationToken ct = default)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"oblivion_appx_{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(scriptPath, ListScript, new UTF8Encoding(false), ct);

        try
        {
            var output = await RunPowerShellFileAsync(scriptPath, ct);
            var list = new List<WindowsApp>();

            foreach (var raw in output.Split('\n'))
            {
                ct.ThrowIfCancellationRequested();
                var line = raw.TrimEnd('\r');
                var parts = line.Split(Sep);
                if (parts.Length < 6 || string.IsNullOrWhiteSpace(parts[0])) continue;

                bool isFramework = parts[5].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                list.Add(new WindowsApp
                {
                    Name = PrettifyName(parts[0]),
                    PackageFullName = parts[1],
                    Publisher = ShortPublisher(parts[2]),
                    Version = parts[3],
                    InstallLocation = string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4],
                    IsFramework = isFramework
                });
            }

            return list
                .OrderBy(a => a.IsFramework)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* ignore */ }
        }
    }

    public async Task<bool> RemoveAsync(WindowsApp app, CancellationToken ct = default)
    {
        // Single quotes inside the double-quoted -Command argument are safe.
        var command = $"Remove-AppxPackage -Package '{app.PackageFullName.Replace("'", "''")}'";
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",
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
            return false;
        }
    }

    private static async Task<string> RunPowerShellFileAsync(string scriptPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var proc = Process.Start(psi);
        if (proc is null) return string.Empty;
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return stdout;
    }

    /// <summary>Turns "Microsoft.WindowsCalculator" into "Windows Calculator".</summary>
    private static string PrettifyName(string raw)
    {
        var name = raw;
        int dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1)
            name = name[(dot + 1)..];

        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string? ShortPublisher(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        foreach (var part in raw.Split(','))
        {
            var p = part.Trim();
            if (p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return p[3..].Trim();
        }
        return raw.Trim();
    }
}
