import AppKit
import SwiftUI

// MARK: - Tools hub

struct ToolsView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc

    private let tools: [(page: Page, symbol: String, colors: [UInt32], title: String, detail: String)] = [
        (.startup, "power", [0x3A8DFF, 0x6F5BFF], "tool.startup", "tool.startup.d"),
        (.junk, "sparkles", [0x18C29C, 0x2E8BFF], "tool.junk", "tool.junk.d"),
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
                    ForEach(tools, id: \.page) { tool in
                        ToolCard(symbol: tool.symbol, colors: tool.colors,
                                 title: loc[tool.title], detail: loc[tool.detail],
                                 open: loc["action.open"]) {
                            state.navigate(tool.page)
                        }
                    }
                }
            }
            .padding(26)
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
                GradientBadge(symbol: symbol, colors: colors, size: 60)
                Text(title).font(.system(size: 16, weight: .semibold)).foregroundStyle(p.text)
                Text(detail).font(.system(size: 12)).foregroundStyle(p.subtext)
                    .fixedSize(horizontal: false, vertical: true)
                    .frame(minHeight: 32, alignment: .top)
                HStack(spacing: 4) {
                    Text(open)
                    Image(systemName: "arrow.right")
                }
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(Palette.accent2)
            }
            .padding(18)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 18, style: .continuous).fill(p.card))
            .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(p.stroke, lineWidth: 1))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .hoverLift()
    }
}

/// Header with a back button to the Tools hub.
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
}

struct StartupView: View {
    @EnvironmentObject private var model: StartupModel
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
                }
            }
            if model.busy && model.items.isEmpty {
                LoadingView(text: loc["common.scanning"])
            } else if model.items.isEmpty {
                EmptyStateView(symbol: "power", text: loc["startup.empty"])
                Spacer()
            } else {
                ScrollView {
                    LazyVStack(spacing: 6) {
                        ForEach(model.items) { item in row(item, p) }
                    }
                }
            }
        }
        .padding(24)
        .onAppear { model.load() }
    }

    private func row(_ item: LaunchItem, _ p: Palette) -> some View {
        HStack(spacing: 12) {
            GradientBadge(symbol: "power", colors: item.scope == .daemon ? [0xFF9F45, 0xFF6B6B] : [0x3A8DFF, 0x6F5BFF], size: 32)
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(item.label).font(.system(size: 13, weight: .semibold)).foregroundStyle(p.text).lineLimit(1)
                    if item.runAtLoad { Chip(text: loc["startup.atLogin"], color: Palette.success) }
                    if item.disabled { Chip(text: loc["startup.disabled"], color: Color.gray) }
                }
                Text(item.program.isEmpty ? item.path : item.program)
                    .font(.system(size: 10.5)).foregroundStyle(p.faint).lineLimit(1).truncationMode(.middle)
            }
            Spacer()
            Chip(text: loc["scope." + item.scope.rawValue], color: Palette.accent)
            Button {
                NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: item.path)])
            } label: { Image(systemName: "folder") }
                .buttonStyle(OBButtonStyle(kind: .secondary))
            Button(loc["action.remove"]) { model.remove(item) }
                .buttonStyle(OBButtonStyle(kind: .danger))
        }
        .padding(.horizontal, 12).padding(.vertical, 9)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
    }
}

// MARK: - Junk cleaner

@MainActor
final class JunkModel: ObservableObject {
    @Published var categories = JunkService.categories()
    @Published var busy = false
    @Published var freed: Int64?

    var total: Int64 {
        categories.filter { $0.selected && $0.scanned }.reduce(Int64(0)) { $0 + $1.size }
    }

    func analyze() {
        busy = true
        freed = nil
        let current = categories
        Task {
            let measured = await Task.detached { () -> [JunkCategory] in
                current.map { (category: JunkCategory) -> JunkCategory in
                    var copy = category
                    JunkService.measure(&copy)
                    return copy
                }
            }.value
            withAnimation(.spring) {
                for i in self.categories.indices {
                    if let match = measured.first(where: { $0.id == self.categories[i].id }) {
                        self.categories[i].size = match.size
                        self.categories[i].count = match.count
                        self.categories[i].scanned = true
                    }
                }
            }
            self.busy = false
        }
    }

    func clean() {
        let chosen = categories.filter { $0.selected && $0.scanned }
        guard !chosen.isEmpty else { return }
        busy = true
        Task {
            let bytes = await Task.detached { () -> Int64 in
                chosen.reduce(Int64(0)) { $0 + JunkService.clean($1) }
            }.value
            ActivityLog.append("log.junk", Fmt.bytes(bytes))
            self.busy = false
            self.analyze()
            self.freed = bytes
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
            ToolHeader(loc["tool.junk"], loc["junk.subtitle"]) { EmptyView() }
            ScrollView {
                LazyVStack(spacing: 8) {
                    ForEach($model.categories) { $category in
                        HStack(spacing: 12) {
                            Toggle("", isOn: $category.selected).toggleStyle(.checkbox).labelsHidden()
                            GradientBadge(symbol: category.symbol, colors: category.colors, size: 38)
                            VStack(alignment: .leading, spacing: 2) {
                                Text(loc[category.titleKey]).font(.system(size: 13.5, weight: .semibold)).foregroundStyle(p.text)
                                Text(loc[category.detailKey]).font(.system(size: 11.5)).foregroundStyle(p.subtext)
                            }
                            Spacer()
                            VStack(alignment: .trailing, spacing: 2) {
                                Text(category.scanned ? Fmt.bytes(category.size) : "—")
                                    .font(.system(size: 14, weight: .bold, design: .rounded)).foregroundStyle(p.text)
                                    .contentTransition(.numericText())
                                if category.scanned {
                                    Text(loc.t("junk.items", ["count": "\(category.count)"]))
                                        .font(.system(size: 10.5)).foregroundStyle(p.faint)
                                }
                            }
                        }
                        .padding(.horizontal, 14).padding(.vertical, 11)
                        .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(p.card))
                    }
                }
            }
            Text(loc["junk.note"]).font(.system(size: 11)).foregroundStyle(p.faint)
            footer(p)
        }
        .padding(24)
        .onAppear { if !model.categories.contains(where: \.scanned) { model.analyze() } }
    }

    private func footer(_ p: Palette) -> some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Text(Fmt.bytes(model.total))
                    .font(.system(size: 22, weight: .bold, design: .rounded)).foregroundStyle(p.text)
                    .contentTransition(.numericText())
                    .animation(.snappy, value: model.total)
                if let freed = model.freed {
                    Text(loc.t("junk.freed", ["size": Fmt.bytes(freed)]))
                        .font(.system(size: 11.5)).foregroundStyle(Palette.success)
                } else {
                    Text(loc["junk.reclaimable"]).font(.system(size: 11.5)).foregroundStyle(p.subtext)
                }
            }
            Spacer()
            if model.busy { ProgressView().controlSize(.small) }
            Button { model.analyze() } label: { Label(loc["junk.analyze"], systemImage: "magnifyingglass") }
                .buttonStyle(OBButtonStyle(kind: .secondary))
            Button {
                model.clean()
                state.refreshSystem()
            } label: { Label(loc["junk.clean"], systemImage: "sparkles") }
                .buttonStyle(OBButtonStyle(kind: .primary))
                .disabled(model.busy || model.total == 0)
        }
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 16, style: .continuous).fill(p.cardStrong))
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

    func scan() {
        worker?.cancel()
        scanning = true
        scanned = 0
        let minBytes = Int64(thresholdMB) * 1_000_000
        // The detached task itself is what gets cancelled, so the scan loop sees it.
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

    func trashSelected() {
        let chosen = files.filter(\.selected)
        guard !chosen.isEmpty else { return }
        Task {
            _ = await Task.detached { Trash.move(chosen.map(\.url)) }.value
            let bytes = chosen.reduce(Int64(0)) { $0 + $1.size }
            ActivityLog.append("log.largeFiles", "\(chosen.count) · \(Fmt.bytes(bytes))")
            withAnimation(.spring) {
                self.files.removeAll { file in !FileManager.default.fileExists(atPath: file.url.path) }
            }
        }
    }
}

struct LargeFilesView: View {
    @EnvironmentObject private var model: LargeFilesModel
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
                                Text(Fmt.bytes(file.size)).font(.system(size: 12.5, weight: .bold, design: .rounded))
                                    .foregroundStyle(p.text).frame(width: 84, alignment: .trailing)
                                Button {
                                    NSWorkspace.shared.activateFileViewerSelecting([file.url])
                                } label: { Image(systemName: "folder") }
                                    .buttonStyle(OBButtonStyle(kind: .secondary))
                            }
                            .padding(.horizontal, 12).padding(.vertical, 8)
                            .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(p.card))
                        }
                    }
                }
                HStack {
                    let selected = model.files.filter(\.selected)
                    Text(loc.t("left.selected", ["count": "\(selected.count)",
                                                  "size": Fmt.bytes(selected.reduce(Int64(0)) { $0 + $1.size })]))
                        .font(.system(size: 12)).foregroundStyle(p.subtext)
                    Spacer()
                    Button { model.trashSelected() } label: { Label(loc["large.trash"], systemImage: "trash.fill") }
                        .buttonStyle(OBButtonStyle(kind: .danger))
                        .disabled(selected.isEmpty)
                }
            }
        }
        .padding(24)
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
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var confirm = false
    @State private var dropHover = false

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 14) {
            ToolHeader(loc["tool.shredder"], loc["shred.subtitle"]) { EmptyView() }

            VStack(spacing: 12) {
                GradientBadge(symbol: "flame.fill", colors: [0xFF6B6B, 0xE5484D], size: 58)
                Text(loc["shred.drop"]).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                Button(loc["shred.pick"]) { model.pick() }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 26)
            .background(
                RoundedRectangle(cornerRadius: 18, style: .continuous)
                    .strokeBorder(style: StrokeStyle(lineWidth: 2, dash: [8, 6]))
                    .foregroundStyle(dropHover ? Palette.danger : p.stroke)
            )
            .background(RoundedRectangle(cornerRadius: 18, style: .continuous).fill(dropHover ? Palette.danger.opacity(0.08) : Color.clear))
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
                                    model.queue.removeAll { $0 == url }
                                } label: { Image(systemName: "xmark") }
                                    .buttonStyle(OBButtonStyle(kind: .ghost))
                            }
                            .padding(.horizontal, 12).padding(.vertical, 7)
                            .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(p.card))
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
                    ProgressView(value: model.progress).frame(width: 220).tint(Palette.danger)
                }
                Spacer()
                Button { confirm = true } label: { Label(loc["shred.start"], systemImage: "flame.fill") }
                    .buttonStyle(OBButtonStyle(kind: .danger))
                    .disabled(model.queue.isEmpty || model.busy)
            }
        }
        .padding(24)
        .alert(loc["shred.confirmTitle"], isPresented: $confirm) {
            Button(loc["shred.start"], role: .destructive) { model.shred() }
            Button(loc["action.cancel"], role: .cancel) {}
        } message: {
            Text(loc.t("shred.confirmBody", ["count": "\(model.queue.count)"]))
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
            self.lastCount = count
            self.busy = false
        }
    }
}

struct HistoryView: View {
    @EnvironmentObject private var model: HistoryModel
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
                    .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(p.card))
                }
            }
            if let count = model.lastCount {
                Label(loc.t("hist.done", ["count": "\(count)"]), systemImage: "checkmark.circle.fill")
                    .foregroundStyle(Palette.success)
                    .font(.system(size: 12.5, weight: .medium))
            }
            Spacer()
            HStack {
                Spacer()
                if model.busy { ProgressView().controlSize(.small) }
                Button { model.clean() } label: { Label(loc["hist.clean"], systemImage: "sparkles") }
                    .buttonStyle(OBButtonStyle(kind: .primary))
                    .disabled(model.busy || !model.items.contains(where: \.selected))
            }
        }
        .padding(24)
    }
}
