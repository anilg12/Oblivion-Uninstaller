import AppKit
import SwiftUI

struct RootView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @EnvironmentObject private var prefs: Prefs
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        GeometryReader { geo in
            // hide the live panel on narrow windows
            let showRightPanel = prefs.showLivePanel && geo.size.width >= 1300
            HStack(spacing: 0) {
                NavPanel()
                pageArea
                if showRightPanel {
                    RightPanel()
                        .transition(.move(edge: .trailing).combined(with: .opacity))
                }
            }
            .animation(.easeOut(duration: 0.25), value: showRightPanel)
            .sheet(isPresented: $state.showAbout) {
                AboutView()
                    .environmentObject(loc)
                    .environmentObject(prefs)
            }
        }
        .background(WindowBackdrop())
        .background(WindowConfigurator())
        .ignoresSafeArea()
        .onAppear {
            state.prefs = prefs
            state.loadApps()
            SnapshotRunner.startIfRequested(state: state, prefs: prefs, loc: loc)
        }
        .alert(loc["confirm.title"], isPresented: confirmBinding, presenting: state.confirmApp) { app in
            Button(loc["action.uninstall"], role: .destructive) { state.uninstall(app) }
            Button(loc["action.cancel"], role: .cancel) { state.confirmApp = nil }
        } message: { app in
            Text(loc.t("confirm.message", ["name": app.name]))
        }
        .alert(loc["error.title"], isPresented: errorBinding) {
            Button(loc["action.ok"], role: .cancel) { state.errorMessageKey = nil }
        } message: {
            Text(loc[state.errorMessageKey ?? "error.removeFailed"])
        }
        .sheet(isPresented: $state.showForceSheet) {
            ForceUninstallSheet()
                .environmentObject(state)
                .environmentObject(loc)
        }
    }

    private var confirmBinding: Binding<Bool> {
        Binding(get: { state.confirmApp != nil }, set: { if !$0 { state.confirmApp = nil } })
    }

    private var errorBinding: Binding<Bool> {
        Binding(get: { state.errorMessageKey != nil }, set: { if !$0 { state.errorMessageKey = nil } })
    }

    private var pageArea: some View {
        let p = Palette(scheme)
        return ZStack {
            pageView
                .id(state.page)
                .transition(.asymmetric(insertion: .opacity.combined(with: .offset(y: 6)), removal: .opacity))
        }
        .padding(.top, 14)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(p.content)
        // all destructive actions confirm here with the full list
        .sheet(item: $state.confirm) { request in
            ConfirmSheet(request: request)
                .environmentObject(loc)
        }
    }

    @ViewBuilder
    private var pageView: some View {
        switch state.page {
        case .dashboard: DashboardView()
        case .apps: AppsView(storeOnly: false)
        case .storeApps: AppsView(storeOnly: true)
        case .monitor: MonitorView()
        case .browserExt: BrowserExtensionsView()
        case .systemMonitor: SystemMonitorView()
        case .logs: LogsView()
        case .hunter: HunterView()
        case .tools: ToolsView()
        case .startup: StartupView()
        case .junk: JunkView()
        case .largeFiles: LargeFilesView()
        case .shredder: ShredderView()
        case .history: HistoryView()
        case .settings: SettingsView()
        }
    }
}

// small square buttons in the sidebar footer (theme, language, about)
struct RailButton: View {
    var symbol: String? = nil
    var text: String? = nil
    let active: Bool
    let help: String
    var namespace: Namespace.ID? = nil
    let action: () -> Void
    @State private var hover = false
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            ZStack {
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(active ? p.navSelected : (hover ? p.navHover : Color.clear))
                if let symbol {
                    Image(systemName: symbol)
                        .font(.system(size: 13, weight: .regular))
                } else if let text {
                    Text(text).font(.system(size: 11, weight: .semibold))
                }
            }
            .foregroundStyle(hover || active ? p.text : p.subtext)
            .frame(width: 32, height: 32)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .help(help)
        .animation(.easeOut(duration: 0.12), value: hover)
        .onHover { hover = $0 }
    }
}

// MARK: - Labelled navigation + action card

struct NavPanel: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @Namespace private var selection

    private struct Item: Identifiable {
        let page: Page
        let symbol: String
        let key: String
        var isNew = false
        var id: Page { page }
    }

    private let mainItems: [Item] = [
        Item(page: .dashboard, symbol: "square.grid.2x2", key: "nav.dashboard"),
        Item(page: .apps, symbol: "square.stack.3d.up", key: "nav.apps"),
        Item(page: .storeApps, symbol: "bag", key: "nav.store"),
        Item(page: .systemMonitor, symbol: "gauge.with.dots.needle.67percent", key: "nav.systemMonitor", isNew: true),
    ]

    private let moreItems: [Item] = [
        Item(page: .tools, symbol: "wrench.and.screwdriver", key: "nav.tools"),
        Item(page: .browserExt, symbol: "puzzlepiece.extension", key: "nav.browser"),
        Item(page: .monitor, symbol: "binoculars", key: "nav.monitor"),
        Item(page: .hunter, symbol: "scope", key: "nav.hunter"),
        Item(page: .logs, symbol: "list.bullet.rectangle", key: "nav.logs"),
    ]

    @EnvironmentObject private var prefs: Prefs

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .center, spacing: 10) {
                LogoMark(size: 30)
                VStack(alignment: .leading, spacing: 1) {
                    Text("Oblivion")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(p.text)
                    Text(loc.t("about.version", ["version": AppInfo.version]))
                        .font(.system(size: 11))
                        .foregroundStyle(p.faint)
                }
                Spacer(minLength: 4)
            }
            .padding(.top, 50)
            .padding(.horizontal, 8)
            .padding(.bottom, 18)

            ActionCard()

            ScrollView(.vertical, showsIndicators: false) {
                VStack(alignment: .leading, spacing: 2) {
                    groupTitle(loc.isTurkish ? "GENEL" : "GENERAL", p)
                    ForEach(mainItems) { row($0) }
                    groupTitle(loc.isTurkish ? "TEMİZLİK VE İZLEME" : "CLEANING & TRACKING", p)
                    ForEach(moreItems) { row($0) }
                }
            }

            Divider().overlay(p.stroke).padding(.vertical, 8)
            NavRow(symbol: "gearshape", title: loc["nav.settings"], active: state.page == .settings, namespace: selection) {
                state.navigate(.settings)
            }
            HStack(spacing: 2) {
                RailButton(symbol: themeSymbol, active: false, help: loc["rail.theme"]) {
                    withAnimation(.easeInOut(duration: 0.25)) { prefs.cycleAppearance() }
                }
                RailButton(text: loc.isTurkish ? "TR" : "EN", active: false, help: loc["rail.language"]) {
                    withAnimation(.easeInOut(duration: 0.2)) { loc.toggle() }
                }
                Spacer()
                InfoButton(help: loc["rail.about"]) { state.showAbout = true }
                    .padding(.trailing, 6)
            }
            .padding(.top, 6)
            .padding(.bottom, 14)
        }
        .padding(.horizontal, 12)
        .frame(width: 248)
        .frame(maxHeight: .infinity, alignment: .top)
        .background(p.rail)
        .overlay(alignment: .trailing) { Rectangle().fill(p.stroke).frame(width: 1) }
    }

    private func row(_ item: Item) -> some View {
        NavRow(symbol: item.symbol, title: loc[item.key], active: isActive(item.page),
               badge: item.isNew ? (loc.isTurkish ? "YENİ" : "NEW") : nil, namespace: selection) {
            state.navigate(item.page)
        }
    }

    private func groupTitle(_ text: String, _ p: Palette) -> some View {
        Text(text)
            .font(.system(size: 10.5, weight: .semibold))
            .tracking(0.6)
            .foregroundStyle(p.faint)
            .padding(.horizontal, 10)
            .padding(.top, 16)
            .padding(.bottom, 6)
    }

    private var themeSymbol: String {
        switch prefs.appearance {
        case "dark": return "moon"
        case "light": return "sun.max"
        default: return "circle.lefthalf.filled"
        }
    }

    private func isActive(_ page: Page) -> Bool {
        page == .tools ? Page.toolPages.contains(state.page) : state.page == page
    }
}

struct NavRow: View {
    let symbol: String
    let title: String
    let active: Bool
    var badge: String? = nil
    var namespace: Namespace.ID? = nil
    let action: () -> Void
    @State private var hover = false
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            HStack(spacing: 10) {
                Image(systemName: symbol)
                    .font(.system(size: 13, weight: .regular))
                    .frame(width: 18)
                    .foregroundStyle(active ? p.text : p.subtext)
                Text(title)
                    .font(.system(size: 13, weight: active ? .medium : .regular))
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                Spacer(minLength: 4)
                if let badge, !active {
                    Text(badge)
                        .font(.system(size: 9.5, weight: .semibold))
                        .foregroundStyle(p.accentText)
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(RoundedRectangle(cornerRadius: 5, style: .continuous).fill(p.accentSoft))
                }
            }
            .foregroundStyle(active ? p.text : p.subtext)
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background {
                ZStack {
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .fill(hover && !active ? p.navHover : Color.clear)
                    if active { selectionFill(p) }
                }
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hover = $0 }
        .animation(.easeOut(duration: 0.15), value: hover)
    }

    @ViewBuilder
    private func selectionFill(_ p: Palette) -> some View {
        let shape = RoundedRectangle(cornerRadius: 8, style: .continuous)
        if let namespace {
            shape.fill(p.navSelected)
                .overlay(shape.stroke(p.stroke, lineWidth: 1))
                .shadow(color: p.shadow, radius: 1, y: 1)
                .matchedGeometryEffect(id: "navSelection", in: namespace)
        } else {
            shape.fill(p.navSelected).overlay(shape.stroke(p.stroke, lineWidth: 1))
        }
    }
}

struct ActionCard: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    private var canAct: Bool {
        state.isOnAppList && state.selectedApp != nil && state.stage == .browsing
    }

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 8) {
            Button {
                state.requestUninstall(state.selectedApp)
            } label: {
                Label(loc["action.uninstall"], systemImage: "trash.fill")
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(OBButtonStyle(kind: .danger, large: true))
            .disabled(!canAct)

            Button {
                state.forceQuery = state.selectedApp?.bundleID ?? state.selectedApp?.name ?? ""
                state.showForceSheet = true
            } label: {
                Label(loc["action.force"], systemImage: "bolt.fill")
                    .lineLimit(1)
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(OBButtonStyle(kind: .secondary))
            .disabled(state.stage != .browsing)

            Menu {
                if let app = state.selectedApp {
                    AppCommands(app: app, includeUninstall: false)
                }
            } label: {
                Label(loc["action.more"], systemImage: "ellipsis.circle")
                    .font(.system(size: 13, weight: .medium))
                    .lineLimit(1)
            }
            .menuStyle(.borderlessButton)
            .menuIndicator(.visible)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 6)
            .background(RoundedRectangle(cornerRadius: 8, style: .continuous).fill(p.card))
            .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(p.strokeStrong, lineWidth: 1))
            .disabled(!canAct)

            Text(state.selectedApp?.name ?? loc["action.hint"])
                .font(.system(size: 11))
                .foregroundStyle(p.faint)
                .lineLimit(1)
                .truncationMode(.middle)
                .padding(.top, 2)
        }
        .padding(.horizontal, 2)
        .padding(.bottom, 4)
    }
}

// "Other commands", used by the action card menu and the right click menus
struct AppCommands: View {
    let app: InstalledApp
    var includeUninstall = true
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc

    var body: some View {
        if includeUninstall {
            Button { state.requestUninstall(app) } label: {
                Label(loc["action.uninstall"], systemImage: "trash")
            }
            Button {
                state.forceQuery = app.bundleID ?? app.name
                state.showForceSheet = true
            } label: {
                Label(loc["action.force"], systemImage: "bolt.trianglebadge.exclamationmark")
            }
            Divider()
        }
        Button { state.open(app) } label: { Label(loc["cmd.open"], systemImage: "arrow.up.forward.app") }
        Button { state.reveal(app) } label: { Label(loc["cmd.reveal"], systemImage: "folder") }
        Button { state.showPackageContents(app) } label: { Label(loc["cmd.contents"], systemImage: "shippingbox") }
        Button { state.getInfo(app) } label: { Label(loc["cmd.info"], systemImage: "info.circle") }
        Divider()
        Button { state.copyDetails(app) } label: { Label(loc["cmd.copy"], systemImage: "doc.on.doc") }
        Button { state.searchWeb(app) } label: { Label(loc["cmd.web"], systemImage: "globe") }
    }
}

// MARK: - Right panel: live system status

struct RightPanel: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var monitor: SystemMonitor
    @EnvironmentObject private var loc: Loc
    @EnvironmentObject private var prefs: Prefs
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        let s = monitor.snapshot
        ScrollView(.vertical, showsIndicators: false) {
            VStack(alignment: .leading, spacing: 16) {
                HStack(spacing: 8) {
                    Text(loc["live.title"]).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text)
                    LiveDot()
                    Spacer()
                    Button { state.navigate(.systemMonitor) } label: {
                        Text(loc.isTurkish ? "Ayrıntılar" : "Details")
                            .font(.system(size: 11.5, weight: .medium))
                    }
                    .buttonStyle(.plain)
                    .foregroundStyle(p.accentText)
                    .help(loc["live.open"])
                }

                HStack(spacing: 6) {
                    MiniGauge(title: loc["live.cpu"], value: s.cpu / 100, text: Fmt.percent(s.cpu, loc),
                              size: 62, animate: !prefs.calmMotion)
                    MiniGauge(title: loc["live.memory"], value: s.memPercent / 100, text: Fmt.percent(s.memPercent, loc),
                              size: 62, animate: !prefs.calmMotion)
                    MiniGauge(title: loc["live.temp"], value: s.temperature.map { $0 / 100 } ?? s.thermalState.fraction,
                              text: s.temperature.map { "\(Int($0.rounded()))°" } ?? loc[s.thermalState.shortKey],
                              size: 62, animate: !prefs.calmMotion)
                }

                VStack(alignment: .leading, spacing: 9) {
                    HStack {
                        Label(loc["live.network"], systemImage: "arrow.up.arrow.down")
                            .font(.system(size: 11.5)).foregroundStyle(p.subtext)
                        Spacer()
                        Text("↓ \(Fmt.rate(s.netDown))   ↑ \(Fmt.rate(s.netUp))")
                            .font(.system(size: 11, weight: .medium).monospacedDigit())
                            .foregroundStyle(p.text)
                            .lineLimit(1)
                            .minimumScaleFactor(0.8)
                    }
                    Sparkline(values: s.netDownHistory)
                        .frame(height: 22)
                    diskRow(s, p)
                    if let battery = s.battery {
                        HStack {
                            Label(loc.t(battery.charging ? "live.batteryCharging" : "live.battery", ["percent": "\(battery.percent)"]),
                                  systemImage: battery.charging ? "battery.100.bolt" : "battery.75")
                                .font(.system(size: 11.5)).foregroundStyle(p.subtext)
                            Spacer()
                        }
                    }
                }

                Divider().overlay(p.stroke)
                systemSection(p)
                Divider().overlay(p.stroke)
                activitySection(p)
                Text("Oblivion \(AppInfo.version) · macOS")
                    .font(.system(size: 10.5))
                    .foregroundStyle(p.faint)
                    .frame(maxWidth: .infinity)
                    .padding(.top, 6)
            }
            .padding(.top, 50)
            .padding(.horizontal, 20)
            .padding(.bottom, 18)
        }
        .frame(width: 272)
        .frame(maxHeight: .infinity)
        .background(p.right)
        .overlay(alignment: .leading) { Rectangle().fill(p.stroke).frame(width: 1) }
        .onAppear { monitor.subscribe("right", detailed: false) }
        .onDisappear { monitor.unsubscribe("right") }
    }

    private func diskRow(_ s: SystemSnapshot, _ p: Palette) -> some View {
        let drive = s.drives.first { $0.path == "/" } ?? s.drives.first
        let free = drive?.free ?? state.diskFree
        let total = drive?.total ?? state.diskTotal
        let used = total > 0 ? min(1, max(0, Double(total - free) / Double(total))) : 0
        return VStack(alignment: .leading, spacing: 5) {
            HStack {
                Label(loc["live.disk"], systemImage: "internaldrive")
                    .font(.system(size: 11.5))
                    .foregroundStyle(p.subtext)
                Spacer()
                Text(Fmt.bytes(free))
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundStyle(p.text)
            }
            GradientProgressBar(value: used, height: 5)
            Text(loc.t("live.diskFree", ["free": Fmt.bytes(free), "total": Fmt.bytes(total)]))
                .font(.system(size: 10.5))
                .foregroundStyle(p.faint)
        }
    }

    private func systemSection(_ p: Palette) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            Text(loc["right.system"]).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text)
            InfoRow(symbol: "apple.logo", label: "macOS", value: SystemInfo.macOSVersion)
            InfoRow(symbol: "cpu", label: loc["right.chip"], value: SystemInfo.chip)
            InfoRow(symbol: "memorychip", label: loc["right.memory"], value: SystemInfo.memory)
        }
    }

    private func activitySection(_ p: Palette) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            Text(loc["right.activity"]).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text)
            if state.recentActivity.isEmpty {
                Text(loc["dash.noActivity"])
                    .font(.system(size: 11.5))
                    .foregroundStyle(p.faint)
            } else {
                ForEach(state.recentActivity) { entry in
                    HStack(alignment: .top, spacing: 9) {
                        Circle().fill(p.faint).frame(width: 5, height: 5).padding(.top, 6)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(loc[entry.actionKey])
                                .font(.system(size: 12, weight: .semibold))
                                .foregroundStyle(p.text)
                            Text(entry.detail)
                                .font(.system(size: 11))
                                .foregroundStyle(p.subtext)
                                .lineLimit(1)
                            Text(Fmt.dateTime(entry.date))
                                .font(.system(size: 10))
                                .foregroundStyle(p.faint)
                        }
                    }
                }
            }
        }
    }
}

struct InfoRow: View {
    let symbol: String
    let label: String
    let value: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        HStack(spacing: 8) {
            Image(systemName: symbol).frame(width: 16).foregroundStyle(p.subtext)
            Text(label).foregroundStyle(p.subtext)
            Spacer(minLength: 6)
            Text(value).foregroundStyle(p.text).fontWeight(.medium).lineLimit(1).minimumScaleFactor(0.75)
        }
        .font(.system(size: 11.5))
    }
}
