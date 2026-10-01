import AppKit
import Foundation

/// Finds what an app leaves behind in the macOS Library folders.
///
/// Matching mirrors how macOS apps store data:
///   • High   — names built from the bundle identifier (com.vendor.app, com.vendor.app.plist,
///              TEAMID.com.vendor.app, com.vendor.app.savedState …)
///   • Medium — a folder named exactly like the app (Application Support/Spotify)
///   • Low    — looser name / vendor matches, shown but never pre-selected
enum LeftoverScanner {
    private struct Root {
        let url: URL
        let system: Bool
        let kind: Leftover.Kind
        let vendorMatch: Bool
    }

    private static func roots() -> [Root] {
        let lib = AppPaths.library
        func user(_ p: String, _ kind: Leftover.Kind, vendor: Bool = false) -> Root {
            Root(url: lib.appendingPathComponent(p), system: false, kind: kind, vendorMatch: vendor)
        }
        func sys(_ p: String, _ kind: Leftover.Kind, vendor: Bool = false) -> Root {
            Root(url: URL(fileURLWithPath: p), system: true, kind: kind, vendorMatch: vendor)
        }
        return [
            user("Application Support", .folder, vendor: true),
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
            user("LaunchAgents", .launchItem, vendor: true),
            sys("/Library/Application Support", .folder, vendor: true),
            sys("/Library/Caches", .folder),
            sys("/Library/Preferences", .preferences),
            sys("/Library/LaunchAgents", .launchItem, vendor: true),
            sys("/Library/LaunchDaemons", .launchItem, vendor: true),
            sys("/Library/PrivilegedHelperTools", .file, vendor: true),
            sys("/Library/Logs", .folder),
        ]
    }

    /// - Parameters:
    ///   - aggressive: "Force uninstall" — also vendor-prefix matches.
    ///   - preselectMedium: pre-tick exact app-name matches.
    static func scan(name: String, bundleID: String?, aggressive: Bool, preselectMedium: Bool) -> [Leftover] {
        let fm = FileManager.default
        let nameKey = normalize(name)
        let firstWord = normalize(name.split(separator: " ").first.map(String.init) ?? name)
        let bid = bundleID?.lowercased()
        let vendor = vendorPrefix(bid)

        var out: [Leftover] = []
        var seen = Set<String>()

        for root in roots() {
            guard let children = try? fm.contentsOfDirectory(atPath: root.url.path) else { continue }
            for child in children where !child.hasPrefix(".") {
                let lower = child.lowercased()
                let base = stripExtensions(lower)
                let compact = normalize(child)
                var confidence: Leftover.Confidence?

                if let bid, !bid.isEmpty,
                   (base == bid || base.hasPrefix(bid + ".") || base.hasSuffix("." + bid)
                    || (bid.count >= 10 && base.contains(bid))) {
                    confidence = .high
                } else if !nameKey.isEmpty, compact == nameKey {
                    confidence = .medium
                } else if nameKey.count >= 5, compact.contains(nameKey) {
                    confidence = .low
                } else if firstWord.count >= 4, compact == firstWord, root.vendorMatch {
                    confidence = .low
                } else if aggressive, root.vendorMatch, let vendor, base.hasPrefix(vendor + ".") {
                    confidence = .low
                }

                guard let found = confidence else { continue }
                let url = root.url.appendingPathComponent(child)
                guard !isProtected(lower), seen.insert(url.path).inserted else { continue }

                let selected = found == .high || (found == .medium && (preselectMedium || aggressive))
                out.append(Leftover(path: url.path, kind: root.kind, size: DiskSize.of(url),
                                    confidence: found, isSystem: root.system, selected: selected))
            }
        }

        // Installer receipts (pkgutil database).
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
            out.append(Leftover(path: pkg, kind: .receipt, size: 0, confidence: found, isSystem: true,
                                selected: found == .high || (found == .medium && (preselectMedium || aggressive))))
        }

        return out.sorted { a, b in
            if a.confidence != b.confidence { return a.confidence > b.confidence }
            return a.path.localizedStandardCompare(b.path) == .orderedAscending
        }
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

    /// "com.vendor.app" -> "com.vendor" (ignoring very generic prefixes).
    private static func vendorPrefix(_ bid: String?) -> String? {
        guard let bid else { return nil }
        let parts = bid.split(separator: ".")
        guard parts.count >= 3 else { return nil }
        let prefix = parts.prefix(2).joined(separator: ".")
        return ["com.apple", "com.google", "com.microsoft", "org.mozilla"].contains(prefix) ? nil : prefix
    }

    private static func isProtected(_ lowerName: String) -> Bool {
        lowerName.hasPrefix("com.apple.") || lowerName.contains("com.anilgul.oblivion")
    }
}

enum Uninstaller {
    /// Asks the app to quit, then force-quits it after a short grace period.
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

        // Stop user launch agents before trashing their plists.
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
