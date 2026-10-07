using System.IO;
using System.Text.Json;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

// browser extensions. chromium browsers (edge, chrome, brave, vivaldi, opera, opera gx) keep them in
// <Profile>\Extensions\<id>\<version>\manifest.json, firefox lists add-ons with real names in
// each profile's extensions.json
public sealed class BrowserExtensionsService
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string Pf => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    private static string Pf86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    public Task<IReadOnlyList<BrowserExtension>> GetExtensionsAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<BrowserExtension>>(() =>
        {
            var list = new List<BrowserExtension>();
            var chromium = new (string Browser, string UserData, bool SingleProfile, string? Exe)[]
            {
                ("Microsoft Edge", Path.Combine(Local, @"Microsoft\Edge\User Data"), false,
                    FirstExisting(Path.Combine(Pf86, @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(Pf, @"Microsoft\Edge\Application\msedge.exe"))),
                ("Google Chrome", Path.Combine(Local, @"Google\Chrome\User Data"), false,
                    FirstExisting(Path.Combine(Pf, @"Google\Chrome\Application\chrome.exe"), Path.Combine(Pf86, @"Google\Chrome\Application\chrome.exe"), Path.Combine(Local, @"Google\Chrome\Application\chrome.exe"))),
                ("Brave", Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data"), false,
                    FirstExisting(Path.Combine(Pf, @"BraveSoftware\Brave-Browser\Application\brave.exe"), Path.Combine(Local, @"BraveSoftware\Brave-Browser\Application\brave.exe"))),
                ("Vivaldi", Path.Combine(Local, @"Vivaldi\User Data"), false,
                    FirstExisting(Path.Combine(Local, @"Vivaldi\Application\vivaldi.exe"), Path.Combine(Pf, @"Vivaldi\Application\vivaldi.exe"))),
                ("Opera", Path.Combine(Roaming, @"Opera Software\Opera Stable"), true,
                    FirstExisting(Path.Combine(Local, @"Programs\Opera\opera.exe"))),
                ("Opera GX", Path.Combine(Roaming, @"Opera Software\Opera GX Stable"), true,
                    FirstExisting(Path.Combine(Local, @"Programs\Opera GX\opera.exe"))),
            };

            foreach (var (browser, userData, single, exe) in chromium)
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(userData)) continue;
                if (single) ScanChromiumProfile(browser, userData, null, exe, list);
                else
                    foreach (var profile in SafeDirs(userData))
                    {
                        var name = Path.GetFileName(profile);
                        if (name == "Default" || name.StartsWith("Profile ", StringComparison.Ordinal))
                            ScanChromiumProfile(browser, profile, name, exe, list);
                    }
            }

            ScanFirefox(list, ct);

            return list
                .OrderBy(e => e.Browser)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);

    private static string? FirstExisting(params string[] paths) => paths.FirstOrDefault(File.Exists);

    private static void ScanChromiumProfile(string browser, string profileDir, string? profileName, string? exe, List<BrowserExtension> sink)
    {
        var extRoot = Path.Combine(profileDir, "Extensions");
        if (!Directory.Exists(extRoot)) return;

        foreach (var extDir in SafeDirs(extRoot))
        {
            var id = Path.GetFileName(extDir);
            if (id.Length < 20 || id.Equals("Temp", StringComparison.OrdinalIgnoreCase)) continue;

            var versionDir = SafeDirs(extDir).OrderByDescending(d => d, StringComparer.Ordinal).FirstOrDefault();
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
                Profile = profileName,
                BrowserIcon = exe,
                Path = extDir
            });
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
        var key = token.Replace("__MSG_", "", StringComparison.Ordinal).TrimEnd('_');
        var defaultLocale = manifestRoot.TryGetProperty("default_locale", out var dl) ? dl.GetString() : "en";
        foreach (var locale in new[] { "tr", defaultLocale, "en", "en_US" })
        {
            if (string.IsNullOrWhiteSpace(locale)) continue;
            var messages = Path.Combine(versionDir, "_locales", locale, "messages.json");
            if (!File.Exists(messages)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(messages));
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.TryGetProperty("message", out var msg)) return msg.GetString();
                }
            }
            catch { /* try next */ }
        }
        return null;
    }

    private static void ScanFirefox(List<BrowserExtension> sink, CancellationToken ct)
    {
        var profiles = Path.Combine(Roaming, @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(profiles)) return;
        var exe = FirstExisting(Path.Combine(Pf, @"Mozilla Firefox\firefox.exe"), Path.Combine(Pf86, @"Mozilla Firefox\firefox.exe"));

        foreach (var profile in SafeDirs(profiles))
        {
            ct.ThrowIfCancellationRequested();
            var json = Path.Combine(profile, "extensions.json");
            var names = new Dictionary<string, (string Name, string? Version)>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(json));
                    if (doc.RootElement.TryGetProperty("addons", out var addons))
                    {
                        foreach (var a in addons.EnumerateArray())
                        {
                            var id = a.TryGetProperty("id", out var i) ? i.GetString() : null;
                            if (id is null) continue;
                            var type = a.TryGetProperty("type", out var t) ? t.GetString() : null;
                            var location = a.TryGetProperty("location", out var l) ? l.GetString() : null;
                            if (type != "extension" || location != "app-profile") continue;
                            string name = id;
                            if (a.TryGetProperty("defaultLocale", out var dl) && dl.TryGetProperty("name", out var nm))
                                name = nm.GetString() ?? id;
                            var ver = a.TryGetProperty("version", out var v) ? v.GetString() : null;
                            names[id] = (name, ver);
                        }
                    }
                }
                catch { /* unreadable */ }
            }

            var extDir = Path.Combine(profile, "extensions");
            if (!Directory.Exists(extDir)) continue;
            foreach (var xpi in SafeFiles(extDir, "*.xpi"))
            {
                var id = Path.GetFileNameWithoutExtension(xpi);
                var meta = names.TryGetValue(id, out var m) ? m : (Name: id, Version: (string?)null);
                sink.Add(new BrowserExtension
                {
                    Name = meta.Name,
                    Browser = "Firefox",
                    Id = id,
                    Version = meta.Version,
                    Profile = Path.GetFileName(profile).Split('.').LastOrDefault(),
                    BrowserIcon = exe,
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
        try { return Directory.GetDirectories(path); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> SafeFiles(string path, string pattern)
    {
        try { return Directory.GetFiles(path, pattern); }
        catch { return Array.Empty<string>(); }
    }
}
