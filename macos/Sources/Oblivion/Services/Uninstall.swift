import AppKit
import Foundation

// finds what an app leaves behind in the Library folders.
//
// matching:
//   high   - names made from the bundle id (com.vendor.app, com.vendor.app.plist,
//            TEAMID.com.vendor.app, com.vendor.app.savedState ...)
//   medium - folder with exactly the app's name (Application Support/Spotify)
//   low    - looser name matches
// nothing is pre-selected, the user ticks what goes
enum LeftoverScanner {
    private struct Root {
        let url: URL
        let system: Bool
        let kind: Leftover.Kind
    }

    private static func roots() -> [Root] {
        let lib = AppPaths.library
        func user(_ p: String, _ kind: Leftover.Kind) -> Root {
            Root(url: lib.appendingPathComponent(p), system: false, kind: kind)
        }
        func sys(_ p: String, _ kind: Leftover.Kind) -> Root {
            Root(url: URL(fileURLWithPath: p), system: true, kind: kind)
        }
        return [
            user("Application Support", .folder),
            user("Caches", .folder),
            user("Preferences", .preferences),
            user("Preferences/ByHost", .preferences),
            user("Containers", .container),
            user("Group Containers", .container),
            user("Saved Application State", .folder),
            user("Logs", .folder),
            user("HTTPStorages", .folder),
            user("WebKit", .folder),
            user("Cookies", .file),
            user("Application Scripts", .folder),
            user("LaunchAgents", .launchItem),
            sys("/Library/Application Support", .folder),
            sys("/Library/Caches", .folder),
            sys("/Library/Preferences", .preferences),
            sys("/Library/LaunchAgents", .launchItem),
            sys("/Library/LaunchDaemons", .launchItem),
            sys("/Library/PrivilegedHelperTools", .file),
            sys("/Library/Logs", .folder),
        ]
    }

    static func scan(name: String, bundleID: String?) -> [Leftover] {
        let fm = FileManager.default
        let nameKey = normalize(name)
        let firstWord = normalize(name.split(separator: " ").first.map(String.init) ?? name)
        let bid = bundleID?.lowercased()

        // lots of apps keep data one level down in a vendor folder:
        //   Application Support/Google/Chrome, Caches/Google/Chrome, Application Support/Adobe/Photoshop 2025
        // vendor = first word of a multi-word name ("Google") or the company part of the bundle id ("com.google.Chrome")
        let words = name.split(separator: " ").map(String.init)
        let restKey = words.count > 1 ? normalize(words.dropFirst().joined()) : ""
        var vendorKeys = Set<String>()
        if words.count > 1, firstWord.count >= 3 { vendorKeys.insert(firstWord) }
        if let bid {
            let parts = bid.split(separator: ".")
            if parts.count >= 3 {
                let company = normalize(String(parts[1]))
                if company.count >= 3 && company != "apple" { vendorKeys.insert(company) }
            }
        }

        var out: [Leftover] = []
        var seen = Set<String>()

        func add(_ url: URL, _ found: Leftover.Confidence, _ root: Root) {
            guard !isProtected(url.lastPathComponent.lowercased()), seen.insert(url.path).inserted else { return }
            out.append(Leftover(path: url.path, kind: root.kind, size: DiskSize.of(url),
                                confidence: found, isSystem: root.system, selected: false))
        }

        for root in roots() {
            guard let children = try? fm.contentsOfDirectory(atPath: root.url.path) else { continue }
            for child in children where !child.hasPrefix(".") {
                let lower = child.lowercased()
                let base = stripExtensions(lower)
                let compact = normalize(child)
                var confidence: Leftover.Confidence?

                // vendor folder (Application Support/Google, .../Microsoft): only offer this app's own subfolders,
                // never the vendor folder itself since other apps keep data there too
                if vendorKeys.contains(compact), compact != nameKey {
                    let nested = nestedMatches(in: root.url.appendingPathComponent(child),
                                               nameKey: nameKey, restKey: restKey, bid: bid)
                    for (url, found) in nested { add(url, found, root) }
                    continue
                }

                if let bid, !bid.isEmpty,
                   (base == bid || base.hasPrefix(bid + ".") || base.hasSuffix("." + bid)
                    || (bid.count >= 10 && base.contains(bid))) {
                    confidence = .high
                } else if !nameKey.isEmpty, compact == nameKey {
                    confidence = .medium
                } else if nameKey.count >= 5, compact.contains(nameKey) {
                    confidence = .low
                }

                guard let found = confidence, !isProtected(lower) else { continue }
                add(root.url.appendingPathComponent(child), found, root)
            }
        }

        // installer receipts (pkgutil)
        let pkgs = Shell.run("/usr/sbin/pkgutil", ["--pkgs"]).out
            .split(separator: "\n").map(String.init)
        for pkg in pkgs {
            let lower = pkg.lowercased()
            if lower.hasPrefix("com.apple.") { continue }
            var confidence: Leftover.Confidence?
            if let bid, !bid.isEmpty, lower == bid || lower.hasPrefix(bid) {
                confidence = .high
            } else if nameKey.count >= 4, normalize(pkg).contains(nameKey) {
                confidence = .medium
            }
            guard let found = confidence else { continue }
            out.append(Leftover(path: pkg, kind: .receipt, size: 0, confidence: found, isSystem: true, selected: false))
        }

        return out.sorted { a, b in
            if a.confidence != b.confidence { return a.confidence > b.confidence }
            return a.path.localizedStandardCompare(b.path) == .orderedAscending
        }
    }

    // subfolders of a vendor folder that belong to this app
    private static func nestedMatches(in dir: URL, nameKey: String, restKey: String,
                                      bid: String?) -> [(URL, Leftover.Confidence)] {
        guard let kids = try? FileManager.default.contentsOfDirectory(atPath: dir.path) else { return [] }
        var out: [(URL, Leftover.Confidence)] = []
        for kid in kids where !kid.hasPrefix(".") {
            let base = stripExtensions(kid.lowercased())
            let compact = normalize(kid)
            let url = dir.appendingPathComponent(kid)
            if let bid, !bid.isEmpty, base == bid || base.hasPrefix(bid + ".") {
                out.append((url, .high))
            } else if compact == nameKey || (restKey.count >= 3 && compact == restKey) {
                out.append((url, .medium))
            }
        }
        return out
    }

    static func normalize(_ s: String) -> String {
        String(s.lowercased().unicodeScalars.filter { CharacterSet.alphanumerics.contains($0) })
    }

    private static func stripExtensions(_ s: String) -> String {
        var result = s
        for ext in [".plist", ".savedstate", ".binarycookies", ".lockfile"] where result.hasSuffix(ext) {
            result = String(result.dropLast(ext.count))
        }
        return result
    }

    private static func isProtected(_ lowerName: String) -> Bool {
        lowerName.hasPrefix("com.apple.") || lowerName.contains("com.anilgul.oblivion")
    }
}

enum Uninstaller {
    // normal quit first, force quit after a short wait
    static func quitApp(bundleID: String?) {
        guard let bundleID else { return }
        let running = NSRunningApplication.runningApplications(withBundleIdentifier: bundleID)
        guard !running.isEmpty else { return }
        for app in running { _ = app.terminate() }
        let deadline = Date().addingTimeInterval(4)
        while Date() < deadline, running.contains(where: { !$0.isTerminated }) {
            Thread.sleep(forTimeInterval: 0.2)
        }
        for app in running where !app.isTerminated { _ = app.forceTerminate() }
    }

    static func removeBundle(_ url: URL) -> Bool {
        Trash.move([url])
        return !FileManager.default.fileExists(atPath: url.path)
    }
}

enum LeftoverRemover {
    static func remove(_ items: [Leftover]) -> (count: Int, bytes: Int64) {
        let fm = FileManager.default
        let files = items.filter { $0.kind != .receipt }

        // stop user launch agents before trashing their plists
        for item in files where item.kind == .launchItem && !item.isSystem {
            Shell.run("/bin/launchctl", ["bootout", "gui/\(getuid())", item.path])
        }

        var count = Trash.move(files.map { URL(fileURLWithPath: $0.path) })
        let bytes = files
            .filter { !fm.fileExists(atPath: $0.path) }
            .reduce(Int64(0)) { $0 + $1.size }

        let receipts = items.filter { $0.kind == .receipt }
        if !receipts.isEmpty {
            let command = receipts
                .map { "/usr/sbin/pkgutil --forget " + Shell.shQuote($0.path) }
                .joined(separator: "; ")
            if Shell.admin(command) { count += receipts.count }
        }
        return (count, bytes)
    }
}
