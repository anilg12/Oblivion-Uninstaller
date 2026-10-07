using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Vanish.Models;

namespace Vanish.Services;

// list/remove store (UWP/MSIX) apps with the Appx cmdlets. the list script goes into a temp .ps1
// to avoid nested quote problems, its pipe separated output is parsed here
public sealed class WindowsAppsService : IWindowsAppsService
{
    private const char Sep = '|';

    // the doubled "" is on purpose, the script is written to the .ps1 as is
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
            // parsing + logo lookup hit the disk, keep it off the ui thread
            return await Task.Run<IReadOnlyList<WindowsApp>>(() => ParsePackages(output, ct), ct);
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* ignore */ }
        }
    }

    private static IReadOnlyList<WindowsApp> ParsePackages(string output, CancellationToken ct)
    {
        var list = new List<WindowsApp>();
        foreach (var raw in output.Split('\n'))
        {
            ct.ThrowIfCancellationRequested();
            var line = raw.TrimEnd('\r');
            var parts = line.Split(Sep);
            if (parts.Length < 6 || string.IsNullOrWhiteSpace(parts[0])) continue;

            bool isFramework = parts[5].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
            var installLocation = string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4];

            list.Add(new WindowsApp
            {
                Name = PrettifyName(parts[0]),
                PackageFullName = parts[1],
                Publisher = ShortPublisher(parts[2]),
                Version = parts[3],
                InstallLocation = installLocation,
                LogoPath = ResolveLogo(installLocation),
                IsFramework = isFramework
            });
        }

        return list
            .OrderBy(a => a.IsFramework)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // finds the tile logo from AppxManifest.xml and the actual png on disk
    // (manifest paths usually don't match the scaled file names)
    private static string? ResolveLogo(string? installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            return null;

        var manifest = Path.Combine(installLocation, "AppxManifest.xml");
        if (!File.Exists(manifest)) return null;

        string text;
        try { text = File.ReadAllText(manifest); } catch { return null; }

        var rel =
            MatchAttr(text, "Square44x44Logo") ??
            MatchAttr(text, "Square150x150Logo") ??
            MatchElement(text, "Logo");
        if (string.IsNullOrWhiteSpace(rel)) return null;

        return FindActualFile(installLocation, rel!);
    }

    private static string? MatchAttr(string xml, string attr)
    {
        var m = Regex.Match(xml, attr + @"\s*=\s*""([^""]+)""");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? MatchElement(string xml, string element)
    {
        var m = Regex.Match(xml, "<(?:[\\w]+:)?" + element + ">([^<]+)</");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? FindActualFile(string installLocation, string relativePath)
    {
        try
        {
            var full = Path.Combine(installLocation, relativePath.Replace('/', '\\'));
            if (File.Exists(full)) return full;

            var dir = Path.GetDirectoryName(full);
            var nameNoExt = Path.GetFileNameWithoutExtension(full);
            var ext = Path.GetExtension(full);
            if (dir is null || !Directory.Exists(dir) || string.IsNullOrEmpty(nameNoExt))
                return null;

            var candidates = Directory.GetFiles(dir, nameNoExt + "*" + ext);
            if (candidates.Length == 0) return null;

            // prefer a mid/high res scaled asset
            return candidates
                .OrderByDescending(f => f.Contains("scale-200", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(f => f.Contains("targetsize-44", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(f => f.Contains("scale-100", StringComparison.OrdinalIgnoreCase))
                .First();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RemoveAsync(WindowsApp app, CancellationToken ct = default)
    {
        // single quotes are fine inside the double quoted -Command
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

    // turns "Microsoft.WindowsCalculator" into "Windows Calculator"
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
