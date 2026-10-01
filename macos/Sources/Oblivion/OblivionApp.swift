import AppKit
import SwiftUI

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

@main
struct OblivionApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate

    @StateObject private var loc = Loc()
    @StateObject private var prefs = Prefs()
    @StateObject private var state = AppState()
    @StateObject private var monitor = MonitorModel()
    @StateObject private var browser = BrowserExtensionsModel()
    @StateObject private var logs = LogsModel()
    @StateObject private var hunter = HunterModel()
    @StateObject private var startup = StartupModel()
    @StateObject private var junk = JunkModel()
    @StateObject private var largeFiles = LargeFilesModel()
    @StateObject private var shredder = ShredderModel()
    @StateObject private var history = HistoryModel()

    var body: some Scene {
        Window("Oblivion", id: "main") {
            RootView()
                .environmentObject(loc)
                .environmentObject(prefs)
                .environmentObject(state)
                .environmentObject(monitor)
                .environmentObject(browser)
                .environmentObject(logs)
                .environmentObject(hunter)
                .environmentObject(startup)
                .environmentObject(junk)
                .environmentObject(largeFiles)
                .environmentObject(shredder)
                .environmentObject(history)
                .preferredColorScheme(prefs.colorScheme)
                .frame(minWidth: 1180, minHeight: 720)
        }
        .windowStyle(.hiddenTitleBar)
        .windowResizability(.contentMinSize)
        .defaultSize(width: 1380, height: 860)
        .commands {
            CommandGroup(replacing: .newItem) {}
        }
    }
}
