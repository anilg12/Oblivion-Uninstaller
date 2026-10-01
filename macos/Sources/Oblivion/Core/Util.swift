import AppKit
import Foundation

// MARK: - Formatting

enum Fmt {
    static func bytes(_ value: Int64) -> String {
        ByteCountFormatter.string(fromByteCount: max(value, 0), countStyle: .file)
    }

    private static let dateFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .none
        return f
    }()

    private static let dateTimeFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f
    }()

    static func date(_ date: Date?) -> String {
        guard let date else { return "—" }
        return dateFormatter.string(from: date)
    }

    static func dateTime(_ date: Date) -> String { dateTimeFormatter.string(from: date) }
}

// MARK: - Paths

enum AppPaths {
    static var home: URL { FileManager.default.homeDirectoryForCurrentUser }
    static var library: URL { home.appendingPathComponent("Library") }

    /// ~/Library/Application Support/Oblivion (created on demand).
    static var support: URL {
        let url = library.appendingPathComponent("Application Support/Oblivion")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }
}

// MARK: - Shell

struct ShellResult {
    let status: Int32
    let out: String
    let err: String
}

enum Shell {
    @discardableResult
    static func run(_ launchPath: String, _ args: [String]) -> ShellResult {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: launchPath)
        process.arguments = args
        let outPipe = Pipe()
        let errPipe = Pipe()
        process.standardOutput = outPipe
        process.standardError = errPipe
        do {
            try process.run()
        } catch {
            return ShellResult(status: -1, out: "", err: error.localizedDescription)
        }
        let outData = outPipe.fileHandleForReading.readDataToEndOfFile()
        let errData = errPipe.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        return ShellResult(status: process.terminationStatus,
                           out: String(decoding: outData, as: UTF8.self),
                           err: String(decoding: errData, as: UTF8.self))
    }

    /// Escapes a string for use inside an AppleScript double-quoted literal.
    static func asEscape(_ s: String) -> String {
        s.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\"", with: "\\\"")
    }

    /// Single-quotes a string for /bin/sh.
    static func shQuote(_ s: String) -> String {
        "'" + s.replacingOccurrences(of: "'", with: "'\\''") + "'"
    }

    @discardableResult
    static func osascript(_ lines: [String]) -> ShellResult {
        var args: [String] = []
        for line in lines { args += ["-e", line] }
        return run("/usr/bin/osascript", args)
    }

    /// Runs a shell command with an administrator password prompt.
    @discardableResult
    static func admin(_ command: String) -> Bool {
        osascript(["do shell script \"\(asEscape(command))\" with administrator privileges"]).status == 0
    }
}

// MARK: - Trash

enum Trash {
    /// Moves items to the Trash. Items the user can't move (root-owned apps,
    /// /Library files…) are handed to Finder, which asks for the admin password.
    /// Returns how many items are gone afterwards.
    @discardableResult
    static func move(_ urls: [URL]) -> Int {
        let fm = FileManager.default
        var moved = 0
        var needsFinder: [URL] = []

        for url in urls where fm.fileExists(atPath: url.path) {
            do {
                try fm.trashItem(at: url, resultingItemURL: nil)
                moved += 1
            } catch {
                needsFinder.append(url)
            }
        }

        if !needsFinder.isEmpty {
            let items = needsFinder
                .map { "(POSIX file \"\(Shell.asEscape($0.path))\" as alias)" }
                .joined(separator: ", ")
            Shell.osascript(["tell application \"Finder\" to delete {\(items)}"])
            moved += needsFinder.filter { !fm.fileExists(atPath: $0.path) }.count
        }
        return moved
    }
}

// MARK: - Disk sizes

enum DiskSize {
    private static let keys: [URLResourceKey] = [.isRegularFileKey, .totalFileAllocatedSizeKey, .fileAllocatedSizeKey]

    static func of(_ url: URL) -> Int64 {
        let fm = FileManager.default
        var isDir: ObjCBool = false
        guard fm.fileExists(atPath: url.path, isDirectory: &isDir) else { return 0 }
        guard isDir.boolValue else { return fileSize(url) }

        guard let enumerator = fm.enumerator(at: url, includingPropertiesForKeys: keys,
                                             options: [], errorHandler: { _, _ in true }) else { return 0 }
        var total: Int64 = 0
        for case let item as URL in enumerator {
            guard let values = try? item.resourceValues(forKeys: Set(keys)),
                  values.isRegularFile == true else { continue }
            total += Int64(values.totalFileAllocatedSize ?? values.fileAllocatedSize ?? 0)
        }
        return total
    }

    static func fileSize(_ url: URL) -> Int64 {
        let values = try? url.resourceValues(forKeys: [.totalFileAllocatedSizeKey, .fileSizeKey])
        return Int64(values?.totalFileAllocatedSize ?? values?.fileSize ?? 0)
    }
}

// MARK: - Icons

final class IconCache {
    static let shared = IconCache()
    private let cache = NSCache<NSString, NSImage>()

    func icon(for path: String) -> NSImage {
        if let cached = cache.object(forKey: path as NSString) { return cached }
        let image = NSWorkspace.shared.icon(forFile: path)
        cache.setObject(image, forKey: path as NSString)
        return image
    }
}

// MARK: - Activity log (the "Logs database")

enum ActivityLog {
    private static let lock = NSLock()
    static var url: URL { AppPaths.support.appendingPathComponent("activity.jsonl") }

    static func append(_ actionKey: String, _ detail: String) {
        let entry = LogEntry(id: UUID(), date: Date(), actionKey: actionKey, detail: detail)
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        guard var data = try? encoder.encode(entry) else { return }
        data.append(0x0A)

        lock.lock()
        defer { lock.unlock() }
        if let handle = try? FileHandle(forWritingTo: url) {
            defer { try? handle.close() }
            _ = try? handle.seekToEnd()
            try? handle.write(contentsOf: data)
        } else {
            try? data.write(to: url)
        }
    }

    static func all() -> [LogEntry] {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else { return [] }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let entries = text.split(separator: "\n").compactMap { line -> LogEntry? in
            try? decoder.decode(LogEntry.self, from: Data(line.utf8))
        }
        return entries.reversed()
    }

    static func clear() {
        lock.lock()
        defer { lock.unlock() }
        try? FileManager.default.removeItem(at: url)
    }
}

// MARK: - System information

enum SystemInfo {
    static var macOSVersion: String {
        let v = ProcessInfo.processInfo.operatingSystemVersion
        var name = ""
        switch v.majorVersion {
        case 26: name = "Tahoe"
        case 15: name = "Sequoia"
        case 14: name = "Sonoma"
        default: name = ""
        }
        let number = v.patchVersion > 0
            ? "\(v.majorVersion).\(v.minorVersion).\(v.patchVersion)"
            : "\(v.majorVersion).\(v.minorVersion)"
        return name.isEmpty ? "macOS \(number)" : "macOS \(name) \(number)"
    }

    static var chip: String { sysctlString("machdep.cpu.brand_string") ?? "Apple Silicon" }

    static var memory: String {
        ByteCountFormatter.string(fromByteCount: Int64(ProcessInfo.processInfo.physicalMemory), countStyle: .memory)
    }

    static func disk() -> (free: Int64, total: Int64) {
        let values = try? URL(fileURLWithPath: "/").resourceValues(
            forKeys: [.volumeAvailableCapacityForImportantUsageKey, .volumeTotalCapacityKey])
        return (values?.volumeAvailableCapacityForImportantUsage ?? 0, Int64(values?.volumeTotalCapacity ?? 0))
    }

    /// Full Disk Access check: these folders are TCC-protected without it.
    static var hasFullDiskAccess: Bool {
        let probes = [
            AppPaths.library.appendingPathComponent("Safari"),
            AppPaths.library.appendingPathComponent("Application Support/com.apple.TCC"),
        ]
        return probes.contains { (try? FileManager.default.contentsOfDirectory(atPath: $0.path)) != nil }
    }

    static func openFullDiskAccessSettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles") {
            NSWorkspace.shared.open(url)
        }
    }

    static func openLoginItemsSettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.LoginItems-Settings.extension") {
            NSWorkspace.shared.open(url)
        }
    }

    private static func sysctlString(_ name: String) -> String? {
        var size = 0
        guard sysctlbyname(name, nil, &size, nil, 0) == 0, size > 0 else { return nil }
        var buffer = [CChar](repeating: 0, count: size)
        guard sysctlbyname(name, &buffer, &size, nil, 0) == 0 else { return nil }
        return buffer.withUnsafeBufferPointer { ptr -> String? in
            guard let base = ptr.baseAddress else { return nil }
            return String(cString: base)
        }
    }
}
