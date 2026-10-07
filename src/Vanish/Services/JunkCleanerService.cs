using System.IO;
using System.Windows.Media;
using Vanish.Helpers;
using Vanish.Models;
using Wpf.Ui.Controls;

namespace Vanish.Services;

// regenerable junk: temp, caches, crash dumps, update downloads, browser caches, old installers,
// recycle bin. every category lists its exact contents and only ticked items get removed.
// personal files go to the recycle bin
public sealed class JunkCleanerService : IJunkCleanerService
{
    private const int MaxListed = 150;

    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public IReadOnlyList<JunkCategory> CreateCategories() => new List<JunkCategory>
    {
        new() { Id = "usertemp", TitleKey = "Junk_UserTemp", DetailKey = "Junk_UserTemp_D", Symbol = SymbolRegular.Clock24,
                From = C(0x3A8DFF), To = C(0x6F5BFF), Mode = JunkMode.Delete },
        new() { Id = "wintemp", TitleKey = "Junk_WinTemp", DetailKey = "Junk_WinTemp_D", Symbol = SymbolRegular.Window24,
                From = C(0x18C29C), To = C(0x2E8BFF), Mode = JunkMode.Delete },
        new() { Id = "browser", TitleKey = "Junk_Browser", DetailKey = "Junk_Browser_D", NoteKey = "Junk_Browser_Note",
                Symbol = SymbolRegular.Globe24, From = C(0xFF9F45), To = C(0xFF6B6B), Mode = JunkMode.Delete },
        new() { Id = "crash", TitleKey = "Junk_Crash", DetailKey = "Junk_Crash_D", Symbol = SymbolRegular.DocumentError24,
                From = C(0xEC4899), To = C(0x8B5CF6), Mode = JunkMode.Delete },
        new() { Id = "update", TitleKey = "Junk_Update", DetailKey = "Junk_Update_D", NoteKey = "Junk_Update_Note",
                Symbol = SymbolRegular.ArrowDownload24, From = C(0x6E5BFF), To = C(0xB45BFF), Mode = JunkMode.Delete },
        new() { Id = "thumbs", TitleKey = "Junk_Thumbs", DetailKey = "Junk_Thumbs_D", NoteKey = "Junk_Thumbs_Note",
                Symbol = SymbolRegular.Image24, From = C(0x14B8A6), To = C(0x22C55E), Mode = JunkMode.Delete },
        new() { Id = "installers", TitleKey = "Junk_Installers", DetailKey = "Junk_Installers_D", NoteKey = "Junk_Installers_Note",
                Symbol = SymbolRegular.BoxMultiple24, From = C(0xF5A524), To = C(0xFF7A45), Mode = JunkMode.Recycle },
        new() { Id = "recyclebin", TitleKey = "Junk_RecycleBin", DetailKey = "Junk_RecycleBin_D", NoteKey = "Junk_RecycleBin_Note",
                Symbol = SymbolRegular.BinFull24, From = C(0x22C55E), To = C(0x14B8A6), Mode = JunkMode.EmptyRecycleBin },
    };

    private static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    // scans one category in the background and returns its entries (largest first)
    public Task<(IReadOnlyList<JunkEntry> Items, long Bytes, int Count)> ScanAsync(JunkCategory category, CancellationToken ct = default)
        => Task.Run(() => Scan(category.Id, ct), ct);

    private (IReadOnlyList<JunkEntry>, long, int) Scan(string id, CancellationToken ct)
    {
        var entries = new List<JunkEntry>();
        switch (id)
        {
            case "usertemp":
                AddChildren(entries, Path.GetTempPath(), ct, skip: p => p.EndsWith(@"\.net", StringComparison.OrdinalIgnoreCase));
                break;
            case "wintemp":
                AddChildren(entries, Path.Combine(WinDir, "Temp"), ct);
                break;
            case "crash":
                AddChildren(entries, Path.Combine(Local, "CrashDumps"), ct);
                AddChildren(entries, Path.Combine(WinDir, "Minidump"), ct);
                AddChildren(entries, Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive"), ct);
                AddChildren(entries, Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue"), ct);
                AddChildren(entries, Path.Combine(Local, @"Microsoft\Windows\WER\ReportArchive"), ct);
                AddChildren(entries, Path.Combine(Local, @"Microsoft\Windows\WER\ReportQueue"), ct);
                var memDump = Path.Combine(WinDir, "MEMORY.DMP");
                if (File.Exists(memDump)) entries.Add(FileEntry(memDump));
                break;
            case "update":
                AddChildren(entries, Path.Combine(WinDir, @"SoftwareDistribution\Download"), ct);
                break;
            case "thumbs":
                var explorer = Path.Combine(Local, @"Microsoft\Windows\Explorer");
                if (Directory.Exists(explorer))
                {
                    foreach (var f in SafeFiles(explorer))
                    {
                        var n = Path.GetFileName(f);
                        if (n.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase) ||
                            n.StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase))
                            entries.Add(FileEntry(f));
                    }
                }
                break;
            case "browser":
                ScanBrowsers(entries, ct);
                break;
            case "installers":
                var downloads = Path.Combine(UserProfile, "Downloads");
                if (Directory.Exists(downloads))
                {
                    foreach (var f in SafeFiles(downloads))
                        if (LooksLikeInstaller(f)) entries.Add(FileEntry(f));
                }
                break;
            case "recyclebin":
                var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.SHQUERYRBINFO>() };
                if (Native.SHQueryRecycleBin(null, ref info) == 0 && info.i64NumItems > 0)
                {
                    entries.Add(new JunkEntry
                    {
                        Name = Loc.I["Junk_RecycleBin"],
                        Location = string.Format(Loc.I["Junk_ItemsFmt"], info.i64NumItems),
                        Paths = new[] { "::recyclebin" },
                        SizeBytes = info.i64Size,
                        Count = (int)Math.Min(int.MaxValue, info.i64NumItems),
                        IsFolder = true
                    });
                }
                break;
        }

        entries = entries.Where(e => e.SizeBytes > 0 || e.Count > 0).OrderByDescending(e => e.SizeBytes).ToList();
        long total = entries.Sum(e => e.SizeBytes);
        int count = entries.Sum(e => e.Count);

        if (entries.Count > MaxListed)
        {
            var rest = entries.Skip(MaxListed - 1).ToList();
            entries = entries.Take(MaxListed - 1).ToList();
            entries.Add(new JunkEntry
            {
                Name = string.Format(Loc.I["Junk_GroupFmt"], rest.Count),
                Location = Loc.I["Junk_GroupDetail"],
                Paths = rest.SelectMany(r => r.Paths).ToList(),
                SizeBytes = rest.Sum(r => r.SizeBytes),
                Count = rest.Sum(r => r.Count),
                IsGroup = true
            });
        }
        return (entries, total, count);
    }

    private static bool LooksLikeInstaller(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".msi" or ".msix" or ".msixbundle" or ".appx" or ".appxbundle") return true;
        if (ext != ".exe") return false;
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return name.Contains("setup") || name.Contains("install") || name.Contains("kurulum");
    }

    private static void ScanBrowsers(List<JunkEntry> sink, CancellationToken ct)
    {
        var chromium = new (string Name, string UserData)[]
        {
            ("Microsoft Edge", Path.Combine(Local, @"Microsoft\Edge\User Data")),
            ("Google Chrome", Path.Combine(Local, @"Google\Chrome\User Data")),
            ("Brave", Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data")),
            ("Vivaldi", Path.Combine(Local, @"Vivaldi\User Data")),
            ("Chromium", Path.Combine(Local, @"Chromium\User Data")),
        };
        foreach (var (name, userData) in chromium)
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(userData)) continue;
            foreach (var profile in SafeDirs(userData))
            {
                var pname = Path.GetFileName(profile);
                if (!(pname == "Default" || pname.StartsWith("Profile ", StringComparison.Ordinal) || pname == "Guest Profile")) continue;
                foreach (var cache in new[] { "Cache", "Code Cache", "GPUCache" })
                    AddFolder(sink, Path.Combine(profile, cache), $"{name} · {pname} · {cache}", ct);
            }
        }

        // opera's cache is in LocalAppData even though the profile is in Roaming
        foreach (var opera in new[] { "Opera Stable", "Opera GX Stable" })
        {
            var root = Path.Combine(Local, "Opera Software", opera);
            AddFolder(sink, Path.Combine(root, "Cache"), $"{opera.Replace(" Stable", "")} · Cache", ct);
            AddFolder(sink, Path.Combine(root, "Code Cache"), $"{opera.Replace(" Stable", "")} · Code Cache", ct);
        }

        // firefox: ONLY the cache folders in LocalAppData, never the profile
        // (bookmarks, passwords, history are in the roaming profile)
        var ffProfiles = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(ffProfiles))
        {
            foreach (var profile in SafeDirs(ffProfiles))
            {
                var pname = Path.GetFileName(profile);
                AddFolder(sink, Path.Combine(profile, "cache2"), $"Firefox · {pname} · cache2", ct);
                AddFolder(sink, Path.Combine(profile, "startupCache"), $"Firefox · {pname} · startupCache", ct);
                AddFolder(sink, Path.Combine(profile, "thumbnails"), $"Firefox · {pname} · thumbnails", ct);
            }
        }
        _ = Roaming; // never touch the roaming profile
    }

    private static void AddFolder(List<JunkEntry> sink, string folder, string label, CancellationToken ct)
    {
        if (!Directory.Exists(folder)) return;
        var (size, count, newest) = Measure(folder, ct);
        if (count == 0) return;
        sink.Add(new JunkEntry
        {
            Name = label,
            Location = folder,
            Paths = new[] { folder + @"\*" }, // contents only, keep the folder
            SizeBytes = size,
            Count = count,
            Modified = newest,
            IsFolder = true
        });
    }

    private static void AddChildren(List<JunkEntry> sink, string root, CancellationToken ct, Func<string, bool>? skip = null)
    {
        if (!Directory.Exists(root)) return;
        foreach (var dir in SafeDirs(root))
        {
            ct.ThrowIfCancellationRequested();
            if (skip?.Invoke(dir) == true) continue;
            var (size, count, newest) = Measure(dir, ct);
            sink.Add(new JunkEntry
            {
                Name = Path.GetFileName(dir),
                Location = dir,
                Paths = new[] { dir },
                SizeBytes = size,
                Count = Math.Max(1, count),
                Modified = newest ?? SafeTime(() => Directory.GetLastWriteTime(dir)),
                IsFolder = true
            });
        }
        foreach (var file in SafeFiles(root))
        {
            ct.ThrowIfCancellationRequested();
            if (skip?.Invoke(file) == true) continue;
            sink.Add(FileEntry(file));
        }
    }

    private static JunkEntry FileEntry(string file)
    {
        long size = 0;
        DateTime? modified = null;
        try
        {
            var fi = new FileInfo(file);
            size = fi.Length;
            modified = fi.LastWriteTime;
        }
        catch { /* inaccessible */ }
        return new JunkEntry
        {
            Name = Path.GetFileName(file),
            Location = file,
            Paths = new[] { file },
            SizeBytes = size,
            Count = 1,
            Modified = modified
        };
    }

    private static DateTime? SafeTime(Func<DateTime> f)
    {
        try { return f(); } catch { return null; }
    }

    private static (long Size, int Count, DateTime? Newest) Measure(string dir, CancellationToken ct)
    {
        long size = 0;
        int count = 0;
        DateTime? newest = null;
        foreach (var f in EnumerateFilesDeep(dir))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fi = new FileInfo(f);
                size += fi.Length;
                count++;
                if (newest is null || fi.LastWriteTime > newest) newest = fi.LastWriteTime;
            }
            catch { /* locked / gone */ }
        }
        return (size, count, newest);
    }

    // removes the given entries. returns (items removed, bytes freed, items skipped)
    public Task<(int Removed, long Freed, int Skipped)> CleanAsync(IReadOnlyList<JunkEntry> entries, IProgress<double>? progress = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            int removed = 0, skipped = 0;
            long freed = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var entry = entries[i];
                var mode = entry.Category?.Mode ?? JunkMode.Delete;
                foreach (var raw in entry.Paths)
                {
                    if (raw == "::recyclebin")
                    {
                        int hr = Native.SHEmptyRecycleBin(IntPtr.Zero, null,
                            Native.SHERB_NOCONFIRMATION | Native.SHERB_NOPROGRESSUI | Native.SHERB_NOSOUND);
                        if (hr == 0) { removed += entry.Count; freed += entry.SizeBytes; } else skipped++;
                        continue;
                    }

                    bool contentsOnly = raw.EndsWith(@"\*", StringComparison.Ordinal);
                    var path = contentsOnly ? raw[..^2] : raw;

                    if (mode == JunkMode.Recycle)
                    {
                        try
                        {
                            long len = File.Exists(path) ? new FileInfo(path).Length : 0;
                            RecycleBin.Delete(path);
                            removed++;
                            freed += len;
                        }
                        catch { skipped++; }
                        continue;
                    }

                    var (r, f, s) = DeletePath(path, contentsOnly, ct);
                    removed += r;
                    freed += f;
                    skipped += s;
                }
                progress?.Report((i + 1) / (double)entries.Count);
            }
            return (removed, freed, skipped);
        }, ct);

    // deletes a file, a folder, or (contentsOnly) the contents of a folder. locked files are skipped
    private static (int Removed, long Freed, int Skipped) DeletePath(string path, bool contentsOnly, CancellationToken ct)
    {
        int removed = 0, skipped = 0;
        long freed = 0;
        if (File.Exists(path))
        {
            try
            {
                var fi = new FileInfo(path);
                long len = fi.Length;
                if (fi.IsReadOnly) fi.IsReadOnly = false;
                fi.Delete();
                return (1, len, 0);
            }
            catch { return (0, 0, 1); }
        }
        if (!Directory.Exists(path)) return (0, 0, 0);

        foreach (var f in EnumerateFilesDeep(path))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fi = new FileInfo(f);
                long len = fi.Length;
                if (fi.IsReadOnly) fi.IsReadOnly = false;
                fi.Delete();
                removed++;
                freed += len;
            }
            catch { skipped++; }
        }

        // remove empty dirs, deepest first
        var dirs = new List<string>();
        try { dirs.AddRange(Directory.EnumerateDirectories(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })); }
        catch { /* ignore */ }
        foreach (var d in dirs.OrderByDescending(d => d.Length))
        {
            try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { /* in use */ }
        }
        if (!contentsOnly)
        {
            try { if (!Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path); } catch { /* in use */ }
        }
        return (removed, freed, skipped);
    }

    private static IEnumerable<string> EnumerateFilesDeep(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            });
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> SafeFiles(string path)
    {
        try { return Directory.GetFiles(path); }
        catch { return Array.Empty<string>(); }
    }
}
