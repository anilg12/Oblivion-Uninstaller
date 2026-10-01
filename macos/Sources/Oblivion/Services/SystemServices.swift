import AppKit
import Foundation

// MARK: - Startup manager (LaunchAgents / LaunchDaemons)

enum LaunchItemsService {
    static func scan() -> [LaunchItem] {
        let sources: [(URL, LaunchItem.Scope)] = [
            (AppPaths.library.appendingPathComponent("LaunchAgents"), .userAgent),
            (URL(fileURLWithPath: "/Library/LaunchAgents"), .globalAgent),
            (URL(fileURLWithPath: "/Library/LaunchDaemons"), .daemon),
        ]
        var items: [LaunchItem] = []
        for (dir, scope) in sources {
            guard let files = try? FileManager.default.contentsOfDirectory(atPath: dir.path) else { continue }
            for file in files where file.hasSuffix(".plist") {
                let url = dir.appendingPathComponent(file)
                let dict = (try? Data(contentsOf: url)).flatMap {
                    try? PropertyListSerialization.propertyList(from: $0, options: [], format: nil) as? [String: Any]
                } ?? [:]
                let label = dict["Label"] as? String ?? (file as NSString).deletingPathExtension
                var program = dict["Program"] as? String ?? ""
                if program.isEmpty, let args = dict["ProgramArguments"] as? [String] {
                    program = args.joined(separator: " ")
                }
                items.append(LaunchItem(path: url.path, label: label, program: program, scope: scope,
                                        runAtLoad: dict["RunAtLoad"] as? Bool ?? false,
                                        disabled: dict["Disabled"] as? Bool ?? false))
            }
        }
        return items.sorted { $0.label.localizedCaseInsensitiveCompare($1.label) == .orderedAscending }
    }

    static func remove(_ item: LaunchItem) -> Bool {
        if item.scope == .userAgent {
            Shell.run("/bin/launchctl", ["bootout", "gui/\(getuid())", item.path])
        }
        return Trash.move([URL(fileURLWithPath: item.path)]) == 1
    }
}

// MARK: - Junk cleaner

enum JunkService {
    static func categories() -> [JunkCategory] {
        let lib = AppPaths.library
        let home = AppPaths.home
        return [
            JunkCategory(id: "caches", titleKey: "junk.caches", detailKey: "junk.caches.d", symbol: "internaldrive",
                         colors: [0x3A8DFF, 0x6F5BFF], roots: [lib.appendingPathComponent("Caches")], mode: .contents),
            JunkCategory(id: "logs", titleKey: "junk.logs", detailKey: "junk.logs.d", symbol: "doc.text",
                         colors: [0x18C29C, 0x2E8BFF], roots: [lib.appendingPathComponent("Logs")], mode: .contents),
            JunkCategory(id: "dev", titleKey: "junk.dev", detailKey: "junk.dev.d", symbol: "hammer",
                         colors: [0xFF9F45, 0xFF6B6B],
                         roots: [lib.appendingPathComponent("Developer/Xcode/DerivedData"),
                                 lib.appendingPathComponent("Developer/Xcode/iOS DeviceSupport"),
                                 lib.appendingPathComponent("Developer/CoreSimulator/Caches")],
                         mode: .contents),
            JunkCategory(id: "installers", titleKey: "junk.installers", detailKey: "junk.installers.d",
                         symbol: "shippingbox", colors: [0xEC4899, 0x8B5CF6],
                         roots: [home.appendingPathComponent("Downloads")], mode: .installers),
            JunkCategory(id: "trash", titleKey: "junk.trash", detailKey: "junk.trash.d", symbol: "trash",
                         colors: [0x22C55E, 0x14B8A6], roots: [home.appendingPathComponent(".Trash")], mode: .trash),
        ]
    }

    static func targets(for category: JunkCategory) -> [URL] {
        let fm = FileManager.default
        var out: [URL] = []
        for root in category.roots {
            guard let kids = try? fm.contentsOfDirectory(at: root, includingPropertiesForKeys: nil, options: []) else { continue }
            switch category.mode {
            case .installers:
                out += kids.filter { ["dmg", "pkg", "mpkg", "iso", "xip"].contains($0.pathExtension.lowercased()) }
            case .contents:
                out += kids.filter {
                    let name = $0.lastPathComponent
                    return !name.hasPrefix(".") && !name.hasPrefix("com.apple.") && !name.contains("com.anilgul.oblivion")
                }
            case .trash:
                out += kids
            }
        }
        return out
    }

    static func measure(_ category: inout JunkCategory) {
        let items = targets(for: category)
        category.size = items.reduce(Int64(0)) { $0 + DiskSize.of($1) }
        category.count = items.count
        category.scanned = true
    }

    /// Returns bytes freed.
    static func clean(_ category: JunkCategory) -> Int64 {
        let items = targets(for: category)
        switch category.mode {
        case .installers:
            var freed: Int64 = 0
            for url in items {
                let size = DiskSize.of(url)
                if Trash.move([url]) == 1 { freed += size }
            }
            return freed
        case .trash:
            let size = category.size
            let result = Shell.osascript(["tell application \"Finder\" to empty trash"])
            return result.status == 0 ? size : 0
        case .contents:
            var freed: Int64 = 0
            for url in items {
                let size = DiskSize.of(url)
                if (try? FileManager.default.removeItem(at: url)) != nil { freed += size }
            }
            return freed
        }
    }
}

// MARK: - Browser extensions

enum BrowserExtensionsService {
    private static let chromium: [(name: String, folder: String, bundleID: String)] = [
        ("Google Chrome", "Google/Chrome", "com.google.Chrome"),
        ("Microsoft Edge", "Microsoft Edge", "com.microsoft.edgemac"),
        ("Brave", "BraveSoftware/Brave-Browser", "com.brave.Browser"),
        ("Arc", "Arc/User Data", "company.thebrowser.Browser"),
        ("Vivaldi", "Vivaldi", "com.vivaldi.Vivaldi"),
        ("Opera", "com.operasoftware.Opera", "com.operasoftware.Opera"),
        ("Chromium", "Chromium", "org.chromium.Chromium"),
    ]

    static func scan() -> [BrowserExtension] {
        var out = chromiumExtensions()
        out += firefoxExtensions()
        out += safariExtensions()
        return out.sorted {
            ($0.browser, $0.name.lowercased()) < ($1.browser, $1.name.lowercased())
        }
    }

    private static func chromiumExtensions() -> [BrowserExtension] {
        let fm = FileManager.default
        let support = AppPaths.library.appendingPathComponent("Application Support")
        var out: [BrowserExtension] = []
        for browser in chromium {
            let base = support.appendingPathComponent(browser.folder)
            guard fm.fileExists(atPath: base.path) else { continue }
            var profiles: [URL] = [base]
            if let kids = try? fm.contentsOfDirectory(at: base, includingPropertiesForKeys: nil) {
                profiles += kids
            }
            for profile in profiles {
                let extRoot = profile.appendingPathComponent("Extensions")
                guard let ids = try? fm.contentsOfDirectory(atPath: extRoot.path) else { continue }
                for id in ids where id.count >= 20 && !id.hasPrefix(".") {
                    let extDir = extRoot.appendingPathComponent(id)
                    guard let versions = try? fm.contentsOfDirectory(atPath: extDir.path),
                          let latest = versions.filter({ !$0.hasPrefix(".") }).sorted().last else { continue }
                    let info = manifestInfo(extDir.appendingPathComponent(latest))
                    let name = info?.name ?? id
                    out.append(BrowserExtension(
                        name: name.isEmpty ? id : name,
                        browser: browser.name,
                        browserBundleID: browser.bundleID,
                        profile: profile == base ? "Default" : profile.lastPathComponent,
                        extID: id,
                        version: info?.version ?? latest,
                        path: extDir.path,
                        removable: true))
                }
            }
        }
        return out
    }

    private static func manifestInfo(_ dir: URL) -> (name: String, version: String)? {
        guard let data = try? Data(contentsOf: dir.appendingPathComponent("manifest.json")),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        var name = json["name"] as? String ?? ""
        let version = json["version"] as? String ?? ""
        if name.hasPrefix("__MSG_") {
            let key = name.replacingOccurrences(of: "__MSG_", with: "").replacingOccurrences(of: "__", with: "")
            let defaultLocale = json["default_locale"] as? String ?? "en"
            for locale in [defaultLocale, "en", "en_US", "tr"] {
                let file = dir.appendingPathComponent("_locales/\(locale)/messages.json")
                guard let d = try? Data(contentsOf: file),
                      let messages = try? JSONSerialization.jsonObject(with: d) as? [String: Any] else { continue }
                let entry = (messages[key] as? [String: Any])
                    ?? (messages.first(where: { $0.key.lowercased() == key.lowercased() })?.value as? [String: Any])
                if let message = entry?["message"] as? String {
                    name = message
                    break
                }
            }
        }
        return (name, version)
    }

    private static func firefoxExtensions() -> [BrowserExtension] {
        let fm = FileManager.default
        let profilesDir = AppPaths.library.appendingPathComponent("Application Support/Firefox/Profiles")
        guard let profiles = try? fm.contentsOfDirectory(at: profilesDir, includingPropertiesForKeys: nil) else { return [] }
        var out: [BrowserExtension] = []
        for profile in profiles {
            let jsonURL = profile.appendingPathComponent("extensions.json")
            guard let data = try? Data(contentsOf: jsonURL),
                  let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let addons = root["addons"] as? [[String: Any]] else { continue }
            for addon in addons {
                guard (addon["type"] as? String) == "extension",
                      (addon["location"] as? String) == "app-profile" else { continue }
                let id = addon["id"] as? String ?? ""
                let name = (addon["defaultLocale"] as? [String: Any])?["name"] as? String ?? id
                let path = addon["path"] as? String
                    ?? profile.appendingPathComponent("extensions/\(id).xpi").path
                out.append(BrowserExtension(name: name, browser: "Firefox", browserBundleID: "org.mozilla.firefox",
                                            profile: profile.lastPathComponent, extID: id,
                                            version: addon["version"] as? String ?? "", path: path, removable: true))
            }
        }
        return out
    }

    /// Safari extensions ship inside apps; listed read-only via pluginkit.
    private static func safariExtensions() -> [BrowserExtension] {
        var out: [BrowserExtension] = []
        var seen = Set<String>()
        for kind in ["com.apple.Safari.web-extension", "com.apple.Safari.extension", "com.apple.Safari.content-blocker"] {
            let output = Shell.run("/usr/bin/pluginkit", ["-mAvvv", "-p", kind]).out
            var current: [String: String] = [:]

            func flush() {
                if let path = current["Path"], seen.insert(path).inserted {
                    let fallback = ((path as NSString).lastPathComponent as NSString).deletingPathExtension
                    out.append(BrowserExtension(
                        name: current["Display Name"] ?? fallback,
                        browser: "Safari", browserBundleID: "com.apple.Safari", profile: "",
                        extID: current["id"] ?? path, version: current["version"] ?? "",
                        path: current["Parent Bundle"] ?? path, removable: false))
                }
                current = [:]
            }

            for raw in output.components(separatedBy: "\n") {
                let line = raw.trimmingCharacters(in: .whitespaces)
                if line.isEmpty { continue }
                if let separator = line.range(of: " = ") {
                    current[String(line[..<separator.lowerBound])] = String(line[separator.upperBound...])
                } else {
                    flush()
                    var header = line
                    if let first = header.first, "+-!=".contains(first) {
                        header = String(header.dropFirst()).trimmingCharacters(in: .whitespaces)
                    }
                    if let paren = header.firstIndex(of: "(") {
                        current["id"] = String(header[..<paren])
                        current["version"] = String(header[header.index(after: paren)...])
                            .replacingOccurrences(of: ")", with: "")
                    } else {
                        current["id"] = header
                    }
                }
            }
            flush()
        }
        return out
    }

    static func remove(_ ext: BrowserExtension) -> Bool {
        Trash.move([URL(fileURLWithPath: ext.path)]) == 1
    }
}

// MARK: - Install monitor (baseline / compare)

enum MonitorService {
    private static var file: URL { AppPaths.support.appendingPathComponent("baseline.json") }

    static var hasBaseline: Bool { FileManager.default.fileExists(atPath: file.path) }

    static var baselineDate: Date? {
        (try? FileManager.default.attributesOfItem(atPath: file.path))?[.modificationDate] as? Date
    }

    private static func watched() -> [(URL, MonitorChange.Kind)] {
        let lib = AppPaths.library
        return [
            (URL(fileURLWithPath: "/Applications"), .app),
            (AppPaths.home.appendingPathComponent("Applications"), .app),
            (lib.appendingPathComponent("Application Support"), .folder),
            (lib.appendingPathComponent("Preferences"), .preferences),
            (lib.appendingPathComponent("Containers"), .folder),
            (lib.appendingPathComponent("Group Containers"), .folder),
            (lib.appendingPathComponent("LaunchAgents"), .launchItem),
            (URL(fileURLWithPath: "/Library/Application Support"), .folder),
            (URL(fileURLWithPath: "/Library/LaunchAgents"), .launchItem),
            (URL(fileURLWithPath: "/Library/LaunchDaemons"), .launchItem),
            (URL(fileURLWithPath: "/Library/PrivilegedHelperTools"), .launchItem),
        ]
    }

    private static func capture() -> [String: String] {
        var map: [String: String] = [:]
        for (dir, kind) in watched() {
            guard let kids = try? FileManager.default.contentsOfDirectory(atPath: dir.path) else { continue }
            for kid in kids where !kid.hasPrefix(".") {
                map[dir.appendingPathComponent(kid).path] = kind.rawValue
            }
        }
        for pkg in Shell.run("/usr/sbin/pkgutil", ["--pkgs"]).out.split(separator: "\n") {
            map["pkg:" + String(pkg)] = MonitorChange.Kind.receipt.rawValue
        }
        return map
    }

    static func takeBaseline() -> Int {
        let snapshot = capture()
        if let data = try? JSONEncoder().encode(snapshot) { try? data.write(to: file) }
        return snapshot.count
    }

    static func compare() -> [MonitorChange] {
        guard let data = try? Data(contentsOf: file),
              let baseline = try? JSONDecoder().decode([String: String].self, from: data) else { return [] }
        let changes = capture().compactMap { entry -> MonitorChange? in
            guard baseline[entry.key] == nil else { return nil }
            let kind = MonitorChange.Kind(rawValue: entry.value) ?? .folder
            let path = entry.key.hasPrefix("pkg:") ? String(entry.key.dropFirst(4)) : entry.key
            return MonitorChange(kind: kind, path: path)
        }
        return changes.sorted { ($0.kind.rawValue, $0.path) < ($1.kind.rawValue, $1.path) }
    }

    static func remove(_ changes: [MonitorChange]) -> Int {
        var count = Trash.move(changes.filter { $0.kind != .receipt }.map { URL(fileURLWithPath: $0.path) })
        let receipts = changes.filter { $0.kind == .receipt }
        if !receipts.isEmpty {
            let command = receipts.map { "/usr/sbin/pkgutil --forget " + Shell.shQuote($0.path) }.joined(separator: "; ")
            if Shell.admin(command) { count += receipts.count }
        }
        return count
    }
}

// MARK: - Large files finder

enum LargeFilesService {
    static func scan(minBytes: Int64, progress: @escaping (Int) -> Void, isCancelled: () -> Bool) -> [LargeFile] {
        let keys: [URLResourceKey] = [.isRegularFileKey, .totalFileAllocatedSizeKey, .fileSizeKey, .contentModificationDateKey]
        guard let enumerator = FileManager.default.enumerator(
            at: AppPaths.home, includingPropertiesForKeys: keys,
            options: [.skipsHiddenFiles, .skipsPackageDescendants], errorHandler: { _, _ in true }) else { return [] }

        var found: [LargeFile] = []
        var scanned = 0
        for case let url as URL in enumerator {
            scanned += 1
            if scanned % 2000 == 0 {
                progress(scanned)
                if isCancelled() { break }
            }
            guard let values = try? url.resourceValues(forKeys: Set(keys)), values.isRegularFile == true else { continue }
            let size = Int64(values.totalFileAllocatedSize ?? values.fileSize ?? 0)
            if size >= minBytes {
                found.append(LargeFile(url: url, size: size, modified: values.contentModificationDate))
            }
        }
        progress(scanned)
        return Array(found.sorted { $0.size > $1.size }.prefix(300))
    }
}

// MARK: - Shredder

enum ShredderService {
    /// Overwrites every file with random bytes, then deletes it. Returns files shredded.
    static func shred(_ urls: [URL], progress: @escaping (Double) -> Void) -> Int {
        let fm = FileManager.default
        var files: [URL] = []
        var folders: [URL] = []
        for url in urls {
            var isDir: ObjCBool = false
            guard fm.fileExists(atPath: url.path, isDirectory: &isDir) else { continue }
            if isDir.boolValue {
                folders.append(url)
                if let enumerator = fm.enumerator(at: url, includingPropertiesForKeys: [.isRegularFileKey]) {
                    for case let file as URL in enumerator
                    where (try? file.resourceValues(forKeys: [.isRegularFileKey]))?.isRegularFile == true {
                        files.append(file)
                    }
                }
            } else {
                files.append(url)
            }
        }

        var done = 0
        for (index, file) in files.enumerated() {
            if overwrite(file), (try? fm.removeItem(at: file)) != nil { done += 1 }
            progress(Double(index + 1) / Double(max(files.count, 1)))
        }
        for folder in folders { try? fm.removeItem(at: folder) }
        return done
    }

    private static func overwrite(_ url: URL) -> Bool {
        guard let attributes = try? FileManager.default.attributesOfItem(atPath: url.path),
              let size = (attributes[.size] as? NSNumber)?.int64Value,
              let handle = try? FileHandle(forWritingTo: url) else { return false }
        defer { try? handle.close() }

        let chunk = 1 << 20
        var buffer = [UInt8](repeating: 0, count: chunk)
        var written: Int64 = 0
        do {
            try handle.seek(toOffset: 0)
            while written < size {
                let count = Int(min(Int64(chunk), size - written))
                buffer.withUnsafeMutableBytes { arc4random_buf($0.baseAddress, count) }
                try handle.write(contentsOf: Data(buffer[0..<count]))
                written += Int64(count)
            }
            try handle.synchronize()
            return true
        } catch {
            return false
        }
    }
}

// MARK: - History & privacy cleaner

enum HistoryCleaner {
    static func items() -> [HistoryItem] {
        [
            HistoryItem(id: "recents", titleKey: "hist.recents", detailKey: "hist.recents.d", symbol: "clock.arrow.circlepath", selected: true),
            HistoryItem(id: "quicklook", titleKey: "hist.quicklook", detailKey: "hist.quicklook.d", symbol: "eye", selected: true),
            HistoryItem(id: "clipboard", titleKey: "hist.clipboard", detailKey: "hist.clipboard.d", symbol: "doc.on.clipboard", selected: true),
            HistoryItem(id: "shell", titleKey: "hist.shell", detailKey: "hist.shell.d", symbol: "terminal", selected: false),
        ]
    }

    /// Clears everything except the clipboard (that one runs on the main thread). Returns items cleared.
    static func clean(_ ids: Set<String>) -> Int {
        let fm = FileManager.default
        var count = 0
        if ids.contains("recents") {
            let dir = AppPaths.library.appendingPathComponent("Application Support/com.apple.sharedfilelist")
            if let enumerator = fm.enumerator(at: dir, includingPropertiesForKeys: nil) {
                for case let file as URL in enumerator
                where file.lastPathComponent.contains("Recent") && file.pathExtension.hasPrefix("sfl") {
                    if (try? fm.removeItem(at: file)) != nil { count += 1 }
                }
            }
            Shell.run("/usr/bin/killall", ["sharedfilelistd"])
        }
        if ids.contains("quicklook"), Shell.run("/usr/bin/qlmanage", ["-r", "cache"]).status == 0 {
            count += 1
        }
        if ids.contains("shell") {
            for name in [".zsh_history", ".bash_history", ".zsh_sessions"] {
                if (try? fm.removeItem(at: AppPaths.home.appendingPathComponent(name))) != nil { count += 1 }
            }
        }
        return count
    }
}
