import AppKit
import SwiftUI

// MARK: - Tools hub

struct ToolsView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc

    private let tools: [(page: Page, symbol: String, colors: [UInt32], title: String, detail: String)] = [
        (.junk, "sparkles", [0x18C29C, 0x2E8BFF], "tool.junk", "tool.junk.d"),
        (.systemMonitor, "gauge.with.dots.needle.67percent", [0x06B6D4, 0x6E5BFF], "nav.systemMonitor", "tool.sysmon.d"),
        (.startup, "power", [0x3A8DFF, 0x6F5BFF], "tool.startup", "tool.startup.d"),
        (.largeFiles, "doc.badge.ellipsis", [0xFF9F45, 0xF5A524], "tool.large", "tool.large.d"),
        (.shredder, "flame.fill", [0xFF6B6B, 0xE5484D], "tool.shredder", "tool.shredder.d"),
        (.history, "clock.arrow.circlepath", [0x9B5BFF, 0xE15BBE], "tool.history", "tool.history.d"),
        (.monitor, "binoculars.fill", [0x22C55E, 0x14B8A6], "nav.monitor", "mon.subtitle"),
    ]

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                PageHeader(loc["nav.tools"], loc["tools.subtitle"])
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 250), spacing: 14)], spacing: 14) {
                    ForEach(Array(tools.enumerated()), id: \.element.page) { index, tool in
                        ToolCard(symbol: tool.symbol, colors: tool.colors,
                                 title: loc[tool.title], detail: loc[tool.detail],
                                 open: loc["action.open"]) {
                            state.navigate(tool.page)
                        }
                        .appearIn(Double(index) * 0.045)
                    }
                }
            }
            .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
        }
    }
}

struct ToolCard: View {
    let symbol: String
    let colors: [UInt32]
    let title: String
    let detail: String
    let open: String
    let action: () -> Void
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            VStack(alignment: .leading, spacing: 12) {
                GradientBadge(symbol: symbol, colors: colors, size: 44)
                Text(title).font(.system(size: 16, weight: .semibold)).foregroundStyle(p.text)
                Text(detail).font(.system(size: 12)).foregroundStyle(p.subtext)
                    .fixedSize(horizontal: false, vertical: true)
                    .frame(minHeight: 32, alignment: .top)
                HStack(spacing: 4) {
                    Text(open)
                    Image(systemName: "arrow.right")
                }
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(p.accentText)
            }
            .padding(18)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(p.stroke, lineWidth: 1))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .hoverLift()
    }
}

// header with a back button to tools
struct ToolHeader<Trailing: View>: View {
    let title: String
    let subtitle: String
    let trailing: Trailing
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc

    init(_ title: String, _ subtitle: String, @ViewBuilder trailing: () -> Trailing) {
        self.title = title
        self.subtitle = subtitle
        self.trailing = trailing()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Button { state.navigate(.tools) } label: {
                Label(loc["nav.tools"], systemImage: "chevron.left")
            }
            .buttonStyle(OBButtonStyle(kind: .ghost))
            PageHeader(title, subtitle) { trailing }
        }
    }
}

// MARK: - Startup manager

@MainActor
final class StartupModel: ObservableObject {
    @Published var items: [LaunchItem] = []
    @Published var busy = false
    // label of the item that failed to toggle (shown as a warning)
    @Published var failure: String?

    func load() {
        busy = true
        Task {
            let found = await Task.detached { LaunchItemsService.scan() }.value
            withAnimation(.spring) { self.items = found }
            self.busy = false
        }
    }

    func remove(_ item: LaunchItem) {
        Task {
            let ok = await Task.detached { LaunchItemsService.remove(item) }.value
            if ok {
                ActivityLog.append("log.launchItem", item.label)
                withAnimation(.spring) { self.items.removeAll { $0.id == item.id } }
            }
        }
    }

    // user agent off/on with launchctl disable/enable, nothing deleted
    func setEnabled(_ item: LaunchItem, _ enabled: Bool) {
        guard item.canToggle, let index = items.firstIndex(where: { $0.id == item.id }) else { return }
        failure = nil
        withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) { items[index].enabled = enabled }
        Task {
            let ok = await Task.detached { LaunchItemsService.setEnabled(item, enabled) }.value
            if ok {
                ActivityLog.append(enabled ? "log.launchOn" : "log.launchOff", item.label)
            } else {
                if let i = self.items.firstIndex(where: { $0.id == item.id }) {
                    withAnimation(.spring(response: 0.3, dampingFraction: 0.8)) { self.items[i].enabled = !enabled }
                }
                self.failure = item.label
            }
        }
    }
}

struct StartupView: View {
    @EnvironmentObject private var model: StartupModel
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 14) {
            ToolHeader(loc["tool.startup"], loc["startup.subtitle"]) {
                HStack(spacing: 8) {
                    Button { SystemInfo.openLoginItemsSettings() } label: {
                        Label(loc["startup.loginItems"], systemImage: "person.crop.circle.badge.checkmark")
                    }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    Button { model.load() } label: { Image(systemName: "arrow.clockwise") }
                        .buttonStyle(OBButtonStyle(kind: .secondary))
                        .help(loc["action.refresh"])
                }
            }
            Text(loc["startup.systemWide"]).font(.system(size: 11.5)).foregroundStyle(p.subtext)
            if let failure = model.failure {
                Label(loc.t("startup.toggleFailed", ["name": failure]), systemImage: "exclamationmark.triangle.fill")
                    .font(.system(size: 12)).foregroundStyle(Palette.warning)
                    .transition(.opacity)
            }
            if model.busy && model.items.isEmpty {
                LoadingView(text: loc["common.scanning"])
            } else if model.items.isEmpty {
                EmptyStateView(symbol: "power", text: loc["startup.empty"])
                Spacer()
            } else {
                ScrollView {
                    LazyVStack(spacing: 6) {
                        ForEach(sorted) { item in
                            row(item, p)
                                .transition(.opacity.combined(with: .move(edge: .leading)))
                        }
                    }
                }
            }
        }
        .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
        .onAppear { model.load() }
    }

    private var sorted: [LaunchItem] {
        func order(_ scope: LaunchItem.Scope) -> Int {
            switch scope {
            case .userAgent: return 0
            case .globalAgent: return 1
            case .daemon: return 2
            }
        }
        return model.items.sorted {
            (order($0.scope), $0.label.lowercased()) < (order($1.scope), $1.label.lowercased())
        }
    }

    private func row(_ item: LaunchItem, _ p: Palette) -> some View {
        HStack(spacing: 12) {
            GradientBadge(symbol: "power", colors: item.scope == .daemon ? [0xFF9F45, 0xFF6B6B] : [0x3A8DFF, 0x6F5BFF], size: 32)
                .saturation(item.enabled ? 1 : 0)
                .opacity(item.enabled ? 1 : 0.6)
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(item.label).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text).lineLimit(1)
                    if item.runAtLoad && item.enabled { Chip(text: loc["startup.atLogin"], color: Palette.success) }
                    if !item.enabled { Chip(text: loc["startup.disabled"], color: Color.gray) }
                }
                Text(item.program.isEmpty ? item.path : item.program)
                    .font(.system(size: 10.5)).foregroundStyle(p.faint).lineLimit(1).truncationMode(.middle)
            }
            Spacer()
            Chip(text: loc["scope." + item.scope.rawValue], color: Palette.accent)
            if item.canToggle {
                Toggle("", isOn: Binding(get: { item.enabled }, set: { model.setEnabled(item, $0) }))
                    .toggleStyle(.switch)
                    .controlSize(.small)
                    .labelsHidden()
                    .help(loc["startup.toggle"])
            }
            Button {
                NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: item.path)])
            } label: { Image(systemName: "folder") }
                .buttonStyle(OBButtonStyle(kind: .secondary))
                .help(loc["cmd.reveal"])
            Button(loc["action.remove"]) { askToRemove(item) }
                .buttonStyle(OBButtonStyle(kind: .danger))
        }
        .padding(.horizontal, 12).padding(.vertical, 9)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
    }

    private func askToRemove(_ item: LaunchItem) {
        state.confirm = ConfirmRequest(
            title: loc["startup.confirmTitle"],
            message: loc.t("startup.confirmBody", ["name": item.label]),
            details: [item.path],
            confirmTitle: loc["action.remove"],
            symbol: "power"
        ) {
            model.remove(item)
        }
    }
}

// MARK: - Junk cleaner

@MainActor
final class JunkModel: ObservableObject {
    @Published var categories = JunkService.categories()
    @Published var scanning = false
    @Published var cleaning = false
    @Published var result: JunkResult?
    private var scannedOnce = false

    var busy: Bool { scanning || cleaning }
    var selectedSize: Int64 { categories.reduce(Int64(0)) { $0 + $1.selectedSize } }
    var selectedCount: Int { categories.reduce(0) { $0 + $1.selectedCount } }
    var foundSize: Int64 { categories.reduce(Int64(0)) { $0 + $1.size } }
    var foundCount: Int { categories.reduce(0) { $0 + $1.count } }

    func scanIfNeeded() {
        if !scannedOnce { scan() }
    }

    // list every category, nothing ticked
    func scan(keepResult: Bool = false) {
        guard !scanning else { return }
        scannedOnce = true
        scanning = true
        if !keepResult { result = nil }
        for i in categories.indices { categories[i].scanning = true }
        let pending = categories
        Task {
            for category in pending {
                let found = await Task.detached(priority: .userInitiated) { JunkService.scan(category) }.value
                guard let i = self.categories.firstIndex(where: { $0.id == category.id }) else { continue }
                withAnimation(.spring(response: 0.45, dampingFraction: 0.85)) {
                    self.categories[i].items = found.items
                    self.categories[i].unreadable = found.unreadable
                    self.categories[i].size = found.items.reduce(Int64(0)) { $0 + $1.size }
                    self.categories[i].count = found.unreadable ? 0 : found.items.count
                    self.categories[i].scanned = true
                    self.categories[i].scanning = false
                    if found.items.isEmpty { self.categories[i].expanded = false }
                }
            }
            self.scanning = false
        }
    }

    // remove the ticked items, then list again (all unticked)
    func clean(onDone: @escaping () -> Void = {}) {
        let chosen = categories.filter { $0.selectedCount > 0 }
        guard !chosen.isEmpty, !busy else { return }
        cleaning = true
        Task {
            let outcome = await Task.detached(priority: .userInitiated) { () -> JunkResult in
                var count = 0
                var bytes: Int64 = 0
                for category in chosen {
                    let removed = JunkService.clean(category)
                    count += removed.count
                    bytes += removed.bytes
                }
                return JunkResult(count: count, bytes: bytes)
            }.value
            ActivityLog.append("log.junk", "\(outcome.count) · \(Fmt.bytes(outcome.bytes))")
            self.cleaning = false
            withAnimation(.spring(response: 0.5, dampingFraction: 0.8)) { self.result = outcome }
            self.scan(keepResult: true)
            onDone()
        }
    }
}

struct JunkView: View {
    @EnvironmentObject private var model: JunkModel
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 14) {
            ToolHeader(loc["tool.junk"], loc["junk.subtitle"]) {
                Button { model.scan() } label: { Label(loc["junk.rescan"], systemImage: "arrow.clockwise") }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    .disabled(model.busy)
            }
            safetyBanner(p)
            if let result = model.result {
                resultBanner(result, p)
                    .transition(.scale(scale: 0.96).combined(with: .opacity))
            }
            ScrollView {
                LazyVStack(spacing: 10) {
                    ForEach($model.categories) { $category in
                        JunkCategoryCard(category: $category)
                    }
                }
                .padding(.bottom, 4)
            }
            footer(p)
        }
        .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
        .onAppear { model.scanIfNeeded() }
    }

    private func safetyBanner(_ p: Palette) -> some View {
        HStack(alignment: .top, spacing: 10) {
            Image(systemName: "hand.raised.fill")
                .foregroundStyle(Palette.info)
                .font(.system(size: 14))
            VStack(alignment: .leading, spacing: 3) {
                Text(loc["junk.safety"]).font(.system(size: 12)).foregroundStyle(p.text)
                Text(loc["junk.note"]).font(.system(size: 11)).foregroundStyle(p.subtext)
            }
            .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(Palette.info.opacity(0.10)))
    }

    private func resultBanner(_ r: JunkResult, _ p: Palette) -> some View {
        HStack(spacing: 10) {
            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 18))
                .foregroundStyle(Palette.success)
                .symbolEffect(.bounce, value: GraphicsSupport.richEffects ? r.id : nil)
                .overlay { Burst(count: 22, spread: 70).frame(width: 160, height: 120).id(r) }
            Text(loc.t("junk.result", ["count": "\(r.count)", "size": Fmt.bytes(r.bytes)]))
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(p.text)
            Spacer()
            Button { withAnimation(.easeOut(duration: 0.2)) { model.result = nil } } label: {
                Image(systemName: "xmark").font(.system(size: 11, weight: .bold))
            }
            .buttonStyle(.plain)
            .foregroundStyle(p.subtext)
            .help(loc["action.close"])
        }
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(Palette.success.opacity(0.12)))
        .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(Palette.success.opacity(0.3), lineWidth: 1))
    }

    private func footer(_ p: Palette) -> some View {
        HStack(spacing: 14) {
            VStack(alignment: .leading, spacing: 2) {
                Text(Fmt.bytes(model.selectedSize))
                    .font(.system(size: 22, weight: .semibold))
                    .foregroundStyle(model.selectedCount > 0 ? p.text : p.faint)
                    .contentTransition(.numericText())
                    .animation(.snappy, value: model.selectedSize)
                Text(model.selectedCount > 0
                     ? loc.t("junk.selectedTotal", ["count": "\(model.selectedCount)"])
                     : loc["junk.tickHint"])
                    .font(.system(size: 11.5))
                    .foregroundStyle(p.subtext)
            }
            Spacer()
            if model.scanning || model.cleaning { ProgressView().controlSize(.small) }
            if model.foundCount > 0 {
                Text(loc.t("junk.found", ["count": "\(model.foundCount)", "size": Fmt.bytes(model.foundSize)]))
                    .font(.system(size: 11.5))
                    .foregroundStyle(p.faint)
            }
            Button { askToClean() } label: { Label(loc["action.clean"], systemImage: "sparkles") }
                .buttonStyle(OBButtonStyle(kind: .danger))
                .disabled(model.busy || model.selectedCount == 0)
        }
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.cardStrong))
    }

    private func askToClean() {
        var details: [String] = []
        for category in model.categories where category.selectedCount > 0 {
            if category.unreadable {
                details.append(loc["junk.trashAll"])
            } else {
                details += category.selectedItems.map { "\($0.url.path) — \(Fmt.bytes($0.size))" }
            }
        }
        state.confirm = ConfirmRequest(
            title: loc.t("junk.confirmTitle", ["count": "\(model.selectedCount)"]),
            message: loc.t("junk.confirmBody", ["size": Fmt.bytes(model.selectedSize)]),
            details: details,
            confirmTitle: loc["action.clean"],
            symbol: "sparkles"
        ) {
            model.clean { state.refreshSystem() }
        }
    }
}

// one junk category: tri-state checkbox, totals, expandable item list
struct JunkCategoryCard: View {
    @Binding var category: JunkCategory
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    private var hasItems: Bool { category.scanned && !category.items.isEmpty }

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 0) {
            header(p)
            if category.expanded && hasItems {
                Divider().overlay(p.stroke).padding(.horizontal, 14)
                items(p)
                    .transition(.opacity.combined(with: .move(edge: .top)))
            }
        }
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
        .overlay(
            RoundedRectangle(cornerRadius: 12, style: .continuous)
                .stroke(category.selectedCount > 0 ? Palette.accent.opacity(0.6) : p.stroke, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        .animation(.easeOut(duration: 0.2), value: category.selectedCount)
    }

    private func header(_ p: Palette) -> some View {
        HStack(spacing: 12) {
            TriStateCheckbox(state: category.selectionState) {
                category.setAll(category.selectionState != true)
            }
            .disabled(!hasItems)
            .opacity(hasItems ? 1 : 0.35)
            .help(loc["action.selectAll"])

            GradientBadge(symbol: category.symbol, colors: category.colors, size: 38)
            VStack(alignment: .leading, spacing: 2) {
                Text(loc[category.titleKey]).font(.system(size: 13.5, weight: .semibold)).foregroundStyle(p.text)
                Text(loc[category.detailKey]).font(.system(size: 11.5)).foregroundStyle(p.subtext).lineLimit(1)
                if let note = category.noteKey {
                    Text(loc[note]).font(.system(size: 10.5)).foregroundStyle(p.faint)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            Spacer(minLength: 8)
            VStack(alignment: .trailing, spacing: 2) {
                if category.scanning {
                    ProgressView().controlSize(.small)
                } else if !category.scanned {
                    Text(loc["junk.notScanned"]).font(.system(size: 11)).foregroundStyle(p.faint)
                } else if category.items.isEmpty {
                    Text(loc["junk.empty"]).font(.system(size: 11)).foregroundStyle(p.faint)
                } else if category.unreadable {
                    Text("—").font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                } else {
                    Text(Fmt.bytes(category.size))
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(p.text)
                        .contentTransition(.numericText())
                    Text(category.selectedCount > 0
                         ? loc.t("junk.selectedOf", ["selected": "\(category.selectedCount)", "count": "\(category.items.count)"])
                         : loc.t("junk.items", ["count": "\(category.items.count)"]))
                        .font(.system(size: 10.5))
                        .foregroundStyle(category.selectedCount > 0 ? Palette.accent2 : p.faint)
                }
            }
            Button(action: toggleExpanded) {
                Image(systemName: "chevron.down")
                    .font(.system(size: 12, weight: .bold))
                    .rotationEffect(.degrees(category.expanded ? 180 : 0))
                    .frame(width: 28, height: 28)
                    .background(Circle().fill(p.cardStrong))
                    .contentShape(Circle())
            }
            .buttonStyle(.plain)
            .foregroundStyle(p.text)
            .help(category.expanded ? loc["junk.hideItems"] : loc["junk.showItems"])
            .disabled(!hasItems)
            .opacity(hasItems ? 1 : 0.35)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 12)
        .contentShape(Rectangle())
        .onTapGesture { if hasItems { toggleExpanded() } }
    }

    private func toggleExpanded() {
        withAnimation(.spring(response: 0.4, dampingFraction: 0.86)) { category.expanded.toggle() }
    }

    @ViewBuilder
    private func items(_ p: Palette) -> some View {
        if category.unreadable {
            // can't list the trash without full disk access, so just an "empty all" option
            HStack(alignment: .top, spacing: 10) {
                Toggle("", isOn: Binding(
                    get: { category.items.first?.selected ?? false },
                    set: { value in category.setAll(value) }
                ))
                .toggleStyle(.checkbox)
                .labelsHidden()
                VStack(alignment: .leading, spacing: 2) {
                    Text(loc["junk.trashAll"]).font(.system(size: 12.5, weight: .semibold)).foregroundStyle(p.text)
                    Text(loc["junk.trashLocked"]).font(.system(size: 11)).foregroundStyle(p.subtext)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer()
            }
            .padding(14)
        } else {
            LazyVStack(spacing: 2) {
                ForEach(category.items) { item in
                    JunkItemRow(item: item, revealHelp: loc["cmd.reveal"]) {
                        category.toggle(item.id)
                    }
                }
            }
            .padding(8)
        }
    }
}

struct JunkItemRow: View {
    let item: JunkItem
    let revealHelp: String
    let toggle: () -> Void
    @Environment(\.colorScheme) private var scheme
    @State private var hover = false

    var body: some View {
        let p = Palette(scheme)
        HStack(spacing: 10) {
            Toggle("", isOn: Binding(get: { item.selected }, set: { _ in toggle() }))
                .toggleStyle(.checkbox)
                .labelsHidden()
            AppIconView(path: item.url.path, size: 22)
            VStack(alignment: .leading, spacing: 1) {
                Text(item.name)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(p.text)
                    .lineLimit(1)
                    .truncationMode(.middle)
                Text(item.url.deletingLastPathComponent().path)
                    .font(.system(size: 10))
                    .foregroundStyle(p.faint)
                    .lineLimit(1)
                    .truncationMode(.middle)
            }
            Spacer(minLength: 8)
            Text(Fmt.date(item.modified))
                .font(.system(size: 10.5))
                .foregroundStyle(p.faint)
            Text(Fmt.bytes(item.size))
                .font(.system(size: 11.5, weight: .semibold))
                .foregroundStyle(p.text)
                .frame(width: 76, alignment: .trailing)
            Button {
                NSWorkspace.shared.activateFileViewerSelecting([item.url])
            } label: {
                Image(systemName: "folder").font(.system(size: 11))
            }
            .buttonStyle(.plain)
            .foregroundStyle(p.subtext)
            .help(revealHelp)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(
            RoundedRectangle(cornerRadius: 9, style: .continuous)
                .fill(item.selected ? Palette.accent.opacity(0.13) : (hover ? p.cardStrong : Color.clear))
        )
        .contentShape(Rectangle())
        .onTapGesture { toggle() }
        .onHover { hover = $0 }
    }
}

// MARK: - Large files

@MainActor
final class LargeFilesModel: ObservableObject {
    @Published var files: [LargeFile] = []
    @Published var scanning = false
    @Published var scanned = 0
    @Published var thresholdMB: Int = 500
    @Published var hasScanned = false
    private var worker: Task<[LargeFile], Never>?

    var selected: [LargeFile] { files.filter(\.selected) }

    func scan() {
        worker?.cancel()
        scanning = true
        scanned = 0
        let minBytes = Int64(thresholdMB) * 1_000_000
        // cancel the detached task itself so the scan loop notices
        let job = Task.detached(priority: .userInitiated) { [weak self] () -> [LargeFile] in
            LargeFilesService.scan(minBytes: minBytes, progress: { count in
                Task { @MainActor in self?.scanned = count }
            }, isCancelled: { Task.isCancelled })
        }
        worker = job
        Task {
            let found = await job.value
            guard !job.isCancelled else { return }
            withAnimation(.spring) { self.files = found }
            self.scanning = false
            self.hasScanned = true
        }
    }

    func cancel() {
        worker?.cancel()
        scanning = false
    }

    func setAll(_ value: Bool) {
        for i in files.indices { files[i].selected = value }
    }

    func trashSelected() {
        let chosen = selected
        guard !chosen.isEmpty else { return }
        Task {
            _ = await Task.detached { Trash.move(chosen.map(\.url)) }.value
            let gone = chosen.filter { !FileManager.default.fileExists(atPath: $0.url.path) }
            let bytes = gone.reduce(Int64(0)) { $0 + $1.size }
            ActivityLog.append("log.largeFiles", "\(gone.count) · \(Fmt.bytes(bytes))")
            withAnimation(.spring) {
                self.files.removeAll { file in !FileManager.default.fileExists(atPath: file.url.path) }
            }
        }
    }
}

struct LargeFilesView: View {
    @EnvironmentObject private var model: LargeFilesModel
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 14) {
            ToolHeader(loc["tool.large"], loc["large.subtitle"]) {
                HStack(spacing: 8) {
                    Picker("", selection: $model.thresholdMB) {
                        Text("100 MB").tag(100)
                        Text("500 MB").tag(500)
                        Text("1 GB").tag(1000)
                        Text("5 GB").tag(5000)
                    }
                    .pickerStyle(.segmented)
                    .labelsHidden()
                    .frame(width: 280)
                    if model.scanning {
                        Button(loc["action.cancel"]) { model.cancel() }
                            .buttonStyle(OBButtonStyle(kind: .secondary))
                    } else {
                        Button { model.scan() } label: { Label(loc["large.scan"], systemImage: "magnifyingglass") }
                            .buttonStyle(OBButtonStyle(kind: .primary))
                    }
                }
            }

            if model.scanning {
                VStack(spacing: 12) {
                    ProgressView().controlSize(.large)
                    Text(loc.t("large.progress", ["count": "\(model.scanned)"]))
                        .foregroundStyle(p.subtext)
                        .contentTransition(.numericText())
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if model.files.isEmpty {
                EmptyStateView(symbol: "doc.badge.ellipsis", text: model.hasScanned ? loc["large.none"] : loc["large.hint"])
                Spacer()
            } else {
                ScrollView {
                    LazyVStack(spacing: 6) {
                        ForEach($model.files) { $file in
                            HStack(spacing: 10) {
                                Toggle("", isOn: $file.selected).toggleStyle(.checkbox).labelsHidden()
                                AppIconView(path: file.url.path, size: 28)
                                VStack(alignment: .leading, spacing: 1) {
                                    Text(file.url.lastPathComponent).font(.system(size: 12.5, weight: .medium))
                                        .foregroundStyle(p.text).lineLimit(1)
                                    Text(file.url.deletingLastPathComponent().path).font(.system(size: 10.5))
                                        .foregroundStyle(p.faint).lineLimit(1).truncationMode(.middle)
                                }
                                Spacer()
                                Text(Fmt.date(file.modified)).font(.system(size: 11)).foregroundStyle(p.faint)
                                Text(Fmt.bytes(file.size)).font(.system(size: 12.5, weight: .semibold))
                                    .foregroundStyle(p.text).frame(width: 84, alignment: .trailing)
                                Button {
                                    NSWorkspace.shared.activateFileViewerSelecting([file.url])
                                } label: { Image(systemName: "folder") }
                                    .buttonStyle(OBButtonStyle(kind: .secondary))
                                    .help(loc["cmd.reveal"])
                            }
                            .padding(.horizontal, 12).padding(.vertical, 8)
                            .background(RoundedRectangle(cornerRadius: 10, style: .continuous)
                                .fill(file.selected ? Palette.accent.opacity(0.12) : p.card))
                        }
                    }
                }
                HStack(spacing: 10) {
                    let selected = model.selected
                    Text(loc.t("left.selected", ["count": "\(selected.count)",
                                                  "size": Fmt.bytes(selected.reduce(Int64(0)) { $0 + $1.size })]))
                        .font(.system(size: 12)).foregroundStyle(p.subtext)
                    Spacer()
                    Button(loc["action.selectNone"]) { model.setAll(false) }
                        .buttonStyle(OBButtonStyle(kind: .secondary))
                        .disabled(selected.isEmpty)
                    Button { askToTrash(selected) } label: { Label(loc["large.trash"], systemImage: "trash.fill") }
                        .buttonStyle(OBButtonStyle(kind: .danger))
                        .disabled(selected.isEmpty)
                }
            }
        }
        .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
    }

    private func askToTrash(_ files: [LargeFile]) {
        let bytes = files.reduce(Int64(0)) { $0 + $1.size }
        state.confirm = ConfirmRequest(
            title: loc.t("large.confirmTitle", ["count": "\(files.count)"]),
            message: loc.t("large.confirmBody", ["size": Fmt.bytes(bytes)]),
            details: files.map { "\($0.url.path) — \(Fmt.bytes($0.size))" },
            confirmTitle: loc["large.trash"],
            symbol: "trash.fill"
        ) {
            model.trashSelected()
        }
    }
}

// MARK: - Shredder

@MainActor
final class ShredderModel: ObservableObject {
    @Published var queue: [URL] = []
    @Published var busy = false
    @Published var progress: Double = 0
    @Published var lastCount: Int?

    func add(_ urls: [URL]) {
        for url in urls where !queue.contains(url) { queue.append(url) }
        lastCount = nil
    }

    func pick() {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = true
        if panel.runModal() == .OK { add(panel.urls) }
    }

    func shred() {
        let items = queue
        guard !items.isEmpty else { return }
        busy = true
        progress = 0
        Task {
            let count = await Task.detached(priority: .userInitiated) { [weak self] () -> Int in
                ShredderService.shred(items) { value in
                    Task { @MainActor in self?.progress = value }
                }
            }.value
            ActivityLog.append("log.shred", "\(count)")
            withAnimation(.spring) { self.queue = [] }
            self.lastCount = count
            self.busy = false
        }
    }
}

struct ShredderView: View {
    @EnvironmentObject private var model: ShredderModel
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var dropHover = false

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 14) {
            ToolHeader(loc["tool.shredder"], loc["shred.subtitle"]) { EmptyView() }

            VStack(spacing: 12) {
                GradientBadge(symbol: "flame.fill", colors: [0xFF6B6B, 0xE5484D], size: 58)
                    .scaleEffect(dropHover ? 1.12 : 1)
                    .animation(.spring(response: 0.3, dampingFraction: 0.6), value: dropHover)
                Text(loc["shred.drop"]).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                Button(loc["shred.pick"]) { model.pick() }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 26)
            .background(
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .strokeBorder(style: StrokeStyle(lineWidth: 2, dash: [8, 6]))
                    .foregroundStyle(dropHover ? Palette.danger : p.stroke)
            )
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(dropHover ? Palette.danger.opacity(0.08) : Color.clear))
            .dropDestination(for: URL.self) { urls, _ in
                model.add(urls)
                return true
            } isTargeted: { dropHover = $0 }

            if !model.queue.isEmpty {
                ScrollView {
                    LazyVStack(spacing: 6) {
                        ForEach(model.queue, id: \.self) { url in
                            HStack(spacing: 10) {
                                AppIconView(path: url.path, size: 24)
                                Text(url.path).font(.system(size: 12)).foregroundStyle(p.text)
                                    .lineLimit(1).truncationMode(.middle)
                                Spacer()
                                Button {
                                    withAnimation(.spring) { model.queue.removeAll { $0 == url } }
                                } label: { Image(systemName: "xmark") }
                                    .buttonStyle(OBButtonStyle(kind: .ghost))
                            }
                            .padding(.horizontal, 12).padding(.vertical, 7)
                            .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(p.card))
                            .transition(.opacity.combined(with: .move(edge: .top)))
                        }
                    }
                }
            } else if let count = model.lastCount {
                EmptyStateView(symbol: "checkmark.seal", text: loc.t("shred.done", ["count": "\(count)"]))
            } else {
                Spacer()
            }

            Text(loc["shred.warning"]).font(.system(size: 11)).foregroundStyle(p.faint)
                .fixedSize(horizontal: false, vertical: true)

            HStack {
                if model.busy {
                    GradientProgressBar(value: model.progress).frame(width: 220)
                }
                Spacer()
                Button { askToShred() } label: { Label(loc["shred.start"], systemImage: "flame.fill") }
                    .buttonStyle(OBButtonStyle(kind: .danger))
                    .disabled(model.queue.isEmpty || model.busy)
            }
        }
        .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
    }

    private func askToShred() {
        state.confirm = ConfirmRequest(
            title: loc["shred.confirmTitle"],
            message: loc.t("shred.confirmBody", ["count": "\(model.queue.count)"]),
            details: model.queue.map(\.path),
            confirmTitle: loc["shred.start"],
            symbol: "flame.fill"
        ) {
            model.shred()
        }
    }
}

// MARK: - History & privacy

@MainActor
final class HistoryModel: ObservableObject {
    @Published var items = HistoryCleaner.items()
    @Published var busy = false
    @Published var lastCount: Int?

    func clean() {
        let ids = Set(items.filter(\.selected).map(\.id))
        guard !ids.isEmpty else { return }
        busy = true
        if ids.contains("clipboard") { _ = NSPasteboard.general.clearContents() }
        Task {
            var count = await Task.detached { HistoryCleaner.clean(ids) }.value
            if ids.contains("clipboard") { count += 1 }
            ActivityLog.append("log.history", "\(count)")
            withAnimation(.spring) {
                self.lastCount = count
                for i in self.items.indices { self.items[i].selected = false }
            }
            self.busy = false
        }
    }
}

struct HistoryView: View {
    @EnvironmentObject private var model: HistoryModel
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 14) {
            ToolHeader(loc["tool.history"], loc["hist.subtitle"]) { EmptyView() }
            VStack(spacing: 8) {
                ForEach($model.items) { $item in
                    HStack(spacing: 12) {
                        Toggle("", isOn: $item.selected).toggleStyle(.checkbox).labelsHidden()
                        GradientBadge(symbol: item.symbol, colors: [0x9B5BFF, 0xE15BBE], size: 36)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(loc[item.titleKey]).font(.system(size: 13.5, weight: .semibold)).foregroundStyle(p.text)
                            Text(loc[item.detailKey]).font(.system(size: 11.5)).foregroundStyle(p.subtext)
                        }
                        Spacer()
                    }
                    .padding(.horizontal, 14).padding(.vertical, 11)
                    .background(RoundedRectangle(cornerRadius: 12, style: .continuous)
                        .fill(item.selected ? Palette.accent.opacity(0.12) : p.card))
                    .contentShape(Rectangle())
                    .onTapGesture { item.selected.toggle() }
                    .appearIn(Double(model.items.firstIndex { $0.id == item.id } ?? 0) * 0.05)
                }
            }
            if let count = model.lastCount {
                Label(loc.t("hist.done", ["count": "\(count)"]), systemImage: "checkmark.circle.fill")
                    .foregroundStyle(Palette.success)
                    .font(.system(size: 12.5, weight: .medium))
                    .transition(.opacity.combined(with: .scale(scale: 0.95)))
            }
            Spacer()
            HStack {
                Spacer()
                if model.busy { ProgressView().controlSize(.small) }
                Button { askToClean() } label: { Label(loc["hist.clean"], systemImage: "sparkles") }
                    .buttonStyle(OBButtonStyle(kind: .primary))
                    .disabled(model.busy || !model.items.contains(where: \.selected))
            }
        }
        .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
    }

    private func askToClean() {
        let chosen = model.items.filter(\.selected)
        state.confirm = ConfirmRequest(
            title: loc.t("hist.confirmTitle", ["count": "\(chosen.count)"]),
            message: loc["hist.confirmBody"],
            details: chosen.map { loc[$0.titleKey] + " — " + loc[$0.detailKey] },
            confirmTitle: loc["hist.clean"],
            symbol: "clock.arrow.circlepath"
        ) {
            model.clean()
        }
    }
}
