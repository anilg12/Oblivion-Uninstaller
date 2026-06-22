using System.IO;
using System.Text.Json;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Discovers and removes browser extensions. Chromium browsers (Edge, Chrome,
/// Brave) keep each extension under
/// <c>User Data\&lt;Profile&gt;\Extensions\&lt;id&gt;\&lt;version&gt;\manifest.json</c>;
/// Firefox stores <c>.xpi</c> files per profile.
/// </summary>
public sealed class BrowserExtensionsService
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public Task<IReadOnlyList<BrowserExtension>> GetExtensionsAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<BrowserExtension>>(() =>
        {
            var list = new List<BrowserExtension>();

            var chromium = new (string Browser, string UserData)[]
            {
                ("Microsoft Edge", Path.Combine(Local, @"Microsoft\Edge\User Data")),
                ("Google Chrome",  Path.Combine(Local, @"Google\Chrome\User Data")),
                ("Brave",          Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data")),
            };

            foreach (var (browser, userData) in chromium)
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(userData)) continue;
                ScanChromium(browser, userData, list, ct);
            }

            ScanFirefox(list, ct);

            return list
                .OrderBy(e => e.Browser)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);

    private static void ScanChromium(string browser, string userData, List<BrowserExtension> sink, CancellationToken ct)
    {
        // Profiles: "Default", "Profile 1", "Profile 2", ...
        IEnumerable<string> profiles;
        try { profiles = Directory.EnumerateDirectories(userData); }
        catch { return; }

        foreach (var profile in profiles)
        {
            ct.ThrowIfCancellationRequested();
            var extRoot = Path.Combine(profile, "Extensions");
            if (!Directory.Exists(extRoot)) continue;

            foreach (var extDir in SafeDirs(extRoot))
            {
                var id = Path.GetFileName(extDir);
                // skip the Chromium temp folder
                if (id.Equals("Temp", StringComparison.OrdinalIgnoreCase)) continue;

                // pick the newest version sub-folder
                var versionDir = SafeDirs(extDir).OrderByDescending(d => d).FirstOrDefault();
                if (versionDir is null) continue;

                var manifest = Path.Combine(versionDir, "manifest.json");
                if (!File.Exists(manifest)) continue;

                var (name, version) = ReadChromiumManifest(manifest, versionDir, id);
                sink.Add(new BrowserExtension
                {
                    Name = name,
                    Browser = browser,
                    Id = id,
                    Version = version,
                    Path = extDir
                });
            }
        }
    }

    private static (string Name, string? Version) ReadChromiumManifest(string manifest, string versionDir, string id)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = doc.RootElement;
            string? version = root.TryGetProperty("version", out var v) ? v.GetString() : null;
            string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? id : id;

            // Localized names look like "__MSG_appName__" -> resolve from _locales.
            if (name.StartsWith("__MSG_", StringComparison.Ordinal))
                name = ResolveLocalizedName(root, versionDir, name) ?? id;

            return (name, version);
        }
        catch
        {
            return (id, null);
        }
    }

    private static string? ResolveLocalizedName(JsonElement manifestRoot, string versionDir, string token)
    {
        var key = token.Trim('_').Replace("MSG_", "", StringComparison.Ordinal);
        var defaultLocale = manifestRoot.TryGetProperty("default_locale", out var dl) ? dl.GetString() : "en";

        foreach (var locale in new[] { defaultLocale, "en", "en_US" })
        {
            if (string.IsNullOrWhiteSpace(locale)) continue;
            var messages = Path.Combine(versionDir, "_locales", locale, "messages.json");
            if (!File.Exists(messages)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(messages));
                if (doc.RootElement.TryGetProperty(key, out var entry) &&
                    entry.TryGetProperty("message", out var msg))
                    return msg.GetString();
            }
            catch { /* try next */ }
        }
        return null;
    }

    private static void ScanFirefox(List<BrowserExtension> sink, CancellationToken ct)
    {
        var profiles = Path.Combine(Roaming, @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(profiles)) return;

        foreach (var profile in SafeDirs(profiles))
        {
            ct.ThrowIfCancellationRequested();
            var extDir = Path.Combine(profile, "extensions");
            if (!Directory.Exists(extDir)) continue;

            foreach (var xpi in SafeFiles(extDir, "*.xpi"))
            {
                sink.Add(new BrowserExtension
                {
                    Name = Path.GetFileNameWithoutExtension(xpi),
                    Browser = "Firefox",
                    Id = Path.GetFileNameWithoutExtension(xpi),
                    Version = null,
                    Path = xpi
                });
            }
        }
    }

    public Task RemoveAsync(BrowserExtension extension, CancellationToken ct = default)
        => Task.Run(() =>
        {
            if (Directory.Exists(extension.Path) || File.Exists(extension.Path))
                RecycleBin.Delete(extension.Path);
        }, ct);

    private static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.EnumerateDirectories(path); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> SafeFiles(string path, string pattern)
    {
        try { return Directory.EnumerateFiles(path, pattern); }
        catch { return Array.Empty<string>(); }
    }
}
