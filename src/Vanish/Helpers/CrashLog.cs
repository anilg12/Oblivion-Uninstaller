using System.IO;
using System.Text;

namespace Vanish.Helpers;

// writes unexpected errors to %LOCALAPPDATA%\Oblivion\crash.log.
// in self-test mode it also writes progress markers
public static class CrashLog
{
    private static readonly object Gate = new();

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oblivion", "crash.log");

    // self-test progress file (set by the snapshot runner), or null
    public static string? ProgressFile { get; set; }

    public static void Write(string where, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where} (Oblivion {typeof(CrashLog).Assembly.GetName().Version}){Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch { /* never throw from the crash handler */ }
        Mark($"ERROR in {where}: {ex.GetType().Name}: {ex.Message}");
    }

    public static void Mark(string message)
    {
        var file = ProgressFile;
        if (file is null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch { /* ignore */ }
    }
}
