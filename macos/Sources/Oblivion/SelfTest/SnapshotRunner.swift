import AppKit
import SwiftUI

/// CI self-test. Only active when the OBLIVION_SNAPSHOT_DIR environment variable is set
/// (see ci-smoke-test.sh): walks every page, saves a PNG of the window for each, runs a
/// normal and a forced uninstall on fixture apps, writes report.txt and quits.
@MainActor
enum SnapshotRunner {
    private static var started = false

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
        loc.lang = "tr"
        await wait(3)
        await waitWhile(timeout: 30) { state.isLoadingApps }

        if let window = mainWindow {
            report.append("window: \(window.frame)  screen: \(window.screen?.frame ?? .zero)")
        }
        report.append("apps found: \(state.apps.count)")
        report.append("full disk access: \(state.hasFullDiskAccess)")

        let pages: [Page] = [.dashboard, .apps, .storeApps, .monitor, .browserExt, .logs, .hunter,
                             .tools, .startup, .junk, .largeFiles, .shredder, .history, .settings]
        for (index, page) in pages.enumerated() {
            state.navigate(page)
            if page == .apps { state.selectedAppID = state.apps.first?.id }
            let slow: Set<Page> = [.browserExt, .junk, .startup, .apps]
            await wait(slow.contains(page) ? 3 : 1.5)
            capture(dir, String(format: "%02d-", index + 1) + page.rawValue + "-dark")
        }

        prefs.appearance = "light"
        for page in [Page.dashboard, .apps, .tools, .junk] {
            state.navigate(page)
            await wait(1.5)
            capture(dir, "light-" + page.rawValue)
        }
        prefs.appearance = "dark"
        loc.lang = "en"
        state.navigate(.dashboard)
        await wait(1.5)
        capture(dir, "en-dashboard")
        loc.lang = "tr"

        // Normal uninstall of the fixture app.
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
            capture(dir, "flow-3-review")
            report.append("")
            report.append("== Uninstall com.oblivion.testapp → stage \(state.stage), app removed: \(state.appRemoved)")
            for item in state.leftovers {
                report.append("  [\(item.confidence)] \(item.selected ? "x" : " ") \(item.kind)  \(item.path)")
            }
            state.removeSelectedLeftovers()
            await waitWhile(timeout: 40) { state.stage == .working }
            await wait(1.5)
            capture(dir, "flow-4-done")
            report.append("  removed: \(state.removedCount)  freed: \(Fmt.bytes(state.freedBytes))  error: \(state.errorMessageKey ?? "-")")
            state.errorMessageKey = nil
            state.finishFlow()
            await wait(1)
        } else {
            report.append("!! fixture app com.oblivion.testapp NOT found in the list")
        }

        // Force uninstall of an app that is already gone (only leftovers remain).
        state.forceQuery = "com.oblivion.ghost"
        state.showForceSheet = true
        await wait(1.5)
        capture(dir, "force-1-sheet")
        state.forceUninstall()
        await waitWhile(timeout: 40) { state.stage == .working }
        await wait(1)
        capture(dir, "force-2-review")
        report.append("")
        report.append("== Force uninstall com.oblivion.ghost → stage \(state.stage)")
        for item in state.leftovers {
            report.append("  [\(item.confidence)] \(item.selected ? "x" : " ") \(item.kind)  \(item.path)")
        }
        state.removeSelectedLeftovers()
        await waitWhile(timeout: 40) { state.stage == .working }
        await wait(1)
        report.append("  removed: \(state.removedCount)  error: \(state.errorMessageKey ?? "-")")
        state.errorMessageKey = nil
        state.finishFlow()

        state.navigate(.logs)
        await wait(1.5)
        capture(dir, "zz-logs-after")

        try? report.joined(separator: "\n").write(to: dir.appendingPathComponent("report.txt"),
                                                   atomically: true, encoding: .utf8)
        NSApp.terminate(nil)
    }

    private static var mainWindow: NSWindow? {
        NSApp.windows.first { $0.isVisible && $0.contentView != nil && $0.frame.width > 600 }
    }

    private static func capture(_ dir: URL, _ name: String) {
        guard let window = NSApp.windows.first(where: { $0.isVisible && $0.isKeyWindow && $0.contentView != nil })
                ?? mainWindow,
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
