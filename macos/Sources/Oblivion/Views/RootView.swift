import AppKit
import SwiftUI

struct RootView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @EnvironmentObject private var prefs: Prefs
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        HStack(spacing: 0) {
            IconRail()
            NavPanel()
            pageArea
            RightPanel()
        }
        .background(WindowBackdrop())
        .background(WindowConfigurator())
        .ignoresSafeArea()
        .onAppear {
            state.prefs = prefs
            state.loadApps()
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
                .transition(.asymmetric(insertion: .opacity.combined(with: .offset(y: 18)), removal: .opacity))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(RoundedRectangle(cornerRadius: 22, style: .continuous).fill(p.content))
        .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).stroke(p.stroke, lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
        .padding(.vertical, 12)
        .padding(.horizontal, 10)
    }

    @ViewBuilder
    private var pageView: some View {
        switch state.page {
        case .dashboard: DashboardView()
        case .apps: AppsView(storeOnly: false)
        case .storeApps: AppsView(storeOnly: true)
        case .monitor: MonitorView()
        case .browserExt: BrowserExtensionsView()
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

// MARK: - Icon rail

struct IconRail: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @EnvironmentObject private var prefs: Prefs
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 6) {
            LogoMark(size: 42)
                .padding(.top, 46)
                .padding(.bottom, 12)
            RailButton(symbol: "square.grid.2x2.fill", active: state.page == .dashboard, help: loc["nav.dashboard"]) {
                state.navigate(.dashboard)
            }
            RailButton(symbol: "square.stack.3d.up.fill", active: state.page == .apps, help: loc["nav.apps"]) {
                state.navigate(.apps)
            }
            RailButton(symbol: "bag.fill", active: state.page == .storeApps, help: loc["nav.store"]) {
                state.navigate(.storeApps)
            }
            RailButton(symbol: "scope", active: state.page == .hunter, help: loc["nav.hunter"]) {
                state.navigate(.hunter)
            }
            RailButton(symbol: "wrench.and.screwdriver.fill", active: Page.toolPages.contains(state.page), help: loc["nav.tools"]) {
                state.navigate(.tools)
            }
            Spacer()
            RailButton(symbol: themeSymbol, active: false, help: loc["rail.theme"]) {
                withAnimation(.easeInOut(duration: 0.35)) { prefs.cycleAppearance() }
            }
            RailButton(text: loc.isTurkish ? "TR" : "EN", active: false, help: loc["rail.language"]) {
                withAnimation(.easeInOut(duration: 0.25)) { loc.toggle() }
            }
            RailButton(symbol: "gearshape.fill", active: state.page == .settings, help: loc["nav.settings"]) {
                state.navigate(.settings)
            }
            .padding(.bottom, 16)
        }
        .frame(width: 68)
        .frame(maxHeight: .infinity)
        .background(p.rail)
    }

    private var themeSymbol: String {
        switch prefs.appearance {
        case "dark": return "moon.stars.fill"
        case "light": return "sun.max.fill"
        default: return "circle.lefthalf.filled"
        }
    }
}

struct RailButton: View {
    var symbol: String? = nil
    var text: String? = nil
    let active: Bool
    let help: String
    let action: () -> Void
    @State private var hover = false
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            ZStack {
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .fill(active ? AnyShapeStyle(Palette.brandGradient) : AnyShapeStyle(p.cardStrong.opacity(hover ? 1 : 0.6)))
                if let symbol {
                    Image(systemName: symbol)
                        .font(.system(size: 16, weight: .semibold))
                } else if let text {
                    Text(text).font(.system(size: 12, weight: .heavy, design: .rounded))
                }
            }
            .foregroundStyle(active ? Color.white : p.text)
            .frame(width: 44, height: 44)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .help(help)
        .scaleEffect(hover ? 1.07 : 1)
        .shadow(color: Palette.accent.opacity(active ? 0.5 : 0), radius: 10, y: 3)
        .animation(.spring(response: 0.25, dampingFraction: 0.7), value: hover)
        .animation(.spring(response: 0.3, dampingFraction: 0.8), value: active)
        .onHover { hover = $0 }
    }
}

// MARK: - Labelled navigation + action card

struct NavPanel: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    private let items: [(page: Page, symbol: String, key: String)] = [
        (.dashboard, "square.grid.2x2", "nav.dashboard"),
        (.apps, "square.stack.3d.up", "nav.apps"),
        (.storeApps, "bag", "nav.store"),
        (.monitor, "binoculars", "nav.monitor"),
        (.browserExt, "puzzlepiece.extension", "nav.browser"),
        (.logs, "list.bullet.rectangle", "nav.logs"),
        (.hunter, "scope", "nav.hunter"),
        (.tools, "wrench.and.screwdriver", "nav.tools"),
        (.settings, "gearshape", "nav.settings"),
    ]

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 0) {
            VStack(alignment: .leading, spacing: 3) {
                Text("OBLIVION")
                    .font(.system(size: 19, weight: .heavy, design: .rounded))
                    .tracking(2.5)
                    .foregroundStyle(p.text)
                Text(loc["brand.by"])
                    .font(.system(size: 10.5, weight: .medium))
                    .foregroundStyle(p.subtext)
            }
            .padding(.top, 46)
            .padding(.horizontal, 6)
            .padding(.bottom, 16)

            ActionCard()

            ScrollView(.vertical, showsIndicators: false) {
                VStack(spacing: 3) {
                    ForEach(items, id: \.page) { item in
                        NavRow(symbol: item.symbol, title: loc[item.key], active: isActive(item.page)) {
                            state.navigate(item.page)
                        }
                    }
                }
            }
        }
        .padding(.horizontal, 12)
        .frame(width: 232)
        .frame(maxHeight: .infinity, alignment: .top)
        .background(p.panel)
    }

    private func isActive(_ page: Page) -> Bool {
        page == .tools ? Page.toolPages.contains(state.page) : state.page == page
    }
}

struct NavRow: View {
    let symbol: String
    let title: String
    let active: Bool
    let action: () -> Void
    @State private var hover = false
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            HStack(spacing: 12) {
                Image(systemName: symbol)
                    .font(.system(size: 14, weight: .medium))
                    .frame(width: 20)
                    .foregroundStyle(active ? Palette.accent2 : p.subtext)
                Text(title)
                    .font(.system(size: 13, weight: active ? .semibold : .regular))
                Spacer()
            }
            .foregroundStyle(active ? Color.white : p.text)
            .padding(.horizontal, 12)
            .padding(.vertical, 9)
            .background(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(active ? p.navSelected : (hover ? p.cardStrong : Color.clear))
            )
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hover = $0 }
        .animation(.easeOut(duration: 0.15), value: hover)
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

            HStack(spacing: 8) {
                Button {
                    state.forceQuery = state.selectedApp?.bundleID ?? state.selectedApp?.name ?? ""
                    state.showForceSheet = true
                } label: {
                    Text(loc["action.force"]).lineLimit(1).frame(maxWidth: .infinity)
                }
                .buttonStyle(OBButtonStyle(kind: .secondary))
                .disabled(state.stage != .browsing)

                Menu {
                    if let app = state.selectedApp {
                        AppCommands(app: app, includeUninstall: false)
                    }
                } label: {
                    Text(loc["action.more"]).lineLimit(1)
                }
                .menuStyle(.borderlessButton)
                .menuIndicator(.visible)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 6)
                .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(p.cardStrong))
                .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(p.stroke, lineWidth: 1))
                .disabled(!canAct)
            }

            Text(state.selectedApp?.name ?? loc["action.hint"])
                .font(.system(size: 11))
                .foregroundStyle(p.subtext)
                .lineLimit(1)
                .truncationMode(.middle)
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 16, style: .continuous).fill(p.card))
        .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).stroke(p.stroke, lineWidth: 1))
        .padding(.bottom, 14)
    }
}

/// The "Other commands" set, shared by the action-card menu and right-click menus.
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

// MARK: - Right info panel

struct RightPanel: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        ScrollView(.vertical, showsIndicators: false) {
            VStack(alignment: .leading, spacing: 16) {
                ProfileCard()
                Divider().overlay(p.stroke)
                systemSection(p)
                Divider().overlay(p.stroke)
                activitySection(p)
                Text("Oblivion 2.0 · macOS")
                    .font(.system(size: 10.5))
                    .foregroundStyle(p.faint)
                    .frame(maxWidth: .infinity)
                    .padding(.top, 6)
            }
            .padding(.top, 46)
            .padding(.horizontal, 18)
            .padding(.bottom, 18)
        }
        .frame(width: 268)
        .frame(maxHeight: .infinity)
        .background(p.right)
    }

    private func systemSection(_ p: Palette) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            Text(loc["right.system"]).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text)
            InfoRow(symbol: "apple.logo", label: "macOS", value: SystemInfo.macOSVersion)
            InfoRow(symbol: "cpu", label: loc["right.chip"], value: SystemInfo.chip)
            InfoRow(symbol: "memorychip", label: loc["right.memory"], value: SystemInfo.memory)
            VStack(alignment: .leading, spacing: 5) {
                HStack {
                    Label(loc["right.disk"], systemImage: "internaldrive")
                        .font(.system(size: 11.5))
                        .foregroundStyle(p.subtext)
                    Spacer()
                    Text(Fmt.bytes(state.diskFree))
                        .font(.system(size: 11.5, weight: .semibold))
                        .foregroundStyle(p.text)
                }
                ProgressView(value: usedFraction)
                    .progressViewStyle(.linear)
                    .tint(Palette.accent)
                Text(loc.t("right.diskOf", ["total": Fmt.bytes(state.diskTotal)]))
                    .font(.system(size: 10.5))
                    .foregroundStyle(p.faint)
            }
        }
    }

    private var usedFraction: Double {
        guard state.diskTotal > 0 else { return 0 }
        return min(1, max(0, Double(state.diskTotal - state.diskFree) / Double(state.diskTotal)))
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
                        Circle().fill(Palette.brandGradient).frame(width: 7, height: 7).padding(.top, 5)
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

/// Daccord-style profile card with a rotating gradient ring and the signature.
struct ProfileCard: View {
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var spin = false

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 6) {
            ZStack {
                Circle()
                    .stroke(
                        AngularGradient(colors: [Color(hex: 0x6E5BFF), Color(hex: 0xB45BFF), Color(hex: 0x3A8DFF), Color(hex: 0x6E5BFF)],
                                        center: .center),
                        lineWidth: 3.5
                    )
                    .frame(width: 100, height: 100)
                    .rotationEffect(.degrees(spin ? 360 : 0))
                    .animation(.linear(duration: 9).repeatForever(autoreverses: false), value: spin)
                Circle()
                    .fill(Palette.brandGradient)
                    .frame(width: 86, height: 86)
                Text("AG")
                    .font(.system(size: 32, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)
            }
            .onAppear { spin = true }

            SignatureText(size: 32)
                .padding(.top, 4)
            Text("@anilgul")
                .font(.system(size: 11.5))
                .foregroundStyle(p.subtext)
            Chip(text: loc["right.developer"], color: Palette.accent2)
                .padding(.top, 2)
        }
        .frame(maxWidth: .infinity)
    }
}
