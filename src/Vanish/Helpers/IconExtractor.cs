using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Vanish.Helpers;

/// <summary>
/// Extracts application icons ("path,index" DisplayIcon values, .exe/.dll/.ico files or
/// image files) into frozen <see cref="BitmapSource"/>s. Extraction always runs on a
/// background thread with limited parallelism; results are cached for the session.
/// </summary>
public static class IconExtractor
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Task<BitmapSource?>> Pending = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim Gate = new(3);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0x0, SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    /// <summary>File types whose icon lives inside the file itself.</summary>
    private static readonly HashSet<string> OwnIcon = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll", ".ico", ".cpl", ".scr", ".ocx", ".mui" };

    public static bool TryGetCached(string key, out BitmapSource? source) => Cache.TryGetValue(key, out source);

    /// <summary>Loads (or returns the cached) icon for <paramref name="key"/> off the UI thread.</summary>
    public static Task<BitmapSource?> LoadAsync(string key)
    {
        if (Cache.TryGetValue(key, out var cached)) return Task.FromResult(cached);
        return Pending.GetOrAdd(key, k => Task.Run(async () =>
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var result = Resolve(k);
                Cache[k] = result;
                return result;
            }
            catch
            {
                Cache[k] = null;
                return null;
            }
            finally
            {
                Gate.Release();
                Pending.TryRemove(k, out _);
            }
        }));
    }

    private static BitmapSource? Resolve(string raw)
    {
        var (path, index) = SplitPathAndIndex(raw);
        path = Environment.ExpandEnvironmentVariables(path);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        bool smallImage = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" && new FileInfo(path).Length < 24L * 1024 * 1024;
        if (!smallImage && !OwnIcon.Contains(ext))
            return FileTypeIcon(ext);
        if (smallImage)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 64;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        if (ext == ".ico")
        {
            try
            {
                using var icon = new Icon(path, 48, 48);
                return ToBitmapSource(icon.Handle);
            }
            catch { /* fall through to shell extraction */ }
        }

        // Crisp 48 px icon via the shell; ExtractIconEx (32 px) as a fallback.
        try
        {
            if (SHDefExtractIcon(path, index, 0, out var large, out var small, (16u << 16) | 48u) == 0 && large != IntPtr.Zero)
            {
                try { return ToBitmapSource(large); }
                finally
                {
                    DestroyIcon(large);
                    if (small != IntPtr.Zero) DestroyIcon(small);
                }
            }
        }
        catch { /* ignore */ }

        var handles = new IntPtr[1];
        try
        {
            if (ExtractIconEx(path, index, handles, null, 1) > 0 && handles[0] != IntPtr.Zero)
                return ToBitmapSource(handles[0]);
        }
        catch { /* ignore */ }
        finally
        {
            if (handles[0] != IntPtr.Zero) DestroyIcon(handles[0]);
        }
        return null;
    }

    /// <summary>The icon Explorer shows for a file type (videos, archives, documents…), looked up by extension only.</summary>
    private static BitmapSource? FileTypeIcon(string ext)
    {
        var key = "*" + ext;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        BitmapSource? result = null;
        var info = new SHFILEINFO();
        try
        {
            var r = SHGetFileInfo(string.IsNullOrEmpty(ext) ? "file" : "file" + ext, FILE_ATTRIBUTE_NORMAL, ref info,
                (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
            if (r != IntPtr.Zero && info.hIcon != IntPtr.Zero) result = ToBitmapSource(info.hIcon);
        }
        catch { /* ignore */ }
        finally
        {
            if (info.hIcon != IntPtr.Zero) DestroyIcon(info.hIcon);
        }
        Cache[key] = result;
        return result;
    }

    private static (string Path, int Index) SplitPathAndIndex(string raw)
    {
        raw = raw.Trim().Trim('"');
        int comma = raw.LastIndexOf(',');
        // Only treat the trailing token as an index if it parses as an integer,
        // so paths containing commas are not mangled.
        if (comma > 0 && int.TryParse(raw[(comma + 1)..].Trim(), out var index))
            return (raw[..comma].Trim().Trim('"'), index);
        return (raw, 0);
    }

    private static BitmapSource ToBitmapSource(IntPtr hIcon)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        source.Freeze(); // cross-thread usable
        return source;
    }
}
