using System.Diagnostics;
using System.IO;

namespace Vanish.Helpers;

// open urls/folders/files through explorer. we run elevated, so links go through the
// non-elevated explorer instead of starting the browser as admin
public static class Shell
{
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url.Trim();
        Start("explorer.exe", $"\"{url}\"");
    }

    public static void OpenFolder(string folder)
    {
        if (Directory.Exists(folder)) Start("explorer.exe", $"\"{folder}\"");
    }

    // opens Explorer with the file or folder selected (or its nearest existing parent)
    public static void Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = path.TrimEnd('\\', '*');
        if (File.Exists(path) || Directory.Exists(path))
        {
            Start("explorer.exe", $"/select,\"{path}\"");
            return;
        }
        var dir = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) dir = Path.GetDirectoryName(dir);
        if (!string.IsNullOrEmpty(dir)) OpenFolder(dir);
    }

    // opens regedit at the given key (regedit reopens at its LastKey value)
    public static void OpenRegistry(string keyPath)
    {
        var full = keyPath
            .Replace(@"HKLM\", @"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase)
            .Replace(@"HKCU\", @"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase)
            .Replace(@"HKCR\", @"HKEY_CLASSES_ROOT\", StringComparison.OrdinalIgnoreCase);
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Applets\Regedit");
            key?.SetValue("LastKey", @"Computer\" + full);
        }
        catch { /* best effort */ }
        // -m = new window even if regedit is already open, so LastKey is used
        Start("regedit.exe", "-m");
    }

    public static bool Start(string file, string args = "")
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = file, Arguments = args, UseShellExecute = true })?.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
