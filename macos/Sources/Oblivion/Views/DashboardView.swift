import SwiftUI

struct DashboardView: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                PageHeader(loc["nav.dashboard"], loc["dash.subtitle"]) {
                    Button {
                        state.loadApps(force: true)
                        state.refreshSystem()
                        state.refreshActivity()
                    } label: {
                        Label(loc["action.refresh"], systemImage: "arrow.clockwise")
                    }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                }

                if !state.hasFullDiskAccess { FullDiskAccessBanner().appearIn(0) }

                stats
                HealthCard().appearIn(0.12)
                quickActions.appearIn(0.18)

                HStack(alignment: .top, spacing: 14) {
                    largestApps
                    recentActivity
                }
                .appearIn(0.24)
            }
            .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
        }
        .onAppear {
            state.loadApps()
            state.refreshSystem()
            state.refreshActivity()
        }
    }

    private var stats: some View {
        LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 12), count: 4), spacing: 12) {
            StatCard(symbol: "square.stack.3d.up.fill", colors: [0x6E5BFF, 0xB45BFF],
                     value: state.isLoadingApps ? "…" : "\(state.apps.count)", label: loc["dash.apps"])
                .appearIn(0)
            StatCard(symbol: "externaldrive.fill", colors: [0x3A8DFF, 0x6F5BFF],
                     value: Fmt.bytes(state.totalAppBytes),
                     label: state.sizesPending > 0 ? loc["dash.measuring"] : loc["dash.footprint"])
                .appearIn(0.04)
            StatCard(symbol: "power", colors: [0xFF9F45, 0xFF6B6B],
                     value: "\(state.launchItemCount)", label: loc["dash.launchItems"])
                .appearIn(0.08)
            StatCard(symbol: "internaldrive.fill", colors: [0x22C55E, 0x14B8A6],
                     value: Fmt.bytes(state.diskFree), label: loc["dash.freeSpace"])
                .appearIn(0.12)
        }
    }

    private var quickActions: some View {
        let p = Palette(scheme)
        return GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 12) {
                Text(loc["dash.quick"]).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                HStack(spacing: 10) {
                    QuickAction(symbol: "trash", colors: [0xE5484D, 0xFF6B6B], title: loc["nav.apps"]) { state.navigate(.apps) }
                    QuickAction(symbol: "bag", colors: [0x3A8DFF, 0x6F5BFF], title: loc["nav.store"]) { state.navigate(.storeApps) }
                    QuickAction(symbol: "sparkles", colors: [0x18C29C, 0x2E8BFF], title: loc["tool.junk"]) { state.navigate(.junk) }
                    QuickAction(symbol: "gauge.with.dots.needle.67percent", colors: [0x06B6D4, 0x6E5BFF],
                                title: loc["nav.systemMonitor"]) { state.navigate(.systemMonitor) }
                    QuickAction(symbol: "doc.badge.ellipsis", colors: [0xFF9F45, 0xF5A524], title: loc["tool.large"]) { state.navigate(.largeFiles) }
                    QuickAction(symbol: "scope", colors: [0x9B5BFF, 0xE15BBE], title: loc["nav.hunter"]) { state.navigate(.hunter) }
                }
            }
        }
    }

    private var largestApps: some View {
        let p = Palette(scheme)
        let top = state.apps.filter { $0.sizeBytes > 0 }.sorted { $0.sizeBytes > $1.sizeBytes }.prefix(6)
        let maxSize = Double(top.first?.sizeBytes ?? 1)
        return GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                Text(loc["dash.largest"]).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                if top.isEmpty {
                    Text(state.isLoadingApps || state.sizesPending > 0 ? loc["dash.measuring"] : "—")
                        .font(.system(size: 12)).foregroundStyle(p.subtext)
                } else {
                    ForEach(Array(top)) { app in
                        HStack(spacing: 10) {
                            AppIconView(path: app.url.path, size: 24)
                            VStack(alignment: .leading, spacing: 4) {
                                HStack {
                                    Text(app.name).font(.system(size: 12.5, weight: .medium)).foregroundStyle(p.text).lineLimit(1)
                                    Spacer()
                                    Text(Fmt.bytes(app.sizeBytes)).font(.system(size: 12, weight: .semibold)).foregroundStyle(p.subtext)
                                }
                                GeometryReader { geo in
                                    Capsule().fill(Palette.brandGradient)
                                        .frame(width: max(6, geo.size.width * Double(app.sizeBytes) / maxSize), height: 4)
                                }
                                .frame(height: 4)
                            }
                        }
                        .contentShape(Rectangle())
                        .onTapGesture {
                            state.selectedAppID = app.id
                            state.navigate(app.isAppStore ? .storeApps : .apps)
                        }
                    }
                }
            }
        }
    }

    private var recentActivity: some View {
        let p = Palette(scheme)
        return GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Text(loc["dash.activity"]).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                    Spacer()
                    Button(loc["dash.allLogs"]) { state.navigate(.logs) }
                        .buttonStyle(OBButtonStyle(kind: .ghost))
                }
                if state.recentActivity.isEmpty {
                    Text(loc["dash.noActivity"]).font(.system(size: 12)).foregroundStyle(p.subtext)
                } else {
                    ForEach(state.recentActivity) { entry in
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(loc[entry.actionKey]).font(.system(size: 12.5, weight: .semibold)).foregroundStyle(p.text)
                                Text(entry.detail).font(.system(size: 11.5)).foregroundStyle(p.subtext).lineLimit(1)
                            }
                            Spacer()
                            Text(Fmt.dateTime(entry.date)).font(.system(size: 10.5)).foregroundStyle(p.faint)
                        }
                    }
                }
            }
        }
    }
}

struct QuickAction: View {
    let symbol: String
    let colors: [UInt32]
    let title: String
    let action: () -> Void
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            VStack(spacing: 8) {
                GradientBadge(symbol: symbol, colors: colors, size: 42)
                Text(title)
                    .font(.system(size: 11.5, weight: .medium))
                    .foregroundStyle(p.text)
                    .multilineTextAlignment(.center)
                    .lineLimit(2)
                    .minimumScaleFactor(0.85)
                    .frame(height: 30, alignment: .top)
                    .padding(.horizontal, 4)
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(p.card))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(p.stroke, lineWidth: 1))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .hoverLift()
    }
}

struct FullDiskAccessBanner: View {
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        HStack(spacing: 14) {
            GradientBadge(symbol: "lock.shield.fill", colors: [0xF5A524, 0xFF6B6B], size: 40)
            VStack(alignment: .leading, spacing: 3) {
                Text(loc["fda.title"]).font(.system(size: 13.5, weight: .semibold)).foregroundStyle(p.text)
                Text(loc["fda.body"]).font(.system(size: 12)).foregroundStyle(p.subtext).fixedSize(horizontal: false, vertical: true)
            }
            Spacer()
            Button(loc["fda.open"]) { SystemInfo.openFullDiskAccessSettings() }
                .buttonStyle(OBButtonStyle(kind: .primary))
            Button {
                state.refreshSystem()
            } label: {
                Image(systemName: "arrow.clockwise")
            }
            .buttonStyle(OBButtonStyle(kind: .secondary))
        }
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(Palette.warning.opacity(0.12)))
        .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(Palette.warning.opacity(0.35), lineWidth: 1))
    }
}

// live cpu/mem/temp/net summary, links to the system monitor
struct HealthCard: View {
    @EnvironmentObject private var monitor: SystemMonitor
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @EnvironmentObject private var prefs: Prefs
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        let s = monitor.snapshot
        GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 12) {
                HStack(spacing: 8) {
                    Text(loc["dash.health"]).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                    LiveDot()
                    Spacer()
                    Button { state.navigate(.systemMonitor) } label: {
                        HStack(spacing: 4) {
                            Text(loc["dash.healthOpen"])
                            Image(systemName: "arrow.right")
                        }
                    }
                    .buttonStyle(OBButtonStyle(kind: .ghost))
                }
                HStack(alignment: .center, spacing: 12) {
                    MiniGauge(title: loc["live.cpu"], value: s.cpu / 100, text: Fmt.percent(s.cpu, loc),
                              colors: [0x6E5BFF, 0xB45BFF], size: 74, animate: !prefs.calmMotion)
                    MiniGauge(title: loc["live.memory"], value: s.memPercent / 100, text: Fmt.percent(s.memPercent, loc),
                              colors: [0x18C29C, 0x2E8BFF], size: 74, animate: !prefs.calmMotion)
                    MiniGauge(title: loc["live.temp"], value: s.temperature.map { $0 / 100 } ?? s.thermalState.fraction,
                              text: s.temperature.map { "\(Int($0.rounded())) °C" } ?? loc[s.thermalState.shortKey],
                              colors: [0xF5A524, 0xE5484D], size: 74, animate: !prefs.calmMotion)
                    VStack(alignment: .leading, spacing: 8) {
                        Label(loc["live.network"], systemImage: "arrow.up.arrow.down")
                            .font(.system(size: 11.5, weight: .medium))
                            .foregroundStyle(p.subtext)
                        Text("↓ \(Fmt.rate(s.netDown))")
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(p.text)
                        Text("↑ \(Fmt.rate(s.netUp))")
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(p.text)
                        Sparkline(values: s.netDownHistory)
                            .frame(height: 22)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
        .onAppear { monitor.subscribe("dashboard", detailed: false) }
        .onDisappear { monitor.unsubscribe("dashboard") }
    }
}
