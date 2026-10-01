import AppKit
import Foundation

/// A .app bundle installed in /Applications or ~/Applications.
struct InstalledApp: Identifiable, Hashable {
    enum Arch: String {
        case universal, appleSilicon, intel, unknown
    }

    let id: String
    let url: URL
    let name: String
    let bundleID: String?
    let version: String
    let copyright: String
    let isAppStore: Bool
    let arch: Arch
    let installDate: Date?
    var lastUsed: Date? = nil
    var sizeBytes: Int64 = -1

    // Sort keys used by the table columns (all non-optional & Comparable).
    var sizeSort: Int64 { sizeBytes }
    var installSort: Date { installDate ?? .distantPast }
    var lastUsedSort: Date { lastUsed ?? .distantPast }
    var archSort: String { arch.rawValue }
    var sourceSort: String { isAppStore ? "0" : "1" }
}

/// A remnant left behind by an app (file, folder, preference, launch item, receipt…).
struct Leftover: Identifiable, Hashable {
    enum Kind: String {
        case folder, file, preferences, container, launchItem, receipt
    }

    enum Confidence: Int, Comparable {
        case low = 0, medium = 1, high = 2
        static func < (a: Confidence, b: Confidence) -> Bool { a.rawValue < b.rawValue }
    }

    let id = UUID()
    let path: String
    let kind: Kind
    let size: Int64
    let confidence: Confidence
    let isSystem: Bool
    var selected: Bool
}

struct LaunchItem: Identifiable, Hashable {
    enum Scope: String {
        case userAgent, globalAgent, daemon
    }

    var id: String { path }
    let path: String
    let label: String
    let program: String
    let scope: Scope
    let runAtLoad: Bool
    let disabled: Bool
}

struct JunkCategory: Identifiable, Hashable {
    enum Mode: Hashable {
        case contents, installers, trash
    }

    let id: String
    let titleKey: String
    let detailKey: String
    let symbol: String
    let colors: [UInt32]
    let roots: [URL]
    let mode: Mode
    var size: Int64 = 0
    var count: Int = 0
    var selected = true
    var scanned = false
}

struct BrowserExtension: Identifiable, Hashable {
    var id: String { browser + "|" + profile + "|" + extID }
    let name: String
    let browser: String
    let browserBundleID: String
    let profile: String
    let extID: String
    let version: String
    let path: String
    let removable: Bool
}

struct MonitorChange: Identifiable, Hashable {
    enum Kind: String {
        case app, folder, preferences, launchItem, receipt
    }

    var id: String { kind.rawValue + "|" + path }
    let kind: Kind
    let path: String
    var selected = true
}

struct LargeFile: Identifiable, Hashable {
    var id: String { url.path }
    let url: URL
    let size: Int64
    let modified: Date?
    var selected = false
}

struct RunningAppInfo: Identifiable, Hashable {
    let pid: pid_t
    let name: String
    let bundleID: String?
    let url: URL?

    var id: pid_t { pid }

    init(_ app: NSRunningApplication) {
        pid = app.processIdentifier
        name = app.localizedName ?? app.bundleIdentifier ?? "?"
        bundleID = app.bundleIdentifier
        url = app.bundleURL
    }
}

struct LogEntry: Codable, Identifiable, Hashable {
    let id: UUID
    let date: Date
    let actionKey: String
    let detail: String
}

struct HistoryItem: Identifiable, Hashable {
    let id: String
    let titleKey: String
    let detailKey: String
    let symbol: String
    var selected: Bool
}
