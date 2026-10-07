using System.Globalization;
using System.IO;
using Microsoft.Win32;
using Vanish.Models;

namespace Vanish.Services;

// installed programs from the three uninstall keys:
//   HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall             (64-bit)
//   HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall (32-bit)
//   HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall             (per user)
public sealed class InstalledProgramsService : IInstalledProgramsService
{
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public Task<IReadOnlyList<InstalledProgram>> GetInstalledProgramsAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<InstalledProgram>>(() =>
        {
            // dedupe by name + version, the same app often shows up in both 32/64-bit views or per-machine + per-user
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<InstalledProgram>();

            foreach (var (baseKey, view, root) in EnumerateHives())
            {
                ct.ThrowIfCancellationRequested();
                using var hive = RegistryKey.OpenBaseKey(baseKey, view);
                using var uninstall = hive.OpenSubKey(UninstallSubKey);
                if (uninstall is null) continue;

                foreach (var subKeyName in uninstall.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    using var entry = uninstall.OpenSubKey(subKeyName);
                    if (entry is null) continue;

                    var program = TryReadEntry(entry, subKeyName, root);
                    if (program is null) continue;

                    var dedupeKey = $"{program.DisplayName}|{program.DisplayVersion}";
                    if (seen.Add(dedupeKey))
                        results.Add(program);
                }
            }

            return results
                .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);

    public void RemoveUninstallEntry(InstalledProgram program)
    {
        var (hive, view) = program.Root switch
        {
            RegistryRoot.LocalMachine64 => (RegistryHive.LocalMachine, RegistryView.Registry64),
            RegistryRoot.LocalMachine32 => (RegistryHive.LocalMachine, RegistryView.Registry32),
            _ => (RegistryHive.CurrentUser, RegistryView.Default)
        };

        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var uninstall = baseKey.OpenSubKey(UninstallSubKey, writable: true);
        uninstall?.DeleteSubKeyTree(program.RegistryKeyName, throwOnMissingSubKey: false);
    }

    private static IEnumerable<(RegistryHive Hive, RegistryView View, RegistryRoot Root)> EnumerateHives()
    {
        yield return (RegistryHive.LocalMachine, RegistryView.Registry64, RegistryRoot.LocalMachine64);
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32, RegistryRoot.LocalMachine32);
        yield return (RegistryHive.CurrentUser, RegistryView.Default, RegistryRoot.CurrentUser);
    }

    private static InstalledProgram? TryReadEntry(RegistryKey entry, string subKeyName, RegistryRoot root)
    {
        var displayName = entry.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(displayName))
            return null; // entries without a name are not user-visible

        // skip os updates, hotfixes, hidden system components (same as Apps & features and revo do)
        if (IsSystemComponentOrUpdate(entry))
            return null;

        var uninstallString = entry.GetValue("UninstallString") as string;
        var quietUninstall = entry.GetValue("QuietUninstallString") as string;

        // no uninstall command -> probably a leftover registration, skip
        if (string.IsNullOrWhiteSpace(uninstallString) && string.IsNullOrWhiteSpace(quietUninstall))
            return null;

        var (isMsi, productCode) = ParseMsi(uninstallString, subKeyName);

        long sizeBytes = 0;
        if (entry.GetValue("EstimatedSize") is int estKb && estKb > 0)
            sizeBytes = (long)estKb * 1024;

        var displayIcon = entry.GetValue("DisplayIcon") as string;
        var installLocation = (entry.GetValue("InstallLocation") as string)?.Trim().Trim('"');

        return new InstalledProgram
        {
            RegistryKeyName = subKeyName,
            Root = root,
            DisplayName = displayName.Trim(),
            DisplayVersion = entry.GetValue("DisplayVersion") as string,
            Publisher = entry.GetValue("Publisher") as string,
            InstallLocation = string.IsNullOrWhiteSpace(installLocation) ? null : installLocation,
            UninstallString = uninstallString,
            QuietUninstallString = quietUninstall,
            DisplayIcon = displayIcon,
            IconPath = ResolveIcon(displayIcon, installLocation, displayName, productCode),
            EstimatedSizeBytes = sizeBytes,
            InstallDate = ParseInstallDate(entry.GetValue("InstallDate") as string),
            IsMsi = isMsi,
            ProductCode = productCode,
            UrlInfoAbout = entry.GetValue("URLInfoAbout") as string,
            Comments = entry.GetValue("Comments") as string
        };
    }

    public bool StillInstalled(InstalledProgram program)
    {
        var (hive, view) = program.Root switch
        {
            RegistryRoot.LocalMachine64 => (RegistryHive.LocalMachine, RegistryView.Registry64),
            RegistryRoot.LocalMachine32 => (RegistryHive.LocalMachine, RegistryView.Registry32),
            _ => (RegistryHive.CurrentUser, RegistryView.Default)
        };
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey($@"{UninstallSubKey}\{program.RegistryKeyName}");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    // icon: DisplayIcon if it points to a real file, else the MSI product icon, else the main exe
    // in the install folder
    private static string? ResolveIcon(string? displayIcon, string? installLocation, string displayName, string? productCode)
    {
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var raw = displayIcon.Trim();
            var file = raw.Trim('"');
            int comma = file.LastIndexOf(',');
            if (comma > 2 && int.TryParse(file[(comma + 1)..].Trim(), out _)) file = file[..comma].Trim().Trim('"');
            file = Environment.ExpandEnvironmentVariables(file);
            if (File.Exists(file)) return raw.Contains(',') ? $"{file},{raw[(raw.LastIndexOf(',') + 1)..].Trim()}" : file;
        }

        if (productCode is not null && MsiProductIcon(productCode) is { } msiIcon)
            return msiIcon;

        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            try
            {
                var folder = Environment.ExpandEnvironmentVariables(installLocation);
                if (!Directory.Exists(folder)) return null;
                var exes = Directory.GetFiles(folder, "*.exe")
                    .Where(f => !IsHelperExe(Path.GetFileNameWithoutExtension(f)))
                    .ToList();
                if (exes.Count == 0) return null;
                var key = new string(displayName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                return exes
                    .OrderByDescending(f =>
                    {
                        var n = new string(Path.GetFileNameWithoutExtension(f).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                        return n.Length > 0 && (key.Contains(n) || n.Contains(key)) ? 1 : 0;
                    })
                    .ThenByDescending(f => SafeLength(f))
                    .First();
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static bool IsHelperExe(string name)
    {
        var n = name.ToLowerInvariant();
        return n.StartsWith("unins") || n.StartsWith("uninst") || n.Contains("uninstall") || n.Contains("update") ||
               n.Contains("setup") || n.Contains("helper") || n.Contains("crash") || n.Contains("report") || n == "elevate";
    }

    private static long SafeLength(string file)
    {
        try { return new FileInfo(file).Length; } catch { return 0; }
    }

    // HKCR\Installer\Products\{packed GUID}\ProductIcon, used by MSI-installed apps
    private static string? MsiProductIcon(string productCode)
    {
        try
        {
            var g = productCode.Trim('{', '}').Split('-');
            if (g.Length != 5) return null;
            static string Rev(string s) => new(s.Reverse().ToArray());
            static string Pairs(string s) => string.Concat(Enumerable.Range(0, s.Length / 2).Select(i => $"{s[i * 2 + 1]}{s[i * 2]}"));
            var packed = Rev(g[0]) + Rev(g[1]) + Rev(g[2]) + Pairs(g[3]) + Pairs(g[4]);
            using var key = Registry.ClassesRoot.OpenSubKey($@"Installer\Products\{packed}");
            if (key?.GetValue("ProductIcon") is string icon)
            {
                var file = Environment.ExpandEnvironmentVariables(icon.Trim('"'));
                int comma = file.LastIndexOf(',');
                var path = comma > 2 ? file[..comma] : file;
                if (File.Exists(path)) return file;
            }
        }
        catch { /* not an MSI product */ }
        return null;
    }

    private static bool IsSystemComponentOrUpdate(RegistryKey entry)
    {
        if (entry.GetValue("SystemComponent") is int sys && sys == 1)
            return true;

        // ReleaseType of "Security Update", "Update", "Hotfix" -> Windows/Office patches
        if (entry.GetValue("ReleaseType") is string releaseType &&
            (releaseType.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
             releaseType.Equals("Hotfix", StringComparison.OrdinalIgnoreCase)))
            return true;

        // entries with ParentKeyName/ParentDisplayName are components of another product
        if (entry.GetValue("ParentKeyName") is string pk && !string.IsNullOrWhiteSpace(pk))
            return true;

        return false;
    }

    private static (bool isMsi, string? productCode) ParseMsi(string? uninstallString, string subKeyName)
    {
        // msi: msiexec + the subkey name is the product guid, e.g.
        // "MsiExec.exe /I{90160000-008C-0000-1000-0000000FF1CE}"
        bool looksMsi = uninstallString is not null &&
                        uninstallString.Contains("msiexec", StringComparison.OrdinalIgnoreCase);

        if (looksMsi && subKeyName.StartsWith('{') && subKeyName.EndsWith('}'))
            return (true, subKeyName);

        return (looksMsi, null);
    }

    private static DateOnly? ParseInstallDate(string? raw)
    {
        // InstallDate is usually yyyyMMdd
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateOnly.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}
