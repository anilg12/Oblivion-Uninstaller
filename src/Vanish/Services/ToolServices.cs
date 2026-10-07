using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using Vanish.Helpers;
using Wpf.Ui.Controls;
using EnumerationOptions = System.IO.EnumerationOptions;

namespace Vanish.Services;

// --- large files ---

public sealed partial class LargeFile : ObservableObject
{
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }
    public DateTime Modified { get; init; }
    [ObservableProperty] private bool _isSelected;

    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string SizeText => ByteSize.Humanize(SizeBytes);
    public string DateText => Modified.ToString("dd.MM.yyyy");
}

// biggest files under a folder/drive. windows, program folders, AppData and other system
// places are skipped completely, only the user's own files
public sealed class LargeFilesService
{
    private static readonly string[] SkipNames =
    {
        "$Recycle.Bin", "System Volume Information", "Recovery", "$WinREAgent", "$SysReset", "$Windows.~BT", "$Windows.~WS",
        "Windows.old", "Config.Msi", "MSOCache", "PerfLogs", "AppData", "node_modules", ".git"
    };

    private static IEnumerable<string> SkipRoots()
    {
        foreach (var f in new[]
                 {
                     Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.CommonApplicationData
                 })
        {
            var p = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(p)) yield return p.TrimEnd('\\');
        }
    }

    public Task<IReadOnlyList<LargeFile>> ScanAsync(string root, long minBytes, IProgress<int>? progress, CancellationToken ct)
        => Task.Run<IReadOnlyList<LargeFile>>(() =>
        {
            var skipRoots = SkipRoots().ToList();
            var found = new List<LargeFile>();
            int scanned = 0;
            var pending = new Stack<string>();
            pending.Push(root);
            var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System };

            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir, "*", options))
                    {
                        var name = System.IO.Path.GetFileName(sub);
                        if (SkipNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                        if (skipRoots.Any(r => r.Equals(sub.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) continue;
                        pending.Push(sub);
                    }
                    foreach (var f in Directory.EnumerateFiles(dir, "*", options))
                    {
                        if (++scanned % 2000 == 0) progress?.Report(scanned);
                        try
                        {
                            var fi = new FileInfo(f);
                            if (fi.Length >= minBytes)
                                found.Add(new LargeFile { Path = f, SizeBytes = fi.Length, Modified = fi.LastWriteTime });
                        }
                        catch { /* gone */ }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { /* access denied */ }
            }
            progress?.Report(scanned);
            return found.OrderByDescending(f => f.SizeBytes).Take(300).ToList();
        }, ct);

    // moves files to the Recycle Bin. returns (moved, bytes)
    public Task<(int Moved, long Bytes)> RecycleAsync(IReadOnlyList<LargeFile> files) => Task.Run(() =>
    {
        int moved = 0;
        long bytes = 0;
        foreach (var f in files)
        {
            if (LeftoverScanService.IsProtectedPath(f.Path)) continue;
            try
            {
                RecycleBin.Delete(f.Path);
                moved++;
                bytes += f.SizeBytes;
            }
            catch { /* in use */ }
        }
        return (moved, bytes);
    });
}

// --- shredder ---

// overwrite with random data (1 or 3 passes), rename to random names, delete,
// so undelete tools can't get them back
public sealed class ShredderService
{
    public Task<(int Shredded, int Failed)> ShredAsync(IReadOnlyList<string> paths, int passes, IProgress<double>? progress, CancellationToken ct)
        => Task.Run(() =>
        {
            var files = new List<string>();
            var folders = new List<string>();
            foreach (var p in paths)
            {
                if (LeftoverScanService.IsProtectedPath(p)) continue;
                if (File.Exists(p)) files.Add(p);
                else if (Directory.Exists(p))
                {
                    folders.Add(p);
                    try
                    {
                        files.AddRange(Directory.EnumerateFiles(p, "*", new EnumerationOptions
                        { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }));
                    }
                    catch { /* ignore */ }
                }
            }

            int done = 0, failed = 0;
            var buffer = new byte[1 << 20];
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    ShredFile(files[i], passes, buffer, ct);
                    done++;
                }
                catch (OperationCanceledException) { throw; }
                catch { failed++; }
                progress?.Report((i + 1) / (double)Math.Max(1, files.Count));
            }

            foreach (var folder in folders)
            {
                try
                {
                    foreach (var d in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                        try { Directory.Delete(d); } catch { /* not empty */ }
                    Directory.Delete(folder);
                }
                catch { /* not empty -> some files failed */ }
            }
            return (done, failed);
        }, ct);

    private static void ShredFile(string path, int passes, byte[] buffer, CancellationToken ct)
    {
        var fi = new FileInfo(path);
        if (fi.IsReadOnly) fi.IsReadOnly = false;
        long length = fi.Length;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.WriteThrough))
        {
            for (int pass = 0; pass < passes; pass++)
            {
                fs.Position = 0;
                long written = 0;
                while (written < length)
                {
                    ct.ThrowIfCancellationRequested();
                    int count = (int)Math.Min(buffer.Length, length - written);
                    if (passes == 3 && pass == 0) Array.Clear(buffer, 0, count);
                    else if (passes == 3 && pass == 1) Array.Fill(buffer, (byte)0xFF, 0, count);
                    else RandomNumberGenerator.Fill(buffer.AsSpan(0, count));
                    fs.Write(buffer, 0, count);
                    written += count;
                }
                fs.Flush(true);
            }
            fs.SetLength(0);
        }
        // hide the original name before deleting
        var dir = Path.GetDirectoryName(path)!;
        var renamed = Path.Combine(dir, Path.GetRandomFileName());
        File.Move(path, renamed);
        File.Delete(renamed);
    }
}

// --- history & privacy ---

public sealed partial class PrivacyItem : ObservableObject
{
    public required string Id { get; init; }
    public required string TitleKey { get; init; }
    public required string DetailKey { get; init; }
    public required SymbolRegular Symbol { get; init; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _count = -1;

    public string Title => Loc.I[TitleKey];
    public string Detail => Loc.I[DetailKey];
    public string CountText => Count < 0 ? "" : Count == 0 ? Loc.I["Priv_Clean"] : string.Format(Loc.I["Junk_ItemsFmt"], Count);

    partial void OnCountChanged(int value) => OnPropertyChanged(nameof(CountText));
}

// clears Windows usage traces. nothing is selected by default
public sealed class HistoryCleanerService
{
    private const string Explorer = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";
    private static string Recent => Environment.GetFolderPath(Environment.SpecialFolder.Recent);

    public IReadOnlyList<PrivacyItem> CreateItems() => new List<PrivacyItem>
    {
        new() { Id = "recent", TitleKey = "Priv_Recent", DetailKey = "Priv_Recent_D", Symbol = SymbolRegular.DocumentBulletList24 },
        new() { Id = "jumplists", TitleKey = "Priv_JumpLists", DetailKey = "Priv_JumpLists_D", Symbol = SymbolRegular.PanelLeft24 },
        new() { Id = "runmru", TitleKey = "Priv_Run", DetailKey = "Priv_Run_D", Symbol = SymbolRegular.Play24 },
        new() { Id = "typedpaths", TitleKey = "Priv_Typed", DetailKey = "Priv_Typed_D", Symbol = SymbolRegular.FolderOpen24 },
        new() { Id = "search", TitleKey = "Priv_Search", DetailKey = "Priv_Search_D", Symbol = SymbolRegular.Search24 },
        new() { Id = "clipboard", TitleKey = "Priv_Clipboard", DetailKey = "Priv_Clipboard_D", Symbol = SymbolRegular.Clipboard24 },
        new() { Id = "dns", TitleKey = "Priv_Dns", DetailKey = "Priv_Dns_D", Symbol = SymbolRegular.Globe24 },
    };

    public Task CountAsync(IReadOnlyList<PrivacyItem> items) => Task.Run(() =>
    {
        foreach (var item in items)
        {
            int count = item.Id switch
            {
                "recent" => SafeCount(() => Directory.GetFiles(Recent, "*.lnk").Length),
                "jumplists" => SafeCount(() => Directory.GetFiles(Path.Combine(Recent, "AutomaticDestinations")).Length)
                               + SafeCount(() => Directory.GetFiles(Path.Combine(Recent, "CustomDestinations")).Length),
                "runmru" => ValueCount(Explorer + @"\RunMRU"),
                "typedpaths" => ValueCount(Explorer + @"\TypedPaths"),
                "search" => ValueCount(Explorer + @"\WordWheelQuery"),
                _ => -1
            };
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => item.Count = count);
        }
    });

    private static int SafeCount(Func<int> f)
    {
        try { return f(); } catch { return 0; }
    }

    private static int ValueCount(string sub)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(sub);
            return key?.GetValueNames().Count(n => !string.Equals(n, "MRUList", StringComparison.OrdinalIgnoreCase) &&
                                                   !string.Equals(n, "MRUListEx", StringComparison.OrdinalIgnoreCase) && n.Length > 0) ?? 0;
        }
        catch { return 0; }
    }

    // clears the given items (clipboard must be cleared by the caller on the UI thread)
    public Task<int> CleanAsync(IReadOnlyList<PrivacyItem> items) => Task.Run(() =>
    {
        int cleared = 0;
        foreach (var item in items)
        {
            switch (item.Id)
            {
                case "recent":
                    cleared += DeleteFiles(Recent, "*.lnk");
                    break;
                case "jumplists":
                    cleared += DeleteFiles(Path.Combine(Recent, "AutomaticDestinations"), "*");
                    cleared += DeleteFiles(Path.Combine(Recent, "CustomDestinations"), "*");
                    break;
                case "runmru":
                    cleared += ClearValues(Explorer + @"\RunMRU");
                    break;
                case "typedpaths":
                    cleared += ClearValues(Explorer + @"\TypedPaths");
                    break;
                case "search":
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(Explorer, writable: true);
                        if (key?.OpenSubKey("WordWheelQuery") is { } w) { cleared += w.ValueCount; w.Dispose(); }
                        key?.DeleteSubKeyTree("WordWheelQuery", throwOnMissingSubKey: false);
                    }
                    catch { /* ignore */ }
                    break;
                case "dns":
                    try { if (Native.DnsFlushResolverCache() != 0) cleared++; } catch { /* ignore */ }
                    break;
            }
        }
        try { Native.SHChangeNotify(Native.SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero); } catch { /* ignore */ }
        return cleared;
    });

    private static int DeleteFiles(string dir, string pattern)
    {
        int n = 0;
        if (!Directory.Exists(dir)) return 0;
        foreach (var f in Directory.EnumerateFiles(dir, pattern))
            try { File.Delete(f); n++; } catch { /* in use */ }
        return n;
    }

    private static int ClearValues(string sub)
    {
        int n = 0;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(sub, writable: true);
            if (key is null) return 0;
            foreach (var name in key.GetValueNames())
            {
                try { key.DeleteValue(name); if (name.Length > 0 && !name.StartsWith("MRUList", StringComparison.OrdinalIgnoreCase)) n++; }
                catch { /* ignore */ }
            }
        }
        catch { /* ignore */ }
        return n;
    }
}

// --- evidence remover ---

public sealed record DriveChoice(string Root, string Label, long Free, long Total, bool IsSsd)
{
    public string Title => string.IsNullOrWhiteSpace(Label) ? Root : $"{Label} ({Root.TrimEnd('\\')})";
    public string FreeText => ByteSize.Humanize(Free);
    public string Detail => $"{ByteSize.Humanize(Free)} / {ByteSize.Humanize(Total)}";
}

// wipes free space with windows' own cipher /w (zeros, ones, random) so files deleted
// earlier can't be recovered
public sealed class EvidenceService
{
    public Task<IReadOnlyList<DriveChoice>> GetDrivesAsync() => Task.Run<IReadOnlyList<DriveChoice>>(() =>
    {
        var ssdLetters = SsdLetters();
        var list = new List<DriveChoice>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                list.Add(new DriveChoice(d.Name, d.VolumeLabel, d.AvailableFreeSpace, d.TotalSize, ssdLetters.Contains(d.Name[0])));
            }
            catch { /* not ready */ }
        }
        return list;
    });

    // drive letters that sit on an SSD (MSFT_PhysicalDisk.MediaType = 4)
    private static HashSet<char> SsdLetters()
    {
        var result = new HashSet<char>();
        try
        {
            var ssdDisks = new HashSet<string>();
            using (var s = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk"))
                foreach (ManagementBaseObject mo in s.Get())
                    using (mo)
                        if (Convert.ToInt32(mo["MediaType"] ?? 0) == 4) ssdDisks.Add(mo["DeviceId"]?.ToString() ?? "");
            if (ssdDisks.Count == 0) return result;
            using var p = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DriveLetter, DiskNumber FROM MSFT_Partition");
            foreach (ManagementBaseObject mo in p.Get())
                using (mo)
                {
                    var letter = mo["DriveLetter"] is char c ? c : (mo["DriveLetter"] is ushort u ? (char)u : '\0');
                    if (letter != '\0' && ssdDisks.Contains(mo["DiskNumber"]?.ToString() ?? "")) result.Add(char.ToUpperInvariant(letter));
                }
        }
        catch { /* storage WMI not available */ }
        return result;
    }

    // runs cipher /w, reports the pass (1..3). cancel stops it and removes its temp folder
    public async Task<bool> WipeFreeSpaceAsync(string root, IProgress<int> pass, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cipher.exe",
            Arguments = $"/w:{root.TrimEnd('\\')}\\",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        if (proc is null) return false;
        proc.OutputDataReceived += (_, e) =>
        {
            var line = e.Data ?? "";
            if (line.Contains("0x00")) pass.Report(1);
            else if (line.Contains("0xFF", StringComparison.OrdinalIgnoreCase)) pass.Report(2);
            else if (line.Contains("Random", StringComparison.OrdinalIgnoreCase) || line.Contains("rastgele", StringComparison.OrdinalIgnoreCase)) pass.Report(3);
        };
        proc.BeginOutputReadLine();
        try
        {
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* exited */ }
            try
            {
                var leftover = Path.Combine(root, "EFSTMPWP");
                if (Directory.Exists(leftover)) Directory.Delete(leftover, recursive: true);
            }
            catch { /* best effort */ }
            throw;
        }
    }
}
