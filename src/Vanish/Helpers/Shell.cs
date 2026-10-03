using System.Diagnostics;
using System.IO;

namespace Vanish.Helpers;

/// <summary>
/// Opens URLs, folders and files through Explorer. Oblivion runs elevated, so links are
/// handed to the (non-elevated) Explorer instead of starting the browser as admin.
/// </summary>
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

    /// <summary>Opens Explorer with the file or folder selected (or its nearest existing parent).</summary>
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

    /// <summary>Opens regedit at the given key (regedit reopens at its LastKey value).</summary>
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
        // -m opens a new window even when regedit is already running, so LastKey applies.
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
