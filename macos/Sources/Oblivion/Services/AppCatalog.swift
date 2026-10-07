import AppKit
import Foundation

// finds installed .app bundles (like revo's "All applications")
enum AppCatalog {
    static var roots: [URL] {
        [URL(fileURLWithPath: "/Applications"), AppPaths.home.appendingPathComponent("Applications")]
    }

    static func scan() -> [InstalledApp] {
        var result: [InstalledApp] = []
        var seen = Set<String>()
        for root in roots {
            collect(in: root, depth: 0, into: &result, seen: &seen)
        }
        return result.sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }

    // goes into normal folders ("Utilities", "Adobe Photoshop 2025") but not into .app bundles
    private static func collect(in dir: URL, depth: Int, into result: inout [InstalledApp], seen: inout Set<String>) {
        let fm = FileManager.default
        guard let items = try? fm.contentsOfDirectory(at: dir, includingPropertiesForKeys: [.isDirectoryKey],
                                                      options: [.skipsHiddenFiles]) else { return }
        for url in items {
            if url.pathExtension.lowercased() == "app" {
                if let app = make(url), seen.insert(app.id).inserted {
                    result.append(app)
                }
            } else if depth < 2,
                      (try? url.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory == true {
                collect(in: url, depth: depth + 1, into: &result, seen: &seen)
            }
        }
    }

    static func make(_ url: URL) -> InstalledApp? {
        // skip SIP protected system apps (safari etc. live in /System)
        if url.resolvingSymlinksInPath().path.hasPrefix("/System/") { return nil }
        guard let bundle = Bundle(url: url) else { return nil }
        let info = bundle.infoDictionary ?? [:]
        let bundleID = bundle.bundleIdentifier
        if let bundleID, bundleID == "com.apple.Safari" || bundleID == Bundle.main.bundleIdentifier {
            return nil
        }

        var name = FileManager.default.displayName(atPath: url.path)
        if name.lowercased().hasSuffix(".app") { name = String(name.dropLast(4)) }

        let version = (info["CFBundleShortVersionString"] as? String)
            ?? (info["CFBundleVersion"] as? String) ?? "—"
        let copyright = (bundle.localizedInfoDictionary?["NSHumanReadableCopyright"] as? String)
            ?? (info["NSHumanReadableCopyright"] as? String) ?? ""
        let receipt = url.appendingPathComponent("Contents/_MASReceipt/receipt").path
        let created = (try? url.resourceValues(forKeys: [.creationDateKey]))?.creationDate

        return InstalledApp(
            id: url.path,
            url: url,
            name: name,
            bundleID: bundleID,
            version: version,
            copyright: copyright.trimmingCharacters(in: .whitespacesAndNewlines),
            isAppStore: FileManager.default.fileExists(atPath: receipt),
            arch: arch(of: bundle),
            installDate: created
        )
    }

    static func arch(of bundle: Bundle) -> InstalledApp.Arch {
        guard let archs = bundle.executableArchitectures?.map({ $0.intValue }) else { return .unknown }
        let arm = archs.contains(NSBundleExecutableArchitectureARM64)
        let intel = archs.contains(NSBundleExecutableArchitectureX86_64)
        if arm && intel { return .universal }
        if arm { return .appleSilicon }
        if intel { return .intel }
        return .unknown
    }

    // spotlight "last opened" date
    static func lastUsed(_ url: URL) -> Date? {
        NSMetadataItem(url: url)?.value(forAttribute: "kMDItemLastUsedDate") as? Date
    }
}
