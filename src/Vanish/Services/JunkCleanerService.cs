using System.IO;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Scans and cleans common junk locations: user/Windows temp, thumbnail cache,
/// Windows Update download cache and browser caches.
/// </summary>
public sealed class JunkCleanerService : IJunkCleanerService
{
    public IReadOnlyList<JunkCategory> GetCategories()
    {
        string Local(string sub) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), sub);
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        return new List<JunkCategory>
        {
            new()
            {
                Name = "User temporary files",
                Description = "Files in your %TEMP% folder",
                Glyph = "",
                Paths = new[] { Path.GetTempPath() }
            },
            new()
            {
                Name = "Windows temporary files",
                Description = @"C:\Windows\Temp",
                Glyph = "",
                Paths = new[] { Path.Combine(win, "Temp") }
            },
            new()
            {
                Name = "Windows Update cache",
                Description = "Downloaded update packages",
                Glyph = "",
                Paths = new[] { Path.Combine(win, "SoftwareDistribution", "Download") }
            },
            new()
            {
                Name = "Thumbnail cache",
                Description = "Explorer thumbnail database",
                Glyph = "",
                Paths = new[] { Local(@"Microsoft\Windows\Explorer") }
            },
            new()
            {
                Name = "Browser caches",
                Description = "Edge / Chrome / Firefox cached files",
                Glyph = "",
                Paths = new[]
                {
                    Local(@"Microsoft\Edge\User Data\Default\Cache"),
                    Local(@"Google\Chrome\User Data\Default\Cache"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox\Profiles")
                }
            },
        };
    }

    public Task ScanAsync(IEnumerable<JunkCategory> categories, IProgress<string>? progress = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            foreach (var cat in categories)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Scanning {cat.Name}…");
                long size = 0;
                int count = 0;
                foreach (var root in cat.Paths)
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var file in SafeEnumerateFiles(root))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            size += new FileInfo(file).Length;
                            count++;
                        }
                        catch { /* locked / removed */ }
                    }
                }
                cat.SizeBytes = size;
                cat.FileCount = count;
            }
        }, ct);

    public Task<long> CleanAsync(IEnumerable<JunkCategory> categories, IProgress<string>? progress = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            long freed = 0;
            foreach (var cat in categories)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Cleaning {cat.Name}…");
                foreach (var root in cat.Paths)
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var file in SafeEnumerateFiles(root))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            long len = new FileInfo(file).Length;
                            File.Delete(file); // in-use files throw and are skipped
                            freed += len;
                        }
                        catch { /* skip files locked by running apps */ }
                    }
                }
            }
            progress?.Report($"Freed {Helpers.ByteSize.Humanize(freed)}.");
            return freed;
        }, ct);

    /// <summary>Enumerates files recursively, ignoring folders we can't access.</summary>
    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch { continue; }
            foreach (var s in subdirs) stack.Push(s);

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { continue; }
            foreach (var f in files) yield return f;
        }
    }
}
