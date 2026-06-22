using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Vanish.Helpers;

/// <summary>
/// Extracts an application icon (from a "path,index" DisplayIcon value or an EXE)
/// and converts it to a WPF <see cref="BitmapSource"/>. Results are cached.
/// </summary>
public static class IconExtractor
{
    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Resolves an icon for a DisplayIcon registry value such as
    /// "C:\Program Files\App\app.exe,0". Returns null when nothing can be loaded.
    /// </summary>
    public static BitmapSource? FromDisplayIcon(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
            return null;

        lock (Gate)
        {
            if (Cache.TryGetValue(displayIcon, out var cached))
                return cached;

            var result = Resolve(displayIcon);
            Cache[displayIcon] = result;
            return result;
        }
    }

    private static BitmapSource? Resolve(string displayIcon)
    {
        var (path, index) = SplitPathAndIndex(displayIcon);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        // .ico files can be loaded directly.
        if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var icon = new Icon(path);
                return ToBitmapSource(icon);
            }
            catch
            {
                return null;
            }
        }

        // EXE/DLL: pull the icon at the requested index.
        var large = new IntPtr[1];
        try
        {
            uint extracted = ExtractIconEx(path, index, large, null, 1);
            if (extracted == 0 || large[0] == IntPtr.Zero)
                return null;

            using var icon = Icon.FromHandle(large[0]);
            return ToBitmapSource(icon);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (large[0] != IntPtr.Zero)
                DestroyIcon(large[0]);
        }
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

    private static BitmapSource ToBitmapSource(Icon icon)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze(); // make it cross-thread usable
        return source;
    }
}
