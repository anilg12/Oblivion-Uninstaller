import AppKit
import SwiftUI

// MARK: - Monitored installs (baseline / compare)

@MainActor
final class MonitorModel: ObservableObject {
    @Published var changes: [MonitorChange] = []
    @Published var busy = false
    @Published var compared = false
    @Published var hasBaseline = MonitorService.hasBaseline
    @Published var baselineDate: Date? = MonitorService.baselineDate
    @Published var lastBaselineCount: Int?

    func takeBaseline() {
        busy = true
        Task {
            let count = await Task.detached { MonitorService.takeBaseline() }.value
            self.lastBaselineCount = count
            self.hasBaseline = true
            self.baselineDate = Date()
            self.changes = []
            self.compared = false
            self.busy = false
            ActivityLog.append("log.baseline", "\(count)")
        }
    }

    func compare() {
        busy = true
        Task {
            let found = await Task.detached { MonitorService.compare() }.value
            withAnimation(.spring) { self.changes = found }
            self.compared = true
            self.busy = false
        }
    }

    func removeSelected() {
        let chosen = changes.filter(\.selected)
        guard !chosen.isEmpty else { return }
        busy = true
        Task {
            let count = await Task.detached { MonitorService.remove(chosen) }.value
            ActivityLog.append("log.monitorRemoved", "\(count)")
            self.busy = false
            self.compare()
        }
    }
}

struct MonitorView: View {
    @EnvironmentObject private var model: MonitorModel
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 16) {
            PageHeader(loc["nav.monitor"], loc["mon.subtitle"]) {
                if model.busy { ProgressView().controlSize(.small) }
            }

            HStack(spacing: 12) {
                step(1, "camera.viewfinder", loc["mon.step1"], [0x6E5BFF, 0xB45BFF], p)
                step(2, "arrow.down.app", loc["mon.step2"], [0x3A8DFF, 0x6F5BFF], p)
                step(3, "arrow.left.arrow.right", loc["mon.step3"], [0x18C29C, 0x2E8BFF], p)
            }

            HStack(spacing: 10) {
                Button { model.takeBaseline() } label: { Label(loc["mon.baseline"], systemImage: "camera.fill") }
                    .buttonStyle(OBButtonStyle(kind: .primary))
                Button { model.compare() } label: { Label(loc["mon.compare"], systemImage: "arrow.left.arrow.right") }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    .disabled(!model.hasBaseline)
                Spacer()
                if let date = model.baselineDate {
                    Text(loc.t("mon.baselineAt", ["date": Fmt.dateTime(date)]))
                        .font(.system(size: 11.5)).foregroundStyle(p.subtext)
                }
            }
            .disabled(model.busy)

            if model.compared {
                results(p)
            } else {
                EmptyStateView(symbol: "binoculars", text: model.hasBaseline ? loc["mon.ready"] : loc["mon.noBaseline"])
                Spacer()
            }
        }
        .padding(24)
    }

    private func step(_ n: Int, _ symbol: String, _ text: String, _ colors: [UInt32], _ p: Palette) -> some View {
        GlassCard(padding: 14) {
            HStack(spacing: 12) {
                GradientBadge(symbol: symbol, colors: colors, size: 38)
                VStack(alignment: .leading, spacing: 2) {
                    Text("\(n)").font(.system(size: 11, weight: .heavy)).foregroundStyle(Palette.accent2)
                    Text(text).font(.system(size: 12)).foregroundStyle(p.text).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
    }

    @ViewBuilder
    private func results(_ p: Palette) -> some View {
        if model.changes.isEmpty {
            EmptyStateView(symbol: "checkmark.circle", text: loc["mon.noChanges"])
            Spacer()
        } else {
            Text(loc.t("mon.found", ["count": "\(model.changes.count)"]))
                .font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text)
            ScrollView {
                LazyVStack(spacing: 6) {
                    ForEach($model.changes) { $change in
                        HStack(spacing: 10) {
                            Toggle("", isOn: $change.selected).toggleStyle(.checkbox).labelsHidden()
                            Image(systemName: icon(change.kind)).frame(width: 18).foregroundStyle(p.subtext)
                            VStack(alignment: .leading, spacing: 1) {
                                Text((change.path as NSString).lastPathComponent)
                                    .font(.system(size: 12.5, weight: .medium)).foregroundStyle(p.text).lineLimit(1)
                                Text(change.path).font(.system(size: 10.5)).foregroundStyle(p.faint)
                                    .lineLimit(1).truncationMode(.middle)
                            }
                            Spacer()
                            Chip(text: loc["mon.kind." + change.kind.rawValue], color: Palette.accent)
                        }
                        .padding(.horizontal, 12).padding(.vertical, 8)
                        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(p.card))
                    }
                }
            }
            HStack {
                Spacer()
                Button { model.removeSelected() } label: { Label(loc["mon.remove"], systemImage: "trash.fill") }
                    .buttonStyle(OBButtonStyle(kind: .danger))
                    .disabled(model.busy || !model.changes.contains(where: { $0.selected }))
            }
        }
    }

    private func icon(_ kind: MonitorChange.Kind) -> String {
        switch kind {
        case .app: return "app.badge"
        case .folder: return "folder"
        case .preferences: return "slider.horizontal.3"
        case .launchItem: return "power"
        case .receipt: return "doc.text.magnifyingglass"
        }
    }
}

// MARK: - Browser extensions

@MainActor
final class BrowserExtensionsModel: ObservableObject {
    @Published var items: [BrowserExtension] = []
    @Published var busy = false
    @Published var loaded = false

    func load() {
        busy = true
        Task {
            let found = await Task.detached { BrowserExtensionsService.scan() }.value
            withAnimation(.spring) { self.items = found }
            self.busy = false
            self.loaded = true
        }
    }

    func remove(_ ext: BrowserExtension) {
        Task {
            let ok = await Task.detached { BrowserExtensionsService.remove(ext) }.value
            if ok {
                ActivityLog.append("log.extension", "\(ext.name) · \(ext.browser)")
                withAnimation(.spring) { self.items.removeAll { $0.id == ext.id } }
            }
        }
    }
}

struct BrowserExtensionsView: View {
    @EnvironmentObject private var model: BrowserExtensionsModel
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        let browsers = Array(Set(model.items.map(\.browser))).sorted()
        VStack(alignment: .leading, spacing: 16) {
            PageHeader(loc["nav.browser"], loc["br.subtitle"]) {
                Button { model.load() } label: { Label(loc["action.refresh"], systemImage: "arrow.clockwise") }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
            }
            Text(loc["br.note"]).font(.system(size: 11.5)).foregroundStyle(p.subtext)

            if model.busy && model.items.isEmpty {
                LoadingView(text: loc["br.loading"])
            } else if model.items.isEmpty {
                EmptyStateView(symbol: "puzzlepiece.extension", text: loc["br.empty"])
                Spacer()
            } else {
                ScrollView {
                    VStack(alignment: .leading, spacing: 18) {
                        ForEach(browsers, id: \.self) { browser in
                            browserSection(browser, p)
                        }
                    }
                }
            }
        }
        .padding(24)
        .onAppear { if !model.loaded { model.load() } }
    }

    private func browserSection(_ browser: String, _ p: Palette) -> some View {
        let items = model.items.filter { $0.browser == browser }
        return VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 10) {
                BrowserIcon(bundleID: items.first?.browserBundleID ?? "", size: 26)
                Text(browser).font(.system(size: 15, weight: .semibold)).foregroundStyle(p.text)
                Chip(text: "\(items.count)", color: Palette.accent)
            }
            ForEach(items) { ext in
                HStack(spacing: 12) {
                    Image(systemName: "puzzlepiece.extension.fill")
                        .foregroundStyle(Palette.brandGradient)
                        .frame(width: 22)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(ext.name).font(.system(size: 13, weight: .medium)).foregroundStyle(p.text).lineLimit(1)
                        Text([ext.version, ext.profile].filter { !$0.isEmpty }.joined(separator: " · "))
                            .font(.system(size: 11)).foregroundStyle(p.faint).lineLimit(1)
                    }
                    Spacer()
                    Button {
                        NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: ext.path)])
                    } label: { Image(systemName: "folder") }
                        .buttonStyle(OBButtonStyle(kind: .secondary))
                        .help(loc["cmd.reveal"])
                    if ext.removable {
                        Button(loc["action.remove"]) { model.remove(ext) }
                            .buttonStyle(OBButtonStyle(kind: .danger))
                    } else {
                        Chip(text: loc["br.managedBySafari"], color: Color.gray)
                    }
                }
                .padding(.horizontal, 12).padding(.vertical, 9)
                .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
            }
        }
    }
}

struct BrowserIcon: View {
    let bundleID: String
    var size: CGFloat = 24

    var body: some View {
        if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleID) {
            AppIconView(path: url.path, size: size)
        } else {
            Image(systemName: "globe").font(.system(size: size * 0.7)).frame(width: size, height: size)
        }
    }
}

// MARK: - Logs database

@MainActor
final class LogsModel: ObservableObject {
    @Published var entries: [LogEntry] = []

    func load() { entries = ActivityLog.all() }

    func clear() {
        ActivityLog.clear()
        withAnimation(.spring) { entries = [] }
    }
}

struct LogsView: View {
    @EnvironmentObject private var model: LogsModel
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var confirmClear = false

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 16) {
            PageHeader(loc["nav.logs"], loc["logs.subtitle"]) {
                HStack(spacing: 8) {
                    Button { model.load() } label: { Image(systemName: "arrow.clockwise") }
                        .buttonStyle(OBButtonStyle(kind: .secondary))
                    Button(loc["logs.clear"]) { confirmClear = true }
                        .buttonStyle(OBButtonStyle(kind: .danger))
                        .disabled(model.entries.isEmpty)
                }
            }
            if model.entries.isEmpty {
                EmptyStateView(symbol: "list.bullet.rectangle", text: loc["dash.noActivity"])
                Spacer()
            } else {
                ScrollView {
                    LazyVStack(spacing: 6) {
                        ForEach(model.entries) { entry in
                            HStack(spacing: 12) {
                                GradientBadge(symbol: symbol(entry.actionKey), colors: [0x6E5BFF, 0xB45BFF], size: 30)
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(loc[entry.actionKey]).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text)
                                    Text(entry.detail).font(.system(size: 11.5)).foregroundStyle(p.subtext).lineLimit(1)
                                }
                                Spacer()
                                Text(Fmt.dateTime(entry.date)).font(.system(size: 11)).foregroundStyle(p.faint)
                            }
                            .padding(.horizontal, 12).padding(.vertical, 9)
                            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
                        }
                    }
                }
            }
        }
        .padding(24)
        .onAppear { model.load() }
        .alert(loc["logs.clearConfirm"], isPresented: $confirmClear) {
            Button(loc["logs.clear"], role: .destructive) {
                model.clear()
                state.refreshActivity()
            }
            Button(loc["action.cancel"], role: .cancel) {}
        }
    }

    private func symbol(_ key: String) -> String {
        switch key {
        case "log.uninstalled": return "trash.fill"
        case "log.forced": return "bolt.fill"
        case "log.leftovers": return "sparkles"
        case "log.junk": return "wind"
        case "log.extension": return "puzzlepiece.extension.fill"
        case "log.shred": return "flame.fill"
        case "log.history": return "clock.arrow.circlepath"
        case "log.baseline", "log.monitorRemoved": return "binoculars.fill"
        case "log.launchItem": return "power"
        default: return "checkmark"
        }
    }
}

// MARK: - Hunter mode

@MainActor
final class HunterModel: ObservableObject {
    @Published var apps: [RunningAppInfo] = []
    @Published var target: RunningAppInfo?
    private var observer: NSObjectProtocol?

    init() {
        observer = NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main
        ) { [weak self] note in
            guard let app = note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication,
                  app.bundleIdentifier != Bundle.main.bundleIdentifier,
                  app.activationPolicy == .regular else { return }
            let info = RunningAppInfo(app)
            Task { @MainActor in
                withAnimation(.spring) { self?.target = info }
                self?.refresh()
            }
        }
        refresh()
    }

    func refresh() {
        apps = NSWorkspace.shared.runningApplications
            .filter { $0.activationPolicy == .regular && $0.bundleIdentifier != Bundle.main.bundleIdentifier }
            .map(RunningAppInfo.init)
            .sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }

    func quit(_ app: RunningAppInfo, force: Bool) {
        if let running = NSRunningApplication(processIdentifier: app.pid) {
            _ = force ? running.forceTerminate() : running.terminate()
        }
        if target?.pid == app.pid { target = nil }
        Task {
            try? await Task.sleep(nanoseconds: 700_000_000)
            self.refresh()
        }
    }

    func reveal(_ app: RunningAppInfo) {
        if let url = app.url { NSWorkspace.shared.activateFileViewerSelecting([url]) }
    }
}

struct HunterView: View {
    @EnvironmentObject private var model: HunterModel
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var pulse = false

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 16) {
            PageHeader(loc["nav.hunter"], loc["hunt.subtitle"]) {
                Button { model.refresh() } label: { Label(loc["action.refresh"], systemImage: "arrow.clockwise") }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
            }
            targetCard(p)
            Text(loc["hunt.running"]).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
            ScrollView {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 240), spacing: 10)], spacing: 10) {
                    ForEach(model.apps) { app in
                        runningCard(app, p)
                    }
                }
            }
        }
        .padding(24)
        .onAppear { model.refresh() }
    }

    private func targetCard(_ p: Palette) -> some View {
        GlassCard(padding: 18, radius: 18) {
            HStack(spacing: 18) {
                ZStack {
                    Circle().stroke(Palette.accent.opacity(0.35), lineWidth: 2).frame(width: 74, height: 74)
                        .scaleEffect(pulse ? 1.18 : 0.92)
                        .opacity(pulse ? 0 : 1)
                        .animation(.easeOut(duration: 1.6).repeatForever(autoreverses: false), value: pulse)
                    if let target = model.target, let url = target.url {
                        AppIconView(path: url.path, size: 52)
                    } else {
                        Image(systemName: "scope")
                            .font(.system(size: 34, weight: .semibold))
                            .foregroundStyle(Palette.brandGradient)
                            .symbolEffect(.pulse)
                    }
                }
                .frame(width: 80, height: 80)
                .onAppear { pulse = true }

                VStack(alignment: .leading, spacing: 4) {
                    Text(model.target?.name ?? loc["hunt.noTarget"])
                        .font(.system(size: 18, weight: .bold, design: .rounded)).foregroundStyle(p.text)
                    Text(model.target == nil ? loc["hunt.howto"] : (model.target?.bundleID ?? ""))
                        .font(.system(size: 12)).foregroundStyle(p.subtext)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer()
                if let target = model.target {
                    actionButtons(target)
                }
            }
        }
    }

    @ViewBuilder
    private func actionButtons(_ app: RunningAppInfo) -> some View {
        HStack(spacing: 8) {
            Button(loc["hunt.quit"]) { model.quit(app, force: false) }
                .buttonStyle(OBButtonStyle(kind: .secondary))
            Button(loc["hunt.forceQuit"]) { model.quit(app, force: true) }
                .buttonStyle(OBButtonStyle(kind: .secondary))
            Button { model.reveal(app) } label: { Image(systemName: "folder") }
                .buttonStyle(OBButtonStyle(kind: .secondary))
                .help(loc["cmd.reveal"])
            if let url = app.url, url.pathExtension == "app" {
                Button(loc["action.uninstall"]) { state.uninstallApp(at: url) }
                    .buttonStyle(OBButtonStyle(kind: .danger))
            }
        }
    }

    private func runningCard(_ app: RunningAppInfo, _ p: Palette) -> some View {
        HStack(spacing: 10) {
            if let url = app.url {
                AppIconView(path: url.path, size: 32)
            } else {
                Image(systemName: "app").frame(width: 32, height: 32)
            }
            VStack(alignment: .leading, spacing: 1) {
                Text(app.name).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text).lineLimit(1)
                Text("PID \(app.pid)").font(.system(size: 10.5)).foregroundStyle(p.faint)
            }
            Spacer()
            Menu {
                Button(loc["hunt.quit"]) { model.quit(app, force: false) }
                Button(loc["hunt.forceQuit"]) { model.quit(app, force: true) }
                Button(loc["cmd.reveal"]) { model.reveal(app) }
                if let url = app.url, url.pathExtension == "app" {
                    Divider()
                    Button(loc["action.uninstall"]) { state.uninstallApp(at: url) }
                }
            } label: {
                Image(systemName: "ellipsis.circle")
            }
            .menuStyle(.borderlessButton)
            .menuIndicator(.hidden)
            .fixedSize()
        }
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
        .hoverLift()
    }
}
