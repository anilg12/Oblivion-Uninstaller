using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

// finds the leftovers of an app, carefully.
//
// matching is by product (exact name, install folder, exe names), never by publisher alone.
// vendor folders like %LOCALAPPDATA%\Google or HKCU\Software\Microsoft are only searched
// INSIDE for the product's own subfolder/key (Google\Chrome, Microsoft\Teams), the vendor
// folder itself is never offered. a protection list blocks windows/system/shared locations
// both while scanning and again right before deleting
public sealed partial class LeftoverScanService : ILeftoverScanService
{
    private readonly ISystemRestoreService _restore;

    public LeftoverScanService(ISystemRestoreService restore) => _restore = restore;

    // keys

    private static readonly HashSet<string> GenericFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "application", "applications", "bin", "bin64", "bin32", "program", "programs", "x64", "x86", "win64",
        "win32", "current", "release", "client", "common", "data", "files", "install", "installer", "setup", "update",
        "updater", "main", "core", "tools", "plugins", "resources", "res", "lib", "libs", "runtime"
    };

    private static readonly HashSet<string> GenericExeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall", "uninst", "unins000", "unins001", "uninstaller", "setup", "install", "installer", "update", "updater",
        "helper", "launcher", "crashpad_handler", "crashreporter", "crashhandler", "elevation_service", "notification_helper",
        "service", "server", "agent", "bootstrapper", "maintenancetool", "vc_redist", "dotnet", "createdump", "squirrel",
        "app", "main", "start", "run", "browser", "host", "daemon", "tray", "monitor"
    };

    // folder names that are never offered and never treated as a product
    private static readonly HashSet<string> ProtectedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "microsoft", "windows", "packages", "temp", "tmp", "programs", "common files", "windowsapps", "modifiablewindowsapps",
        "windows defender", "windows defender advanced threat protection", "windows nt", "windows mail", "windows media player",
        "windows photo viewer", "windows portable devices", "windows security", "windows sidebar", "windowspowershell",
        "internet explorer", "microsoft.net", "dotnet", "msbuild", "reference assemblies", "uninstall information",
        "package cache", "regid.1991-06.com.microsoft", "ssh", "usoshared", "usoprivate", "comms", "connecteddevicesplatform",
        "d3dscache", "placeholdertilelogofolder", "publishers", "isolatedstorage", "virtualstore", "desktop", "documents",
        "downloads", "pictures", "music", "videos", "favorites", "links", "saved games", "searches", "contacts", "onedrive",
        "start menu", "startup", "templates", "recent", "sendto", "network shortcuts", "printer shortcuts", "crashdumps",
        "intel", "nvidia", "nvidia corporation", "amd", "ati", "realtek", "oem", "system32", "syswow64", "drivers",
        "default", "public", "all users", "default user", "fonts", "assembly", "installer", "logs", "cache", "caches",
        "history", "inetcache", "inetcookies", "credentials", "crypto", "protect", "systemcertificates", "vault",
        "speech", "speech_onecore", "spp", "wer", "diagnosis", "device stage", "wfp", "ssh", ".ssh", ".gnupg", ".aws",
        ".azure", ".kube", ".docker", ".config", ".local", ".cache", "ntuser", "appdata"
    };

    // top-level registry keys under SOFTWARE that are never offered as a whole
    private static readonly HashSet<string> ProtectedRegistryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "classes", "clients", "policies", "microsoft", "windows", "wow6432node", "registeredapplications", "odbc", "intel",
        "nvidia corporation", "amd", "ati technologies", "realtek", "khronos", "partner", "oem", "defaultuserenvironment",
        "setup", "system", "hardware", "sam", "security", "cbs", "appdatalow", "wow64", "macromedia", "mozillaplugins",
        "javasoft", "python", "gnu", "cygwin"
    };

    private static string Norm(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"\b(v(ersion)?\s*)?\d+(\.\d+)+\b", RegexOptions.IgnoreCase)]
    private static partial Regex VersionNumber();

    [GeneratedRegex(@"\b(x64|x86|64-bit|32-bit|64 bit|32 bit|amd64|arm64)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ArchWords();

    // "7-Zip 23.01 (x64)" -> "7-Zip"
    private static string CleanName(string name)
    {
        var s = Bracketed().Replace(name, " ");
        s = VersionNumber().Replace(s, " ");
        s = ArchWords().Replace(s, " ");
        s = Regex.Replace(s, @"\s+", " ").Trim(' ', '-', '_', '.');
        return s.Length >= 2 ? s : name.Trim();
    }

    private static readonly string[] CompanySuffixes =
    {
        "corporation", "corp", "incorporated", "inc", "llc", "ltd", "limited", "gmbh", "ag", "ab", "sa", "srl", "bv",
        "co", "company", "software", "technologies", "technology", "systems", "studios", "studio", "games", "labs",
        "foundation", "group", "the", "as", "oy", "pty", "plc", "sas", "kk", "s.a.", "a.s."
    };

    private sealed class Keys
    {
        public required string Name { get; init; }               // "googlechrome"
        public string? Rest { get; init; }                        // "chrome"
        public HashSet<string> Vendors { get; } = new();          // "google"
        public HashSet<string> Folders { get; } = new();          // install folder name(s)
        public HashSet<string> Exes { get; } = new();             // product exe names
        public string? InstallLocation { get; init; }
        public string? UninstallKeyPath { get; init; }
    }

    private static Keys BuildKeys(AppFingerprint fp)
    {
        var clean = CleanName(fp.DisplayName);
        var words = clean.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        string? rest = null;
        if (words.Length > 1)
        {
            var r = Norm(string.Join("", words.Skip(1)));
            if (r.Length >= 3) rest = r;
        }

        var keys = new Keys
        {
            Name = Norm(clean),
            Rest = rest,
            InstallLocation = string.IsNullOrWhiteSpace(fp.InstallLocation) ? null : fp.InstallLocation.Trim().TrimEnd('\\'),
            UninstallKeyPath = fp.UninstallKeyPath
        };

        // vendor = first meaningful word(s) of the publisher + first word of a multi-word name
        if (!string.IsNullOrWhiteSpace(fp.Publisher))
        {
            var pubWords = Bracketed().Replace(fp.Publisher, " ")
                .Split(new[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !CompanySuffixes.Contains(w.ToLowerInvariant().Trim('.')))
                .ToList();
            if (pubWords.Count > 0)
            {
                var v1 = Norm(pubWords[0]);
                if (v1.Length >= 3) keys.Vendors.Add(v1);
                var vAll = Norm(string.Join("", pubWords));
                if (vAll.Length >= 3) keys.Vendors.Add(vAll);
            }
        }
        if (words.Length > 1)
        {
            var first = Norm(words[0]);
            if (first.Length >= 3) keys.Vendors.Add(first);
        }
        keys.Vendors.Remove(keys.Name); // "Spotify" by "Spotify AB": the folder IS the product

        // install folder: the last part, or its parent if that one is generic ("...\Chrome\Application")
        if (keys.InstallLocation is { } loc)
        {
            var leaf = Path.GetFileName(loc);
            var parent = Path.GetFileName(Path.GetDirectoryName(loc) ?? "");
            if (!string.IsNullOrWhiteSpace(leaf) && !GenericFolderNames.Contains(leaf) && !IsVersionLike(leaf))
                AddKey(keys.Folders, leaf);
            else if (!string.IsNullOrWhiteSpace(parent))
                AddKey(keys.Folders, parent);
        }

        foreach (var exe in fp.ExeNames)
        {
            if (GenericExeNames.Contains(exe)) continue;
            var k = Norm(exe);
            if (k.Length >= 3) keys.Exes.Add(k);
        }

        // if the word is also the folder/exe name it's the product, not a vendor
        // ("VLC media player" -> "vlc" is the app)
        keys.Vendors.ExceptWith(keys.Folders);
        keys.Vendors.ExceptWith(keys.Exes);
        return keys;
    }

    private static void AddKey(HashSet<string> set, string raw)
    {
        if (ProtectedFolderNames.Contains(raw)) return;
        var k = Norm(raw);
        if (k.Length >= 3) set.Add(k);
    }

    private static bool IsVersionLike(string s) => Regex.IsMatch(s, @"^[vV]?\d+([._-]\d+)*$");

    // fingerprint (before the uninstaller runs)

    public AppFingerprint CaptureFingerprint(InstalledProgram program)
    {
        var exes = new List<string>();
        var loc = program.InstallLocation;
        if (string.IsNullOrWhiteSpace(loc))
            loc = GuessInstallFolder(program);
        if (!string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc) && !IsProtectedPath(loc!))
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(loc!, "*.exe").Take(40))
                    exes.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { /* ignore */ }
        }
        if (program.DisplayIcon is { } icon)
        {
            var p = icon.Split(',')[0].Trim('"', ' ');
            if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) exes.Add(Path.GetFileNameWithoutExtension(p));
        }

        return new AppFingerprint
        {
            DisplayName = program.DisplayName,
            Publisher = program.Publisher,
            InstallLocation = loc,
            UninstallKeyPath = program.UninstallRegistryPath,
            ExeNames = exes.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    // when InstallLocation is empty, derive it from DisplayIcon / UninstallString
    public static string? GuessInstallFolder(InstalledProgram program)
    {
        foreach (var raw in new[] { program.DisplayIcon, program.UninstallString })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string path = raw!.Trim();
            if (path.StartsWith('"'))
            {
                int end = path.IndexOf('"', 1);
                if (end > 1) path = path[1..end];
            }
            else
            {
                int exe = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exe > 0) path = path[..(exe + 4)];
                path = path.Split(',')[0];
            }
            if (path.Contains("msiexec", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !IsProtectedPath(dir)) return dir;
            }
            catch { /* invalid path */ }
        }
        return null;
    }

    // scan

    public Task<IReadOnlyList<LeftoverItem>> ScanAsync(AppFingerprint fp,
        IReadOnlyList<string> otherInstallLocations, IProgress<string>? progress = null, CancellationToken ct = default)
        => Task.Run<IReadOnlyList<LeftoverItem>>(() =>
        {
            var keys = BuildKeys(fp);
            var others = otherInstallLocations
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim().TrimEnd('\\'))
                .Where(o => keys.InstallLocation is null || !o.Equals(keys.InstallLocation, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var found = new List<LeftoverItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (keys.Name.Length < 2) return found;

            void Add(LeftoverKind kind, string path, MatchConfidence conf, string reason, string? valueName = null)
            {
                var id = path + "|" + valueName;
                if (!seen.Add(id)) return;
                if (kind is LeftoverKind.Folder or LeftoverKind.File or LeftoverKind.Shortcut && IsProtectedPath(path)) return;
                if (kind is LeftoverKind.RegistryKey && IsProtectedRegistryPath(path)) return;
                if (kind == LeftoverKind.Folder && conf != MatchConfidence.Low && IsShared(path, keys, others))
                    conf = MatchConfidence.Low;
                long size = kind switch
                {
                    LeftoverKind.Folder => DirSize(path, ct),
                    LeftoverKind.File or LeftoverKind.Shortcut => SafeLength(path),
                    _ => 0
                };
                found.Add(new LeftoverItem
                {
                    Kind = kind,
                    Path = path,
                    ValueName = valueName,
                    SizeBytes = size,
                    Confidence = conf,
                    ReasonKey = reason
                });
            }

            // 1) install folder, if the uninstaller left it
            progress?.Report("Work_InstallFolder");
            if (keys.InstallLocation is { } loc && Directory.Exists(loc) && !IsProtectedPath(loc))
                Add(LeftoverKind.Folder, loc, MatchConfidence.High, "Reason_InstallFolder");

            // 2) program + data folders
            progress?.Report("Work_Folders");
            foreach (var root in DataRoots())
            {
                ct.ThrowIfCancellationRequested();
                ScanFolderRoot(root, keys, Add, ct);
            }

            // 3) shortcuts (start menu, desktop)
            progress?.Report("Work_Shortcuts");
            ScanShortcuts(keys, Add);

            // 4) registry
            progress?.Report("Work_Registry");
            ScanRegistry(keys, Add, ct);

            return found
                .OrderBy(i => i.Confidence)
                .ThenBy(i => i.IsRegistry)
                .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, ct);

    private static IEnumerable<string> DataRoots()
    {
        string? Env(string v) => Environment.GetEnvironmentVariable(v);
        var local = Env("LOCALAPPDATA") ?? "";
        var roots = new[]
        {
            Env("APPDATA"),
            local,
            Path.Combine(local, "Programs"),
            Env("ProgramData"),
            Env("ProgramFiles"),
            Env("ProgramFiles(x86)"),
            Env("ProgramW6432"),
            Env("USERPROFILE"),
            Path.Combine(local, "Temp"),
            Path.Combine(Env("USERPROFILE") ?? "", "AppData", "LocalLow"),
            Path.Combine(Env("USERPROFILE") ?? "", "Documents"),
        };
        return roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)).Distinct(StringComparer.OrdinalIgnoreCase)!;
    }

    private delegate void AddFn(LeftoverKind kind, string path, MatchConfidence conf, string reason, string? valueName = null);

    private static void ScanFolderRoot(string root, Keys keys, AddFn add, CancellationToken ct)
    {
        bool isProgramFiles = IsProgramFilesRoot(root);
        bool isProfile = string.Equals(root, Environment.GetEnvironmentVariable("USERPROFILE"), StringComparison.OrdinalIgnoreCase);
        bool isDocuments = root.EndsWith(@"\Documents", StringComparison.OrdinalIgnoreCase);

        foreach (var dir in SafeDirs(root))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(dir);
            var k = Norm(name.TrimStart('.'));
            if (k.Length < 2) continue;
            bool productFolder = k == keys.Name || (keys.Folders.Contains(k) && keys.Exes.Contains(k));

            // vendor folders (google, mozilla, microsoft...) are only searched inside
            if (!productFolder && keys.Vendors.Contains(k))
            {
                if (!isProfile && !isDocuments) ScanVendorFolder(dir, keys, add, ct);
                continue;
            }
            if (ProtectedFolderNames.Contains(name) || ProtectedFolderNames.Contains(name.TrimStart('.'))) continue;

            // user profile / documents can have the user's own files, never pre-selected
            if (isProfile || isDocuments)
            {
                if (productFolder) add(LeftoverKind.Folder, dir, MatchConfidence.Low, "Reason_Name");
                continue;
            }

            if (k == keys.Name)
                add(LeftoverKind.Folder, dir, MatchConfidence.High, "Reason_Name");
            else if (productFolder)
                add(LeftoverKind.Folder, dir, MatchConfidence.High, "Reason_InstallFolderName");
            else if (keys.Folders.Contains(k))
                add(LeftoverKind.Folder, dir, isProgramFiles ? MatchConfidence.High : MatchConfidence.Medium, "Reason_InstallFolderName");
            else if (keys.Exes.Contains(k))
                add(LeftoverKind.Folder, dir, MatchConfidence.Medium, "Reason_Exe");
            else if (keys.Name.Length >= 5 && k.Contains(keys.Name))
                add(LeftoverKind.Folder, dir, MatchConfidence.Low, "Reason_Contains");
        }
    }

    // "shared" = another installed program lives inside it, or it's a parent of this app's
    // install folder that also has other stuff (e.g. ...\Programs\Python with several versions).
    // shared folders are never pre-selected
    private static bool IsShared(string path, Keys keys, List<string> others)
    {
        var full = path.TrimEnd('\\');
        foreach (var o in others)
        {
            if (o.Equals(full, StringComparison.OrdinalIgnoreCase) ||
                o.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        if (keys.InstallLocation is { } loc && loc.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase))
        {
            var next = loc[(full.Length + 1)..].Split('\\')[0];
            try
            {
                if (Directory.EnumerateFileSystemEntries(full).Any(e => !Path.GetFileName(e).Equals(next, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            catch { return true; }
        }
        return false;
    }

    private static readonly HashSet<string> ProtectedInsideMicrosoft = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows", "credentials", "crypto", "protect", "systemcertificates", "vault", "windowsapps", "internet explorer",
        "windows defender", "clr_security_config", "speech", "input", "inputpersonalization", "spelling", "templates",
        "addins", "network", "event viewer", "media player", "wlansvc", "deviceSync", "diagnosis", "storage health",
        "windows sidebar", "windows mail", "office", "edge", "onedrive", "teams"
    };

    // inside a vendor folder, offer only the product's own subfolder(s)
    private static void ScanVendorFolder(string vendorDir, Keys keys, AddFn add, CancellationToken ct)
    {
        bool isMicrosoft = Norm(Path.GetFileName(vendorDir)) == "microsoft";
        foreach (var sub in SafeDirs(vendorDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(sub);
            var k = Norm(name);
            if (k.Length < 2) continue;

            bool exact = k == keys.Name || (keys.Rest is not null && k == keys.Rest);
            if (isMicrosoft && ProtectedInsideMicrosoft.Contains(name) && !exact) continue;
            if (ProtectedFolderNames.Contains(name) && !(isMicrosoft && exact)) continue;

            if (exact)
                add(LeftoverKind.Folder, sub, MatchConfidence.High, "Reason_Vendor");
            else if (keys.Folders.Contains(k) || keys.Exes.Contains(k))
                add(LeftoverKind.Folder, sub, MatchConfidence.Medium, "Reason_Vendor");
        }
    }

    private static void ScanShortcuts(Keys keys, AddFn add)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var menus = new[]
        {
            Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(progData, @"Microsoft\Windows\Start Menu\Programs"),
        };
        foreach (var menu in menus.Where(Directory.Exists))
        {
            foreach (var dir in SafeDirs(menu))
            {
                var k = Norm(Path.GetFileName(dir));
                if (k == keys.Name || (keys.Vendors.Contains(k) == false && keys.Folders.Contains(k)))
                    add(LeftoverKind.Folder, dir, MatchConfidence.High, "Reason_StartMenu");
            }
            foreach (var lnk in SafeFiles(menu, "*.lnk"))
            {
                var k = Norm(Path.GetFileNameWithoutExtension(lnk));
                if (k == keys.Name) add(LeftoverKind.Shortcut, lnk, MatchConfidence.High, "Reason_StartMenu");
            }
        }

        var desktops = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };
        foreach (var desk in desktops.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)))
        {
            foreach (var lnk in SafeFiles(desk, "*.lnk"))
            {
                var k = Norm(Path.GetFileNameWithoutExtension(lnk));
                if (k == keys.Name) add(LeftoverKind.Shortcut, lnk, MatchConfidence.High, "Reason_Desktop");
            }
        }
    }

    private static void ScanRegistry(Keys keys, AddFn add, CancellationToken ct)
    {
        var roots = new (RegistryKey Hive, string HiveName, string Path)[]
        {
            (Registry.CurrentUser, "HKCU", @"SOFTWARE"),
            (Registry.LocalMachine, "HKLM", @"SOFTWARE"),
            (Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node"),
        };

        foreach (var (hive, hiveName, basePath) in roots)
        {
            ct.ThrowIfCancellationRequested();
            using var key = SafeOpen(hive, basePath);
            if (key is null) continue;

            foreach (var sub in SafeSubKeys(key))
            {
                ct.ThrowIfCancellationRequested();
                var k = Norm(sub);
                if (k.Length < 2) continue;
                bool exact = k == keys.Name;
                if (ProtectedRegistryNames.Contains(sub) && !keys.Vendors.Contains(k))
                    continue;

                if (exact && !ProtectedRegistryNames.Contains(sub))
                {
                    add(LeftoverKind.RegistryKey, $@"{hiveName}\{basePath}\{sub}", MatchConfidence.High, "Reason_Name");
                }
                else if (keys.Vendors.Contains(k))
                {
                    // vendor key: only the product's own key inside
                    using var vendorKey = SafeOpen(key, sub);
                    if (vendorKey is null) continue;
                    bool isMicrosoft = k == "microsoft";
                    foreach (var product in SafeSubKeys(vendorKey))
                    {
                        var pk = Norm(product);
                        bool match = pk == keys.Name || (keys.Rest is not null && pk == keys.Rest);
                        if (!match) continue;
                        if (isMicrosoft && ProtectedInsideMicrosoft.Contains(product) && !match) continue;
                        if (isMicrosoft && (product.Equals("Windows", StringComparison.OrdinalIgnoreCase) || product.Equals("Windows NT", StringComparison.OrdinalIgnoreCase))) continue;
                        add(LeftoverKind.RegistryKey, $@"{hiveName}\{basePath}\{sub}\{product}", MatchConfidence.High, "Reason_Vendor");
                    }
                }
                else if (keys.Folders.Contains(k) && !ProtectedRegistryNames.Contains(sub))
                {
                    add(LeftoverKind.RegistryKey, $@"{hiveName}\{basePath}\{sub}", MatchConfidence.Medium, "Reason_InstallFolderName");
                }
            }
        }

        // orphaned uninstall entry (the uninstaller didn't clean up its own registration)
        if (keys.UninstallKeyPath is { } uk)
        {
            var (hive, sub) = SplitRegistryPath(uk.Replace("HKEY_LOCAL_MACHINE", "HKLM").Replace("HKEY_CURRENT_USER", "HKCU"));
            using var still = hive is null ? null : SafeOpen(hive, sub);
            if (still is not null)
                add(LeftoverKind.RegistryKey, uk.Replace("HKEY_LOCAL_MACHINE", "HKLM").Replace("HKEY_CURRENT_USER", "HKCU"),
                    MatchConfidence.High, "Reason_UninstallEntry");
        }

        // autostart values pointing into the removed install folder
        if (keys.InstallLocation is { } loc)
        {
            foreach (var (hive, hiveName, path) in new[]
            {
                (Registry.CurrentUser, "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                (Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                (Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
            })
            {
                using var run = SafeOpen(hive, path);
                if (run is null) continue;
                foreach (var name in run.GetValueNames())
                {
                    if (run.GetValue(name) is string cmd && cmd.Contains(loc, StringComparison.OrdinalIgnoreCase))
                        add(LeftoverKind.RegistryValue, $@"{hiveName}\{path}", MatchConfidence.High, "Reason_Autostart", name);
                }
            }
        }

        // App Paths entries for the product exe(s)
        foreach (var exe in keys.Exes)
        {
            foreach (var (hive, hiveName) in new[] { (Registry.LocalMachine, "HKLM"), (Registry.CurrentUser, "HKCU") })
            {
                using var appPaths = SafeOpen(hive, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (appPaths is null) continue;
                foreach (var sub in SafeSubKeys(appPaths))
                {
                    if (Norm(Path.GetFileNameWithoutExtension(sub)) != exe) continue;
                    using var entry = SafeOpen(appPaths, sub);
                    var target = entry?.GetValue("") as string;
                    bool pointsAway = string.IsNullOrWhiteSpace(target) || !File.Exists(target.Trim('"'));
                    if (pointsAway)
                        add(LeftoverKind.RegistryKey, $@"{hiveName}\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{sub}",
                            MatchConfidence.High, "Reason_AppPath");
                }
            }
        }
    }

    // protection

    private static bool IsProgramFilesRoot(string root)
    {
        foreach (var v in new[] { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432" })
            if (string.Equals(root.TrimEnd('\\'), Environment.GetEnvironmentVariable(v)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return true;
        var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        return local is not null && string.Equals(root.TrimEnd('\\'), Path.Combine(local, "Programs"), StringComparison.OrdinalIgnoreCase);
    }

    // paths that must never be deleted: drive roots, Windows, the Program Files / ProgramData /
    // AppData roots themselves, user library folders, and anything under Windows or a protected vendor folder
    public static bool IsProtectedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        string full;
        try { full = Path.GetFullPath(path).TrimEnd('\\'); }
        catch { return true; }
        if (full.Length <= 3) return true; // "C:" / "C:\"

        string? Env(string v) => Environment.GetEnvironmentVariable(v)?.TrimEnd('\\');
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (!string.IsNullOrEmpty(windows) && (full.Equals(windows, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase)))
            return true;

        var exactProtected = new[]
        {
            Env("ProgramFiles"), Env("ProgramFiles(x86)"), Env("ProgramW6432"), Env("CommonProgramFiles"),
            Env("CommonProgramFiles(x86)"), Env("CommonProgramW6432"), Env("ProgramData"), Env("APPDATA"), Env("LOCALAPPDATA"),
            Env("USERPROFILE"), Env("PUBLIC"), Env("ALLUSERSPROFILE"), Env("SystemDrive"),
            Env("LOCALAPPDATA") is { } l ? Path.Combine(l, "Programs") : null,
            Env("LOCALAPPDATA") is { } l2 ? Path.Combine(l2, "Temp") : null,
            Env("USERPROFILE") is { } u ? Path.Combine(u, "AppData") : null,
            Env("USERPROFILE") is { } u2 ? Path.Combine(u2, "AppData", "LocalLow") : null,
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Env("USERPROFILE") is { } u3 ? Path.Combine(u3, "Downloads") : null,
        };
        foreach (var p in exactProtected)
            if (!string.IsNullOrEmpty(p) && full.Equals(p.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return true;

        // protected name directly under a data root (e.g. %APPDATA%\Microsoft) -> whole folder is protected
        var name = Path.GetFileName(full);
        var parent = Path.GetDirectoryName(full)?.TrimEnd('\\');
        if (parent is not null && exactProtected.Any(p => !string.IsNullOrEmpty(p) && parent.Equals(p.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            && ProtectedFolderNames.Contains(name))
            return true;

        // Common Files and WindowsApps are always shared
        if (full.Contains(@"\Common Files", StringComparison.OrdinalIgnoreCase) ||
            full.Contains(@"\WindowsApps", StringComparison.OrdinalIgnoreCase) ||
            full.Contains(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase) && !full.Contains(@"\Start Menu\Programs\", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool IsProtectedRegistryPath(string path)
    {
        var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return true; // "HKLM\SOFTWARE" itself
        if (!parts[1].Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase)) return true;
        int i = 2;
        if (parts[i].Equals("WOW6432Node", StringComparison.OrdinalIgnoreCase)) i++;
        if (parts.Length <= i) return true;
        var top = parts[i];
        int depth = parts.Length - i; // 1 = SOFTWARE\Vendor, 2 = SOFTWARE\Vendor\Product
        if (top.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            if (depth < 2) return true;
            var second = parts[i + 1];
            // allowed: SOFTWARE\Microsoft\<Product> (not Windows), App Paths, Run values
            if (second.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                var joined = string.Join('\\', parts.Skip(i));
                return !(joined.StartsWith(@"Microsoft\Windows\CurrentVersion\App Paths\", StringComparison.OrdinalIgnoreCase) && depth == 5)
                       && !(joined.StartsWith(@"Microsoft\Windows\CurrentVersion\Uninstall\", StringComparison.OrdinalIgnoreCase) && depth == 5);
            }
            return ProtectedInsideMicrosoft.Contains(second) && depth == 2
                   || second.Equals("Windows NT", StringComparison.OrdinalIgnoreCase)
                   || second.Equals("Cryptography", StringComparison.OrdinalIgnoreCase)
                   || second.Equals("Policies", StringComparison.OrdinalIgnoreCase);
        }
        if (depth == 1 && ProtectedRegistryNames.Contains(top)) return true;
        if (top.Equals("Classes", StringComparison.OrdinalIgnoreCase) || top.Equals("Policies", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    // deletion

    public Task<(int Removed, long Freed, int Failed)> DeleteAsync(IReadOnlyList<LeftoverItem> items, bool useRecycleBin,
        IProgress<string>? progress = null, CancellationToken ct = default)
        => Task.Run(async () =>
        {
            int removed = 0, failed = 0;
            long freed = 0;
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    switch (item.Kind)
                    {
                        case LeftoverKind.Folder:
                        case LeftoverKind.File:
                        case LeftoverKind.Shortcut:
                            if (IsProtectedPath(item.Path)) { failed++; continue; }
                            progress?.Report(item.Path);
                            if (!Directory.Exists(item.Path) && !File.Exists(item.Path)) { removed++; continue; }
                            if (useRecycleBin)
                                RecycleBin.Delete(item.Path);
                            else if (Directory.Exists(item.Path))
                                Directory.Delete(item.Path, recursive: true);
                            else
                                File.Delete(item.Path);
                            removed++;
                            freed += item.SizeBytes;
                            break;

                        case LeftoverKind.RegistryKey:
                            if (IsProtectedRegistryPath(item.Path)) { failed++; continue; }
                            progress?.Report(item.Path);
                            await _restore.BackupRegistryKeyAsync(item.Path, ct);
                            DeleteRegistryKey(item.Path);
                            removed++;
                            break;

                        case LeftoverKind.RegistryValue:
                            progress?.Report($"{item.Path} → {item.ValueName}");
                            await _restore.BackupRegistryKeyAsync(item.Path, ct);
                            var (hive, sub) = SplitRegistryPath(item.Path);
                            using (var key = hive?.OpenSubKey(sub, writable: true))
                                key?.DeleteValue(item.ValueName ?? "", throwOnMissingValue: false);
                            removed++;
                            break;
                    }
                }
                catch
                {
                    failed++;
                }
            }
            return (removed, freed, failed);
        }, ct);

    private static void DeleteRegistryKey(string fullPath)
    {
        var (hive, sub) = SplitRegistryPath(fullPath);
        hive?.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
    }

    private static (RegistryKey? Hive, string Sub) SplitRegistryPath(string fullPath)
    {
        int slash = fullPath.IndexOf('\\');
        if (slash < 0) return (null, "");
        var hivePart = fullPath[..slash];
        var sub = fullPath[(slash + 1)..];
        RegistryKey? hive = hivePart.ToUpperInvariant() switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            _ => null
        };
        return (hive, sub);
    }

    // helpers

    private static RegistryKey? SafeOpen(RegistryKey parent, string sub)
    {
        try { return parent.OpenSubKey(sub); } catch { return null; }
    }

    private static string[] SafeSubKeys(RegistryKey key)
    {
        try { return key.GetSubKeyNames(); } catch { return Array.Empty<string>(); }
    }

    private static string[] SafeDirs(string path)
    {
        try { return Directory.GetDirectories(path); } catch { return Array.Empty<string>(); }
    }

    private static string[] SafeFiles(string path, string pattern)
    {
        try { return Directory.GetFiles(path, pattern); } catch { return Array.Empty<string>(); }
    }

    private static long SafeLength(string file)
    {
        try { return new FileInfo(file).Length; } catch { return 0; }
    }

    private static long DirSize(string path, CancellationToken ct)
    {
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", new EnumerationOptions
                     { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            {
                ct.ThrowIfCancellationRequested();
                try { total += new FileInfo(f).Length; } catch { /* locked */ }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* inaccessible */ }
        return total;
    }
}
