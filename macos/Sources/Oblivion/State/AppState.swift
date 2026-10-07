import AppKit
import SwiftUI

enum Page: String, CaseIterable, Identifiable {
    case dashboard, apps, storeApps, monitor, browserExt, systemMonitor, logs, hunter
    case tools, startup, junk, largeFiles, shredder, history, settings

    var id: String { rawValue }

    static let toolPages: [Page] = [.tools, .startup, .junk, .largeFiles, .shredder, .history]
}

// user prefs (UserDefaults)
final class Prefs: ObservableObject {
    @Published var appearance: String {
        didSet { UserDefaults.standard.set(appearance, forKey: "oblivion.appearance") }
    }
    @Published var confirmBeforeDelete: Bool {
        didSet { UserDefaults.standard.set(confirmBeforeDelete, forKey: "oblivion.confirm") }
    }
    // live cpu / memory / disk / network panel on the right
    @Published var showLivePanel: Bool {
        didSet { UserDefaults.standard.set(showLivePanel, forKey: "oblivion.livePanel") }
    }
    // fewer animations (also on if macOS reduce motion is set)
    @Published var reduceMotion: Bool {
        didSet { UserDefaults.standard.set(reduceMotion, forKey: "oblivion.reduceMotion") }
    }

    init() {
        let defaults = UserDefaults.standard
        appearance = defaults.string(forKey: "oblivion.appearance") ?? "dark"
        confirmBeforeDelete = defaults.object(forKey: "oblivion.confirm") as? Bool ?? true
        showLivePanel = defaults.object(forKey: "oblivion.livePanel") as? Bool ?? true
        reduceMotion = defaults.object(forKey: "oblivion.reduceMotion") as? Bool ?? false
    }

    // skip the decorative looping animations
    var calmMotion: Bool {
        reduceMotion || NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
    }

    var colorScheme: ColorScheme? {
        switch appearance {
        case "dark": return .dark
        case "light": return .light
        default: return nil
        }
    }

    func cycleAppearance() {
        switch appearance {
        case "dark": appearance = "light"
        case "light": appearance = "system"
        default: appearance = "dark"
        }
    }
}

// app state: navigation, installed apps, uninstall flow
@MainActor
final class AppState: ObservableObject {
    enum Stage: Equatable {
        case browsing, working, review, done
    }

    // navigation
    @Published var page: Page = .dashboard

    // catalog
    @Published var apps: [InstalledApp] = []
    @Published var isLoadingApps = false
    @Published var sizesPending = 0
    @Published var selectedAppID: InstalledApp.ID?
    @Published var search = ""

    // uninstall flow
    @Published var stage: Stage = .browsing
    @Published var workKey = "work.scanning"
    @Published var leftovers: [Leftover] = []
    @Published var targetName = ""
    @Published var targetIconPath: String?
    @Published var freedBytes: Int64 = 0
    @Published var removedCount = 0
    @Published var appRemoved = false

    // dialogs
    @Published var showAbout = false
    // pending destructive action, shown as a sheet with the exact list
    @Published var confirm: ConfirmRequest?
    @Published var confirmApp: InstalledApp?
    @Published var showForceSheet = false
    @Published var forceQuery = ""
    @Published var errorMessageKey: String?

    // dashboard / right panel
    @Published var recentActivity: [LogEntry] = []
    @Published var launchItemCount = 0
    @Published var diskFree: Int64 = 0
    @Published var diskTotal: Int64 = 0
    @Published var hasFullDiskAccess = true

    weak var prefs: Prefs?

    // when the working screen showed up. it stays at least minWorkTime so fast operations
    // don't just flash it
    private var workStartedAt = Date()
    private let minWorkTime: TimeInterval = 0.9

    init() {
        refreshSystem()
        refreshActivity()
    }

    var selectedApp: InstalledApp? {
        guard let selectedAppID else { return nil }
        return apps.first { $0.id == selectedAppID }
    }

    var totalAppBytes: Int64 { apps.reduce(Int64(0)) { $0 + max($1.sizeBytes, 0) } }

    var isOnAppList: Bool { page == .apps || page == .storeApps }

    func navigate(_ target: Page) {
        withAnimation(.spring(response: 0.38, dampingFraction: 0.86)) { page = target }
        refreshActivity()
    }

    func refreshSystem() {
        let disk = SystemInfo.disk()
        diskFree = disk.free
        diskTotal = disk.total
        hasFullDiskAccess = SystemInfo.hasFullDiskAccess
    }

    func refreshActivity() {
        recentActivity = Array(ActivityLog.all().prefix(6))
    }

    // MARK: Catalog

    func loadApps(force: Bool = false) {
        if isLoadingApps { return }
        if !apps.isEmpty && !force { return }
        isLoadingApps = true
        Task {
            let list = await Task.detached(priority: .userInitiated) { AppCatalog.scan() }.value
            self.apps = list
            if let id = self.selectedAppID, !list.contains(where: { $0.id == id }) { self.selectedAppID = nil }
            self.isLoadingApps = false
            self.measureSizes()
        }
        Task {
            let count = await Task.detached { LaunchItemsService.scan().count }.value
            self.launchItemCount = count
        }
    }

    private func measureSizes() {
        let targets = apps.filter { $0.sizeBytes < 0 }.map { ($0.id, $0.url) }
        sizesPending = targets.count
        guard !targets.isEmpty else { return }
        Task.detached(priority: .utility) { [weak self] in
            var batch: [(String, Int64, Date?)] = []
            for (id, url) in targets {
                batch.append((id, DiskSize.of(url), AppCatalog.lastUsed(url)))
                if batch.count >= 6 {
                    let ready = batch
                    batch.removeAll()
                    await self?.applySizes(ready)
                }
            }
            if !batch.isEmpty { await self?.applySizes(batch) }
        }
    }

    private func applySizes(_ batch: [(String, Int64, Date?)]) {
        var index: [String: Int] = [:]
        for (i, app) in apps.enumerated() { index[app.id] = i }
        for (id, size, used) in batch {
            guard let i = index[id] else { continue }
            apps[i].sizeBytes = size
            apps[i].lastUsed = used
        }
        sizesPending = max(0, sizesPending - batch.count)
    }

    // MARK: Uninstall flow

    func requestUninstall(_ app: InstalledApp?) {
        guard let app, stage == .browsing else { return }
        if prefs?.confirmBeforeDelete ?? true {
            confirmApp = app
        } else {
            uninstall(app)
        }
    }

    func uninstall(_ app: InstalledApp) {
        confirmApp = nil
        if !isOnAppList { navigate(app.isAppStore ? .storeApps : .apps) }
        beginFlow(name: app.name, iconPath: app.url.path)
        workKey = "work.quitting"

        Task {
            let removed = await Task.detached { () -> Bool in
                Uninstaller.quitApp(bundleID: app.bundleID)
                return Uninstaller.removeBundle(app.url)
            }.value

            guard removed else {
                self.errorMessageKey = "error.removeFailed"
                withAnimation(.spring) { self.stage = .browsing }
                return
            }

            self.appRemoved = true
            self.removedCount = 1
            self.freedBytes = max(app.sizeBytes, 0)
            self.apps.removeAll { $0.id == app.id }
            if self.selectedAppID == app.id { self.selectedAppID = nil }
            ActivityLog.append("log.uninstalled", app.name)

            self.workKey = "work.scanning"
            let found = await Task.detached {
                LeftoverScanner.scan(name: app.name, bundleID: app.bundleID)
            }.value
            await self.settle()
            self.finishScan(found)
        }
    }

    // force uninstall: aggressive scan for a name / bundle id (works for already deleted apps too),
    // removes the bundle if it's still there
    func forceUninstall() {
        let query = forceQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { return }
        showForceSheet = false

        let match = apps.first {
            $0.name.caseInsensitiveCompare(query) == .orderedSame
                || $0.bundleID?.caseInsensitiveCompare(query) == .orderedSame
                || $0.url.path == query
        }
        let looksLikeBundleID = query.contains(".") && !query.contains(" ") && !query.hasPrefix("/")
        let name = match?.name ?? (looksLikeBundleID ? String(query.split(separator: ".").last ?? Substring(query)) : query)
        let bundleID = match?.bundleID ?? (looksLikeBundleID ? query : nil)

        if !isOnAppList { navigate(.apps) }
        beginFlow(name: name, iconPath: match?.url.path)
        workKey = match == nil ? "work.scanning" : "work.quitting"

        Task {
            if let match {
                let removed = await Task.detached { () -> Bool in
                    Uninstaller.quitApp(bundleID: match.bundleID)
                    return Uninstaller.removeBundle(match.url)
                }.value
                if removed {
                    self.appRemoved = true
                    self.removedCount = 1
                    self.freedBytes = max(match.sizeBytes, 0)
                    self.apps.removeAll { $0.id == match.id }
                }
            }
            self.workKey = "work.scanning"
            let found = await Task.detached {
                LeftoverScanner.scan(name: name, bundleID: bundleID)
            }.value
            ActivityLog.append("log.forced", name)
            await self.settle()
            self.finishScan(found)
        }
    }

    private func beginFlow(name: String, iconPath: String?) {
        targetName = name
        targetIconPath = iconPath
        if let iconPath { _ = IconCache.shared.icon(for: iconPath) }   // keep the icon after the bundle is gone
        freedBytes = 0
        removedCount = 0
        appRemoved = false
        leftovers = []
        workStartedAt = Date()
        withAnimation(.spring) { stage = .working }
    }

    private func settle() async {
        let remaining = minWorkTime - Date().timeIntervalSince(workStartedAt)
        if remaining > 0 { try? await Task.sleep(nanoseconds: UInt64(remaining * 1_000_000_000)) }
    }

    private func finishScan(_ found: [Leftover]) {
        leftovers = found
        refreshActivity()
        withAnimation(.spring) { stage = found.isEmpty ? .done : .review }
    }

    func removeSelectedLeftovers() {
        let chosen = leftovers.filter(\.selected)
        guard !chosen.isEmpty else {
            withAnimation(.spring) { stage = .done }
            return
        }
        workKey = "work.removing"
        workStartedAt = Date()
        withAnimation(.spring) { stage = .working }
        Task {
            let result = await Task.detached { LeftoverRemover.remove(chosen) }.value
            await self.settle()
            self.removedCount += result.count
            self.freedBytes += result.bytes
            ActivityLog.append("log.leftovers", "\(self.targetName) · \(result.count) · \(Fmt.bytes(result.bytes))")
            self.refreshActivity()
            self.refreshSystem()
            withAnimation(.spring) { self.stage = .done }
        }
    }

    func setAllLeftovers(_ value: Bool) {
        for i in leftovers.indices { leftovers[i].selected = value }
    }

    // select only the exact (bundle id) matches. the user has to click this, never automatic
    func selectCertainLeftovers() {
        for i in leftovers.indices { leftovers[i].selected = leftovers[i].confidence == .high }
    }

    func finishFlow() {
        withAnimation(.spring) {
            stage = .browsing
            leftovers = []
        }
        refreshSystem()
    }

    // MARK: "Other commands"

    func reveal(_ app: InstalledApp) {
        NSWorkspace.shared.activateFileViewerSelecting([app.url])
    }

    func showPackageContents(_ app: InstalledApp) {
        NSWorkspace.shared.open(app.url.appendingPathComponent("Contents"))
    }

    func open(_ app: InstalledApp) {
        NSWorkspace.shared.openApplication(at: app.url, configuration: NSWorkspace.OpenConfiguration())
    }

    func getInfo(_ app: InstalledApp) {
        let path = Shell.asEscape(app.url.path)
        Task.detached {
            Shell.osascript([
                "tell application \"Finder\" to activate",
                "tell application \"Finder\" to open information window of (POSIX file \"\(path)\" as alias)",
            ])
        }
    }

    func copyDetails(_ app: InstalledApp) {
        let text = [
            "Name: \(app.name)",
            "Bundle ID: \(app.bundleID ?? "—")",
            "Version: \(app.version)",
            "Size: \(app.sizeBytes < 0 ? "—" : Fmt.bytes(app.sizeBytes))",
            "Path: \(app.url.path)",
            "App Store: \(app.isAppStore ? "yes" : "no")",
        ].joined(separator: "\n")
        _ = NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
    }

    func searchWeb(_ app: InstalledApp) {
        let query = "\(app.name) mac".addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? app.name
        if let url = URL(string: "https://www.google.com/search?q=\(query)") {
            NSWorkspace.shared.open(url)
        }
    }

    // hunter mode: uninstall the app at url
    func uninstallApp(at url: URL) {
        if let app = apps.first(where: { $0.url.standardizedFileURL == url.standardizedFileURL }) ?? AppCatalog.make(url) {
            selectedAppID = app.id
            requestUninstall(app)
        }
    }
}
