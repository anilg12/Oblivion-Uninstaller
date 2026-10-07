import AppKit
import SwiftUI

// CI self-test, only runs when OBLIVION_SNAPSHOT_DIR is set (see ci-smoke-test.sh).
// goes through every page and saves a png of each, checks nothing is pre-selected,
// toggles a startup item off and on, does a normal and a forced uninstall on the fixture apps,
// writes report.txt and quits. lines starting with "!! " are failures
@MainActor
enum SnapshotRunner {
    private static var started = false
    private static weak var junk: JunkModel?
    private static weak var startup: StartupModel?
    private static weak var system: SystemMonitor?

    // set by the app before the first page shows up
    static func register(junk: JunkModel, startup: StartupModel, system: SystemMonitor) {
        self.junk = junk
        self.startup = startup
        self.system = system
    }

    static func startIfRequested(state: AppState, prefs: Prefs, loc: Loc) {
        guard !started, let path = ProcessInfo.processInfo.environment["OBLIVION_SNAPSHOT_DIR"] else { return }
        started = true
        let dir = URL(fileURLWithPath: path)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        Task { await run(dir: dir, state: state, prefs: prefs, loc: loc) }
    }

    private static func run(dir: URL, state: AppState, prefs: Prefs, loc: Loc) async {
        var report: [String] = []
        prefs.appearance = "dark"
        prefs.showLivePanel = true
        prefs.reduceMotion = false
        loc.lang = "tr"
        await wait(2)
        // about a 13" macbook air window (the ci screen is smaller)
        mainWindow?.setFrame(NSRect(x: 0, y: 0, width: 1440, height: 880), display: true)
        await wait(1)
        await waitWhile(timeout: 30) { state.isLoadingApps }

        if let window = mainWindow {
            report.append("window: \(window.frame)  screen: \(window.screen?.frame ?? .zero)")
        }
        report.append("architecture: \(StaticInfoReader.architecture())")
        report.append("apps found: \(state.apps.count)")
        report.append("full disk access: \(state.hasFullDiskAccess)")

        let pages: [Page] = [.dashboard, .apps, .storeApps, .systemMonitor, .monitor, .browserExt, .logs, .hunter,
                             .tools, .startup, .junk, .largeFiles, .shredder, .history, .settings]
        for (index, page) in pages.enumerated() {
            state.navigate(page)
            if page == .apps { state.selectedAppID = state.apps.first?.id }
            let slow: Set<Page> = [.browserExt, .junk, .startup, .apps, .systemMonitor, .dashboard]
            await wait(slow.contains(page) ? 3.5 : 1.5)
            capture(dir, String(format: "%02d-", index + 1) + page.rawValue + "-dark")
        }

        // live monitor values (sampler was running during the pages above)
        if let s = system?.snapshot {
            report.append(String(format: "monitor: cpu %.1f%%  memory %.1f%%  temperature %@  thermal %ld  drives %ld  battery %@",
                                 s.cpu, s.memPercent, s.temperature.map { String(format: "%.1f °C", $0) } ?? "n/a",
                                 s.thermalState.rawValue, s.drives.count, s.battery.map { "\($0.percent)%" } ?? "none"))
            if !s.sampled { report.append("!! system monitor never sampled") }
            if let info = system?.info { report.append("this Mac: \(info.modelName) \(info.modelID) · \(info.chip) · \(info.cores) · \(info.architecture)") }
        }

        // about sheet
        state.navigate(.dashboard)
        state.showAbout = true
        await wait(2.2)
        capture(dir, "about-dark", sheet: true)
        state.showAbout = false
        await wait(0.8)

        prefs.appearance = "light"
        for page in [Page.dashboard, .apps, .systemMonitor, .tools, .junk] {
            state.navigate(page)
            await wait(page == .systemMonitor ? 2.5 : 1.5)
            capture(dir, "light-" + page.rawValue)
        }
        prefs.appearance = "dark"
        loc.lang = "en"
        for page in [Page.dashboard, .systemMonitor, .junk] {
            state.navigate(page)
            await wait(1.5)
            capture(dir, "en-" + page.rawValue)
        }
        loc.lang = "tr"

        await junkCheck(dir: dir, state: state, loc: loc, report: &report)
        await startupCheck(state: state, report: &report)

        // normal uninstall of the fixture app
        state.navigate(.apps)
        await wait(1)
        if let app = state.apps.first(where: { $0.bundleID == "com.oblivion.testapp" }) {
            state.selectedAppID = app.id
            await wait(1.5)
            capture(dir, "flow-1-selected")
            state.uninstall(app)
            await wait(0.3)
            capture(dir, "flow-2-working")
            await waitWhile(timeout: 40) { state.stage == .working }
            report.append("stage after scan: \(state.stage)")
            await wait(1.5)
            capture(dir, "flow-3-review")
            report.append("")
            report.append("== Uninstall com.oblivion.testapp → stage \(state.stage), app removed: \(state.appRemoved)")
            listLeftovers(state, &report)
            // like a user would: select everything, confirm, delete
            state.setAllLeftovers(true)
            await wait(0.6)
            capture(dir, "flow-3b-all-selected")
            state.confirm = ConfirmRequest(
                title: loc.t("left.confirmTitle", ["count": "\(state.leftovers.count)"]),
                message: loc.t("left.confirmBody", ["size": Fmt.bytes(state.leftovers.reduce(Int64(0)) { $0 + $1.size })]),
                details: state.leftovers.map(\.path),
                confirmTitle: loc["left.remove"],
                symbol: "trash.fill"
            ) {}
            await wait(1.4)
            capture(dir, "flow-3c-confirm", sheet: true)
            state.confirm = nil
            await wait(0.8)
            state.removeSelectedLeftovers()
            await wait(0.4)
            capture(dir, "flow-4a-removing")
            await waitWhile(timeout: 40) { state.stage == .working }
            report.append("stage after removal: \(state.stage)")
            await wait(0.5)
            capture(dir, "flow-4b-done-0.5s")
            await wait(2)
            capture(dir, "flow-4c-done-2.5s")
            report.append("stage at last capture: \(state.stage)")
            report.append("  removed: \(state.removedCount)  freed: \(Fmt.bytes(state.freedBytes))  error: \(state.errorMessageKey ?? "-")")
            state.errorMessageKey = nil
            state.finishFlow()
            await wait(1)
        } else {
            report.append("!! fixture app com.oblivion.testapp NOT found in the list")
        }

        // chrome-like app with its data inside a vendor folder
        if let app = state.apps.first(where: { $0.bundleID == "com.acme.browser" }) {
            state.uninstall(app)
            await waitWhile(timeout: 40) { state.stage == .working }
            await wait(1.5)
            capture(dir, "vendor-1-review")
            report.append("")
            report.append("== Uninstall com.acme.browser (vendor folder) → stage \(state.stage)")
            listLeftovers(state, &report)
            state.setAllLeftovers(true)
            state.removeSelectedLeftovers()
            await waitWhile(timeout: 40) { state.stage == .working }
            await wait(1)
            report.append("  removed: \(state.removedCount)  error: \(state.errorMessageKey ?? "-")")
            state.errorMessageKey = nil
            state.finishFlow()
            await wait(1)
        } else {
            report.append("!! fixture app com.acme.browser NOT found in the list")
        }

        // dry run, nothing removed: what would a real browser uninstall find on this mac
        for bundleID in ["com.google.Chrome", "org.mozilla.firefox", "com.microsoft.edgemac"] {
            guard let app = state.apps.first(where: { $0.bundleID == bundleID }) else { continue }
            let found = LeftoverScanner.scan(name: app.name, bundleID: app.bundleID)
            report.append("")
            report.append("== Dry run \(app.name) (\(bundleID)): \(found.count) items")
            for item in found {
                report.append("  [\(item.confidence)] \(item.selected ? "x" : " ") \(item.path)")
            }
            if found.contains(where: \.selected) { report.append("!! dry run pre-selected items") }
            // a whole vendor folder (Application Support/Google, .../Microsoft) must never show up
            let vendors: Set<String> = ["google", "microsoft", "mozilla", "adobe", "apple"]
            for item in found where vendors.contains(URL(fileURLWithPath: item.path).lastPathComponent.lowercased()) {
                report.append("!! vendor folder offered: \(item.path)")
            }
        }

        // force uninstall of an app that's already deleted (only leftovers left)
        state.forceQuery = "com.oblivion.ghost"
        state.showForceSheet = true
        await wait(1.5)
        capture(dir, "force-1-sheet", sheet: true)
        state.forceUninstall()
        await waitWhile(timeout: 40) { state.stage == .working }
        await wait(1)
        capture(dir, "force-2-review")
        report.append("")
        report.append("== Force uninstall com.oblivion.ghost → stage \(state.stage)")
        listLeftovers(state, &report)
        state.selectCertainLeftovers()
        state.removeSelectedLeftovers()
        await waitWhile(timeout: 40) { state.stage == .working }
        await wait(1)
        report.append("  removed: \(state.removedCount)  error: \(state.errorMessageKey ?? "-")")
        state.errorMessageKey = nil
        state.finishFlow()

        state.navigate(.logs)
        await wait(1.5)
        capture(dir, "zz-logs-after")

        // narrowest window, the live panel should hide
        mainWindow?.setFrame(NSRect(x: 0, y: 0, width: 1180, height: 740), display: true)
        state.navigate(.dashboard)
        await wait(2)
        capture(dir, "zz-narrow-dashboard")
        state.navigate(.apps)
        await wait(2)
        capture(dir, "zz-narrow-apps")

        try? report.joined(separator: "\n").write(to: dir.appendingPathComponent("report.txt"),
                                                   atomically: true, encoding: .utf8)
        NSApp.terminate(nil)
    }

    private static func listLeftovers(_ state: AppState, _ report: inout [String]) {
        for item in state.leftovers {
            report.append("  [\(item.confidence)] \(item.selected ? "x" : " ") \(item.kind)  \(item.path)")
        }
        if state.leftovers.contains(where: \.selected) { report.append("!! leftovers were pre-selected") }
    }

    // junk cleaner has to list everything and tick nothing. opens a category and the
    // confirmation, then cancels (nothing gets deleted on the ci machine)
    private static func junkCheck(dir: URL, state: AppState, loc: Loc, report: inout [String]) async {
        guard let junk else {
            report.append("!! junk model not registered")
            return
        }
        state.navigate(.junk)
        junk.scan()
        await wait(0.5)
        await waitWhile(timeout: 60) { junk.scanning }
        await wait(0.8)
        report.append("")
        report.append("== Junk: \(junk.foundCount) items · \(Fmt.bytes(junk.foundSize)) · selected after scan: \(junk.selectedCount)")
        for category in junk.categories {
            report.append("  \(category.id): \(category.items.count) items · \(Fmt.bytes(category.size))\(category.unreadable ? " (unreadable)" : "")")
        }
        if junk.selectedCount != 0 { report.append("!! junk cleaner pre-selected items") }

        if let index = junk.categories.firstIndex(where: { !$0.items.isEmpty && !$0.unreadable }) {
            withAnimation { junk.categories[index].expanded = true }
            await wait(1)
            capture(dir, "junk-expanded")
            if let first = junk.categories[index].items.first {
                junk.categories[index].toggle(first.id)
                await wait(0.6)
                capture(dir, "junk-one-selected")
                state.confirm = ConfirmRequest(
                    title: loc.t("junk.confirmTitle", ["count": "\(junk.selectedCount)"]),
                    message: loc.t("junk.confirmBody", ["size": Fmt.bytes(junk.selectedSize)]),
                    details: junk.categories[index].selectedItems.map { "\($0.url.path) — \(Fmt.bytes($0.size))" },
                    confirmTitle: loc["action.clean"],
                    symbol: "sparkles"
                ) {}
                await wait(1.4)
                capture(dir, "junk-confirm", sheet: true)
                state.confirm = nil
                junk.categories[index].setAll(false)
            }
            junk.categories[index].expanded = false
            await wait(0.6)
        }
    }

    // fixture launch agent off and on again with launchctl
    private static func startupCheck(state: AppState, report: inout [String]) async {
        guard let startup else { return }
        state.navigate(.startup)
        startup.load()
        await wait(2)
        let label = "com.oblivion.testapp.helper"
        report.append("")
        guard let item = startup.items.first(where: { $0.label == label }) else {
            report.append("== Startup toggle: fixture agent not listed (\(startup.items.count) items)")
            return
        }
        startup.setEnabled(item, false)
        await wait(2)
        let off = LaunchItemsService.disabledLabels().contains(label)
        if let current = startup.items.first(where: { $0.label == label }) {
            startup.setEnabled(current, true)
        }
        await wait(2)
        let on = !LaunchItemsService.disabledLabels().contains(label)
        report.append("== Startup toggle \(label): off \(off ? "ok" : "FAILED") · back on \(on ? "ok" : "FAILED")")
        // not fatal, ci runners might not have a usable gui launchd domain
        if !off || !on { report.append("?? startup toggle did not work on this runner") }
    }

    private static var mainWindow: NSWindow? {
        NSApp.windows.first { $0.isVisible && $0.contentView != nil && $0.frame.width > 600 }
    }

    private static func capture(_ dir: URL, _ name: String, sheet: Bool = false) {
        let target = sheet ? (mainWindow?.attachedSheet ?? mainWindow) : mainWindow
        guard let window = target,
              let view = window.contentView,
              let rep = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { return }
        view.cacheDisplay(in: view.bounds, to: rep)
        if let data = rep.representation(using: .png, properties: [:]) {
            try? data.write(to: dir.appendingPathComponent(name + ".png"))
        }
    }

    private static func wait(_ seconds: Double) async {
        try? await Task.sleep(nanoseconds: UInt64(seconds * 1_000_000_000))
    }

    private static func waitWhile(timeout: Double, _ condition: () -> Bool) async {
        var waited = 0.0
        while condition() && waited < timeout {
            await wait(0.25)
            waited += 0.25
        }
    }
}
