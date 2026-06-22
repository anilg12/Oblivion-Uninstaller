using System.IO;
using Microsoft.Win32;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Heuristic leftover scanner. Builds search tokens from the program's name,
/// publisher and install location, then walks well-known filesystem roots and
/// registry trees looking for matching folders, files and keys.
/// </summary>
public sealed class LeftoverScanService : ILeftoverScanService
{
    // Folders we never recurse into or offer for deletion — too risky / shared.
    private static readonly string[] ForbiddenRoots =
    {
        @"C:\Windows",
        @"C:\Program Files\Common Files",
        @"C:\Program Files (x86)\Common Files"
    };

    public Task<IReadOnlyList<LeftoverItem>> ScanAsync(
        InstalledProgram program,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run<IReadOnlyList<LeftoverItem>>(() =>
        {
            var tokens = BuildTokens(program);
            var found = new List<LeftoverItem>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) The install location itself, if it still exists.
            if (!string.IsNullOrWhiteSpace(program.InstallLocation) &&
                Directory.Exists(program.InstallLocation) &&
                !IsForbidden(program.InstallLocation!))
            {
                progress?.Report("Checking install location…");
                AddFolder(found, seenPaths, program.InstallLocation!, MatchConfidence.High,
                    "Original install folder");
            }

            // 2) Common application data roots.
            progress?.Report("Scanning application data folders…");
            foreach (var root in DataRoots())
            {
                ct.ThrowIfCancellationRequested();
                ScanFolderRoot(root, tokens, found, seenPaths, ct);
            }

            // 3) Registry: Software trees + uninstall remnants.
            progress?.Report("Scanning registry…");
            ScanRegistry(tokens, program, found, ct);

            progress?.Report($"Found {found.Count} leftover item(s).");
            return found
                .OrderByDescending(i => i.Confidence)
                .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, ct);

    // ---- token building -----------------------------------------------------

    private static IReadOnlyList<string> BuildTokens(InstalledProgram p)
    {
        var tokens = new List<string>();

        void Add(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            s = s.Trim();
            // Drop common noise words so "Foo Bar Inc." doesn't match everything.
            foreach (var noise in new[] { "Inc.", "Inc", "LLC", "Ltd.", "Ltd", "Corporation", "Corp.", "GmbH", "Software", "®", "™", ",", "(R)" })
                s = s.Replace(noise, "", StringComparison.OrdinalIgnoreCase);
            s = s.Trim();
            if (s.Length >= 3 && !tokens.Contains(s, StringComparer.OrdinalIgnoreCase))
                tokens.Add(s);
        }

        Add(p.DisplayName);
        Add(p.Publisher);
        // First word of the display name catches e.g. "Spotify" from "Spotify (x64)".
        var firstWord = p.DisplayName.Split(' ', '-', '(')[0];
        Add(firstWord);

        return tokens;
    }

    // ---- filesystem ---------------------------------------------------------

    private static IEnumerable<string> DataRoots()
    {
        string? Env(string v) => Environment.GetEnvironmentVariable(v);
        var roots = new[]
        {
            Env("APPDATA"),
            Env("LOCALAPPDATA"),
            Env("ProgramData"),
            Env("ProgramFiles"),
            Env("ProgramFiles(x86)"),
            Path.Combine(Env("LOCALAPPDATA") ?? "", "Programs"),
            Path.Combine(Env("LOCALAPPDATA") ?? "", "Temp")
        };
        return roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r))!;
    }

    private static void ScanFolderRoot(
        string root,
        IReadOnlyList<string> tokens,
        List<LeftoverItem> found,
        HashSet<string> seen,
        CancellationToken ct)
    {
        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(root);
        }
        catch
        {
            return; // access denied / not found
        }

        foreach (var dir in dirs)
        {
            ct.ThrowIfCancellationRequested();
            if (IsForbidden(dir)) continue;

            var name = Path.GetFileName(dir);
            var match = MatchToken(name, tokens);
            if (match is not null)
            {
                AddFolder(found, seen, dir, match.Value.Confidence, match.Value.Reason);
            }
        }
    }

    private static void AddFolder(
        List<LeftoverItem> found,
        HashSet<string> seen,
        string path,
        MatchConfidence confidence,
        string reason)
    {
        if (!seen.Add(path)) return;
        long size = TryGetDirectorySize(path);
        found.Add(new LeftoverItem
        {
            Kind = LeftoverKind.Folder,
            Path = path,
            SizeBytes = size,
            Confidence = confidence,
            Reason = reason,
            IsSelected = confidence == MatchConfidence.High
        });
    }

    private static long TryGetDirectorySize(string path)
    {
        try
        {
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { /* skip locked */ }
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    // ---- registry -----------------------------------------------------------

    private static void ScanRegistry(
        IReadOnlyList<string> tokens,
        InstalledProgram program,
        List<LeftoverItem> found,
        CancellationToken ct)
    {
        var roots = new (RegistryKey Hive, string Path)[]
        {
            (Registry.CurrentUser, @"SOFTWARE"),
            (Registry.LocalMachine, @"SOFTWARE"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node"),
        };

        foreach (var (hive, basePath) in roots)
        {
            ct.ThrowIfCancellationRequested();
            using var key = hive.OpenSubKey(basePath);
            if (key is null) continue;

            string[] subNames;
            try { subNames = key.GetSubKeyNames(); }
            catch { continue; }

            foreach (var sub in subNames)
            {
                ct.ThrowIfCancellationRequested();
                var match = MatchToken(sub, tokens);
                if (match is null) continue;

                // Publisher-named vendor keys often contain only this product.
                found.Add(new LeftoverItem
                {
                    Kind = LeftoverKind.RegistryKey,
                    Path = $@"{HiveName(hive)}\{basePath}\{sub}",
                    Confidence = match.Value.Confidence,
                    Reason = match.Value.Reason,
                    IsSelected = match.Value.Confidence == MatchConfidence.High
                });
            }
        }
    }

    private static string HiveName(RegistryKey hive) =>
        hive.Name.StartsWith("HKEY_CURRENT_USER", StringComparison.Ordinal) ? "HKCU" : "HKLM";

    // ---- matching -----------------------------------------------------------

    private static (MatchConfidence Confidence, string Reason)? MatchToken(string candidate, IReadOnlyList<string> tokens)
    {
        foreach (var token in tokens)
        {
            if (candidate.Equals(token, StringComparison.OrdinalIgnoreCase))
                return (MatchConfidence.High, $"Name matches \"{token}\"");
        }
        foreach (var token in tokens)
        {
            if (candidate.Contains(token, StringComparison.OrdinalIgnoreCase))
                return (MatchConfidence.Medium, $"Contains \"{token}\"");
        }
        return null;
    }

    private static bool IsForbidden(string path) =>
        ForbiddenRoots.Any(f => path.StartsWith(f, StringComparison.OrdinalIgnoreCase) &&
                                path.TrimEnd('\\').Length <= f.Length + 1) ||
        ForbiddenRoots.Any(f => string.Equals(path.TrimEnd('\\'), f, StringComparison.OrdinalIgnoreCase));

    // ---- deletion -----------------------------------------------------------

    public Task<int> DeleteAsync(
        IEnumerable<LeftoverItem> items,
        bool useRecycleBin,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            int removed = 0;
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    switch (item.Kind)
                    {
                        case LeftoverKind.Folder:
                        case LeftoverKind.File:
                            progress?.Report($"Deleting {item.Path}");
                            if (useRecycleBin)
                                RecycleBin.Delete(item.Path);
                            else if (item.Kind == LeftoverKind.Folder)
                                Directory.Delete(item.Path, recursive: true);
                            else
                                File.Delete(item.Path);
                            removed++;
                            break;

                        case LeftoverKind.RegistryKey:
                            progress?.Report($"Removing key {item.Path}");
                            DeleteRegistryKey(item.Path);
                            removed++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Skipped {item.Path}: {ex.Message}");
                }
            }
            progress?.Report($"Removed {removed} item(s).");
            return removed;
        }, ct);

    private static void DeleteRegistryKey(string fullPath)
    {
        // fullPath like "HKLM\SOFTWARE\Vendor\Product"
        int slash = fullPath.IndexOf('\\');
        if (slash < 0) return;
        var hivePart = fullPath[..slash];
        var subPath = fullPath[(slash + 1)..];

        var hive = hivePart.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
            ? Registry.CurrentUser
            : Registry.LocalMachine;

        hive.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
    }
}
