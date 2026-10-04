import AppKit
import SwiftUI

struct AppsView: View {
    let storeOnly: Bool
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc

    var body: some View {
        ZStack {
            stageView
                .id(state.stage)
                .transition(.asymmetric(insertion: .opacity.combined(with: .scale(scale: 0.985)),
                                        removal: .opacity))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onAppear { state.loadApps() }
    }

    @ViewBuilder
    private var stageView: some View {
        switch state.stage {
        case .browsing:
            AppListView(storeOnly: storeOnly)
        case .working:
            WorkingView()
        case .review:
            LeftoverReviewView()
        case .done:
            DoneView()
        }
    }
}

// MARK: - App table (Revo's "All applications")

struct AppListView: View {
    let storeOnly: Bool
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var sortOrder = [KeyPathComparator(\InstalledApp.name)]

    private var rows: [InstalledApp] {
        let query = state.search.trimmingCharacters(in: .whitespaces).lowercased()
        return state.apps
            .filter { app in
                (!storeOnly || app.isAppStore)
                    && (query.isEmpty
                        || app.name.lowercased().contains(query)
                        || (app.bundleID?.lowercased().contains(query) ?? false))
            }
            .sorted(using: sortOrder)
    }

    var body: some View {
        let list = rows
        let total = list.reduce(Int64(0)) { $0 + max($1.sizeBytes, 0) }
        VStack(alignment: .leading, spacing: 12) {
            PageHeader(storeOnly ? loc["nav.store"] : loc["nav.apps"],
                       loc.t("apps.subtitle", ["count": "\(list.count)", "size": Fmt.bytes(total)])) {
                HStack(spacing: 8) {
                    SearchField(text: $state.search, placeholder: loc["apps.search"])
                    Button {
                        state.loadApps(force: true)
                    } label: {
                        Image(systemName: "arrow.clockwise")
                    }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    .help(loc["action.refresh"])
                }
            }
            .padding(.horizontal, 36)
            .padding(.top, 30)

            if state.isLoadingApps && state.apps.isEmpty {
                LoadingView(text: loc["apps.loading"])
            } else if list.isEmpty {
                EmptyStateView(symbol: storeOnly ? "bag" : "square.stack.3d.up.slash",
                               text: storeOnly ? loc["apps.emptyStore"] : loc["apps.empty"])
                Spacer()
            } else {
                table(list)
            }

            DescriptionPanel(app: state.selectedApp)
                .padding(.horizontal, 20)
                .padding(.bottom, 16)
        }
    }

    private func table(_ list: [InstalledApp]) -> some View {
        Table(list, selection: $state.selectedAppID, sortOrder: $sortOrder) {
            TableColumn(loc["col.app"], value: \.name) { app in
                AppNameCell(app: app)
            }
            .width(min: 220, ideal: 300)

            TableColumn(loc["col.size"], value: \.sizeSort) { app in
                Text(app.sizeBytes < 0 ? "…" : Fmt.bytes(app.sizeBytes)).monospacedDigit()
            }
            .width(min: 70, ideal: 90)

            TableColumn(loc["col.version"], value: \.version) { app in
                Text(app.version).lineLimit(1)
            }
            .width(min: 60, ideal: 85)

            TableColumn(loc["col.arch"], value: \.archSort) { app in
                ArchBadge(arch: app.arch)
            }
            .width(min: 90, ideal: 110)

            TableColumn(loc["col.installed"], value: \.installSort) { app in
                Text(Fmt.date(app.installDate))
            }
            .width(min: 80, ideal: 100)

            TableColumn(loc["col.lastUsed"], value: \.lastUsedSort) { app in
                Text(Fmt.date(app.lastUsed))
            }
            .width(min: 80, ideal: 100)

            TableColumn(loc["col.source"], value: \.sourceSort) { app in
                SourceBadge(isAppStore: app.isAppStore)
            }
            .width(min: 80, ideal: 95)
        }
        .tableStyle(.inset(alternatesRowBackgrounds: false))
        .scrollContentBackground(.hidden)
        .contextMenu(forSelectionType: InstalledApp.ID.self) { ids in
            if let app = findApp(ids) {
                AppCommands(app: app)
            }
        } primaryAction: { ids in
            if let app = findApp(ids) { state.reveal(app) }
        }
        .padding(.horizontal, 12)
    }

    private func findApp(_ ids: Set<InstalledApp.ID>) -> InstalledApp? {
        guard let id = ids.first else { return nil }
        return state.apps.first { $0.id == id }
    }
}

struct AppNameCell: View {
    let app: InstalledApp

    var body: some View {
        HStack(spacing: 10) {
            AppIconView(path: app.url.path, size: 26)
            VStack(alignment: .leading, spacing: 1) {
                Text(app.name).fontWeight(.semibold).lineLimit(1)
                Text(app.bundleID ?? app.copyright)
                    .font(.system(size: 10.5))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
        }
        .padding(.vertical, 2)
    }
}

struct ArchBadge: View {
    let arch: InstalledApp.Arch
    @EnvironmentObject private var loc: Loc

    var body: some View {
        switch arch {
        case .universal: Chip(text: loc["arch.universal"], color: Palette.accent)
        case .appleSilicon: Chip(text: "Apple Silicon", color: Palette.success)
        case .intel: Chip(text: "Intel", color: Palette.warning)
        case .unknown: Text("—").foregroundStyle(.secondary)
        }
    }
}

struct SourceBadge: View {
    let isAppStore: Bool

    var body: some View {
        if isAppStore {
            Chip(text: "App Store", color: Palette.info)
        } else {
            Text("—").foregroundStyle(.secondary)
        }
    }
}

/// Revo's "Description panel" for the selected app.
struct DescriptionPanel: View {
    let app: InstalledApp?
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        GlassCard(padding: 14) {
            if let app {
                HStack(spacing: 14) {
                    AppIconView(path: app.url.path, size: 48)
                    VStack(alignment: .leading, spacing: 4) {
                        HStack(spacing: 8) {
                            Text(app.name).font(.system(size: 15, weight: .semibold)).foregroundStyle(p.text)
                            if app.isAppStore { Chip(text: "App Store", color: Palette.info) }
                        }
                        Text(app.url.path)
                            .font(.system(size: 11))
                            .foregroundStyle(p.subtext)
                            .lineLimit(1)
                            .truncationMode(.middle)
                        HStack(spacing: 14) {
                            meta("number", app.version, p)
                            meta("externaldrive", app.sizeBytes < 0 ? "…" : Fmt.bytes(app.sizeBytes), p)
                            meta("clock", Fmt.date(app.lastUsed), p)
                            meta("barcode", app.bundleID ?? "—", p)
                        }
                    }
                    Spacer()
                }
            } else {
                Label(loc["desc.empty"], systemImage: "hand.point.up.left")
                    .font(.system(size: 12.5))
                    .foregroundStyle(p.subtext)
            }
        }
    }

    private func meta(_ symbol: String, _ value: String, _ p: Palette) -> some View {
        Label(value, systemImage: symbol)
            .font(.system(size: 11))
            .foregroundStyle(p.subtext)
            .lineLimit(1)
    }
}

// MARK: - Flow screens

struct WorkingView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 18) {
            ZStack {
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .fill(p.card)
                    .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).stroke(p.stroke, lineWidth: 1))
                    .frame(width: 104, height: 104)
                if let icon = state.targetIconPath {
                    AppIconView(path: icon, size: 68)
                } else {
                    GradientBadge(symbol: "magnifyingglass", colors: [0x4F6BED], size: 68)
                }
            }
            .frame(height: 120)

            Text(state.targetName)
                .font(.system(size: 20, weight: .semibold))
                .foregroundStyle(p.text)
            GradientProgressBar()
                .frame(width: 260)
            Text(loc[state.workKey])
                .font(.system(size: 13))
                .foregroundStyle(p.subtext)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

struct LeftoverReviewView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        let selected = state.leftovers.filter(\.selected)
        let selectedBytes = selected.reduce(Int64(0)) { $0 + $1.size }

        VStack(alignment: .leading, spacing: 14) {
            PageHeader(loc["left.title"], loc.t("left.subtitle", ["name": state.targetName, "count": "\(state.leftovers.count)"]))

            HStack(spacing: 12) {
                Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(Palette.warning)
                Text(loc["left.info"]).font(.system(size: 12)).foregroundStyle(p.subtext)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(Palette.warning.opacity(0.10)))

            ScrollView {
                LazyVStack(spacing: 6) {
                    ForEach($state.leftovers) { $item in
                        LeftoverRow(item: $item)
                    }
                }
            }

            HStack(spacing: 10) {
                Text(loc.t("left.selected", ["count": "\(selected.count)", "size": Fmt.bytes(selectedBytes)]))
                    .font(.system(size: 12.5, weight: .medium))
                    .foregroundStyle(p.subtext)
                    .contentTransition(.numericText())
                    .animation(.snappy, value: selected.count)
                Spacer()
                Button { state.selectCertainLeftovers() } label: {
                    Label(loc["left.selectCertain"], systemImage: "checkmark.shield")
                }
                .buttonStyle(OBButtonStyle(kind: .secondary))
                .disabled(!state.leftovers.contains { $0.confidence == .high })
                Button(loc["action.selectAll"]) { state.setAllLeftovers(true) }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                Button(loc["action.selectNone"]) { state.setAllLeftovers(false) }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    .disabled(selected.isEmpty)
                Button(loc["action.skip"]) { state.finishFlow() }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                Button {
                    askToRemove(selected, selectedBytes)
                } label: {
                    Label(loc["left.remove"], systemImage: "trash.fill")
                }
                .buttonStyle(OBButtonStyle(kind: .danger))
                .disabled(selected.isEmpty)
            }
        }
        .padding(24)
    }

    private func askToRemove(_ items: [Leftover], _ bytes: Int64) {
        state.confirm = ConfirmRequest(
            title: loc.t("left.confirmTitle", ["count": "\(items.count)"]),
            message: loc.t("left.confirmBody", ["size": Fmt.bytes(bytes)]),
            details: items.map { $0.kind == .receipt ? "pkgutil: \($0.path)" : "\($0.path) — \(Fmt.bytes($0.size))" },
            confirmTitle: loc["left.remove"],
            symbol: "trash.fill"
        ) {
            state.removeSelectedLeftovers()
        }
    }
}

struct LeftoverRow: View {
    @Binding var item: Leftover
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        HStack(spacing: 10) {
            Toggle("", isOn: $item.selected)
                .toggleStyle(.checkbox)
                .labelsHidden()
            Image(systemName: symbol)
                .frame(width: 18)
                .foregroundStyle(p.subtext)
            VStack(alignment: .leading, spacing: 2) {
                Text(item.kind == .receipt ? item.path : (item.path as NSString).lastPathComponent)
                    .font(.system(size: 12.5, weight: .medium))
                    .foregroundStyle(p.text)
                    .lineLimit(1)
                Text(item.kind == .receipt ? loc["left.receipt"] : item.path)
                    .font(.system(size: 10.5))
                    .foregroundStyle(p.faint)
                    .lineLimit(1)
                    .truncationMode(.middle)
            }
            Spacer()
            if item.isSystem {
                Image(systemName: "lock.fill")
                    .font(.system(size: 10))
                    .foregroundStyle(p.faint)
                    .help(loc["left.admin"])
            }
            Text(item.kind == .receipt ? "—" : Fmt.bytes(item.size))
                .font(.system(size: 11.5))
                .foregroundStyle(p.subtext)
                .frame(width: 72, alignment: .trailing)
            confidenceChip
                .frame(width: 70, alignment: .trailing)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(item.selected ? Palette.accent.opacity(0.12) : p.card))
        .onTapGesture { item.selected.toggle() }
    }

    private var symbol: String {
        switch item.kind {
        case .folder: return "folder"
        case .file: return "doc"
        case .preferences: return "slider.horizontal.3"
        case .container: return "shippingbox"
        case .launchItem: return "power"
        case .receipt: return "doc.text.magnifyingglass"
        }
    }

    @ViewBuilder
    private var confidenceChip: some View {
        switch item.confidence {
        case .high: Chip(text: loc["conf.high"], color: Palette.success)
        case .medium: Chip(text: loc["conf.medium"], color: Palette.warning)
        case .low: Chip(text: loc["conf.low"], color: Color.gray)
        }
    }
}

struct DoneView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var appeared = false

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 16) {
            Image(systemName: "checkmark.seal.fill")
                .font(.system(size: 72, weight: .semibold))
                .foregroundStyle(Palette.brandGradient)
                .symbolEffect(.bounce, value: GraphicsSupport.richEffects && appeared)
                .scaleEffect(appeared ? 1 : 0.6)
                .opacity(appeared ? 1 : 0)
                .background { Burst(count: 34, spread: 120).frame(width: 300, height: 240) }
            Text(loc["done.title"])
                .font(.system(size: 26, weight: .semibold))
                .foregroundStyle(p.text)
            Text(state.targetName)
                .font(.system(size: 15, weight: .medium))
                .foregroundStyle(p.subtext)
            HStack(spacing: 14) {
                summary(value: Fmt.bytes(state.freedBytes), label: loc["done.freed"], p: p)
                summary(value: "\(state.removedCount)", label: loc["done.items"], p: p)
            }
            .padding(.top, 6)
            Button {
                state.finishFlow()
            } label: {
                Label(loc["done.back"], systemImage: "arrow.uturn.backward")
            }
            .buttonStyle(OBButtonStyle(kind: .primary, large: true))
            .padding(.top, 8)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onAppear {
            withAnimation(.spring(response: 0.5, dampingFraction: 0.6)) { appeared = true }
        }
    }

    private func summary(value: String, label: String, p: Palette) -> some View {
        VStack(spacing: 4) {
            Text(value).font(.system(size: 20, weight: .semibold)).foregroundStyle(p.text)
            Text(label).font(.system(size: 11.5)).foregroundStyle(p.subtext)
        }
        .frame(width: 150)
        .padding(.vertical, 14)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
    }
}

// MARK: - Force uninstall sheet

struct ForceUninstallSheet: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 14) {
                GradientBadge(symbol: "bolt.fill", colors: [0xFF9F45, 0xE5484D], size: 46)
                VStack(alignment: .leading, spacing: 3) {
                    Text(loc["force.title"]).font(.system(size: 18, weight: .semibold))
                    Text(loc["force.subtitle"]).font(.system(size: 12)).foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            TextField(loc["force.placeholder"], text: $state.forceQuery)
                .textFieldStyle(.roundedBorder)
                .onSubmit { state.forceUninstall() }
            Text(loc["force.hint"])
                .font(.system(size: 11.5))
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack {
                Spacer()
                Button(loc["action.cancel"]) { state.showForceSheet = false }
                    .keyboardShortcut(.cancelAction)
                Button(loc["force.scan"]) { state.forceUninstall() }
                    .keyboardShortcut(.defaultAction)
                    .disabled(state.forceQuery.trimmingCharacters(in: .whitespaces).isEmpty)
            }
        }
        .padding(24)
        .frame(width: 480)
        .background(
            LinearGradient(colors: [Palette(scheme).tintTop, Palette(scheme).tintBottom],
                           startPoint: .topLeading, endPoint: .bottomTrailing)
        )
        .foregroundStyle(Palette(scheme).text)
        .dropDestination(for: URL.self) { urls, _ in
            guard let url = urls.first else { return false }
            if let bundle = Bundle(url: url), let id = bundle.bundleIdentifier {
                state.forceQuery = id
            } else {
                state.forceQuery = url.deletingPathExtension().lastPathComponent
            }
            return true
        }
    }
}
