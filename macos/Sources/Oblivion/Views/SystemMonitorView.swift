import AppKit
import SwiftUI

/// Live system monitor: CPU, memory, temperature, network, top processes, this Mac,
/// disks and battery. Samples every second only while the page is open.
struct SystemMonitorView: View {
    @EnvironmentObject private var monitor: SystemMonitor
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme
    @State private var processMode = 0
    @State private var message: String?

    var body: some View {
        let p = Palette(scheme)
        let s = monitor.snapshot
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                PageHeader(loc["nav.systemMonitor"], loc["sys.subtitle"]) {
                    HStack(spacing: 8) {
                        HStack(spacing: 6) {
                            LiveDot()
                            Text(loc["sys.live"]).font(.system(size: 11, weight: .semibold)).foregroundStyle(Palette.success)
                        }
                        .padding(.horizontal, 10).padding(.vertical, 6)
                        .background(RoundedRectangle(cornerRadius: 7, style: .continuous).fill(Palette.success.opacity(0.12)))
                        Button {
                            NSWorkspace.shared.open(URL(fileURLWithPath: "/System/Applications/Utilities/Activity Monitor.app"))
                        } label: {
                            Label(loc["sys.activityMonitor"], systemImage: "arrow.up.forward.app")
                        }
                        .buttonStyle(OBButtonStyle(kind: .secondary))
                    }
                }

                LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 12, alignment: .top), count: 4), spacing: 12) {
                    gaugeCard(title: loc["live.cpu"], symbol: "cpu", colors: [0x6E5BFF, 0xB45BFF],
                              value: s.cpu / 100, center: Fmt.percent(s.cpu, loc), p: p) {
                        Sparkline(values: s.cpuHistory, maximum: 100).frame(height: 34)
                        Text(monitor.info.chip).font(.system(size: 11)).foregroundStyle(p.subtext).lineLimit(1)
                        Text(loc.t("sys.cores", ["cores": monitor.info.cores])).font(.system(size: 10.5)).foregroundStyle(p.faint)
                    }
                    .appearIn(0)
                    gaugeCard(title: loc["live.memory"], symbol: "memorychip", colors: [0x18C29C, 0x2E8BFF],
                              value: s.memPercent / 100, center: Fmt.percent(s.memPercent, loc), p: p) {
                        Sparkline(values: s.memHistory, maximum: 100).frame(height: 34)
                        Text(loc.t("sys.memUsed", ["used": Fmt.bytes(Int64(s.memUsed)), "total": Fmt.bytes(Int64(s.memTotal))]))
                            .font(.system(size: 11)).foregroundStyle(p.subtext).lineLimit(1)
                        Text(loc["sys.memNote"]).font(.system(size: 10.5)).foregroundStyle(p.faint).lineLimit(1)
                    }
                    .appearIn(0.05)
                    temperatureCard(s, p).appearIn(0.1)
                    networkCard(s, p).appearIn(0.15)
                }

                HStack(alignment: .top, spacing: 12) {
                    processesCard(s, p)
                        .frame(maxWidth: .infinity)
                    VStack(spacing: 12) {
                        thisMacCard(s, p)
                        drivesCard(s, p)
                        if let battery = s.battery { batteryCard(battery, p) }
                    }
                    .frame(width: 330)
                }
                .appearIn(0.12)
            }
            .padding(.horizontal, 36).padding(.top, 30).padding(.bottom, 24)
        }
        .onAppear { monitor.subscribe("page", detailed: true) }
        .onDisappear { monitor.unsubscribe("page") }
    }

    // MARK: Cards

    private func gaugeCard<Extra: View>(title: String, symbol: String, colors: [UInt32], value: Double, center: String,
                                        p: Palette, @ViewBuilder extra: () -> Extra) -> some View {
        // GlassCard keeps its content closure, so the extra view is built up front.
        let details = extra()
        return GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                Label(title, systemImage: symbol)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(p.text)
                ZStack {
                    RingGauge(value: value, colors: colors, lineWidth: 9)
                    Text(center)
                        .font(.system(size: 20, weight: .semibold))
                        .foregroundStyle(p.text)
                        .contentTransition(.numericText())
                        .animation(.snappy, value: center)
                }
                .frame(width: 96, height: 96)
                .frame(maxWidth: .infinity)
                details
            }
        }
    }

    private func temperatureCard(_ s: SystemSnapshot, _ p: Palette) -> some View {
        let thermal = s.thermalState
        return GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                Label(loc["sys.temperature"], systemImage: "thermometer.medium")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(p.text)
                ZStack {
                    RingGauge(value: s.temperature.map { $0 / 100 } ?? thermal.fraction,
                              colors: [0xF5A524, 0xE5484D], lineWidth: 9)
                    Text(s.temperature.map { "\(Int($0.rounded())) °C" } ?? loc[thermal.shortKey])
                        .font(.system(size: s.temperature == nil ? 15 : 19, weight: .semibold))
                        .foregroundStyle(p.text)
                        .multilineTextAlignment(.center)
                }
                .frame(width: 96, height: 96)
                .frame(maxWidth: .infinity)
                Text(loc.t("sys.thermalState", ["state": loc[thermal.longKey]]))
                    .font(.system(size: 11.5, weight: .medium))
                    .foregroundStyle(thermal.color)
                Text(s.temperature == nil ? loc["sys.tempUnavailable"] : loc["sys.tempSource"])
                    .font(.system(size: 10.5))
                    .foregroundStyle(p.faint)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private func networkCard(_ s: SystemSnapshot, _ p: Palette) -> some View {
        GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                Label(loc["live.network"], systemImage: "arrow.up.arrow.down")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(p.text)
                VStack(alignment: .leading, spacing: 8) {
                    rate(symbol: "arrow.down", color: Palette.success, value: s.netDown, label: loc["sys.download"], p: p)
                    rate(symbol: "arrow.up", color: Palette.info, value: s.netUp, label: loc["sys.upload"], p: p)
                }
                .padding(.vertical, 6)
                ZStack {
                    Sparkline(values: s.netDownHistory)
                    Sparkline(values: s.netUpHistory, color: Color(hex: 0x8C8C96))
                }
                .frame(height: 40)
            }
        }
    }

    private func rate(symbol: String, color: Color, value: Double, label: String, p: Palette) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Image(systemName: symbol).foregroundStyle(color).font(.system(size: 12, weight: .bold))
            VStack(alignment: .leading, spacing: 0) {
                Text(Fmt.rate(value))
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundStyle(p.text)
                    .contentTransition(.numericText())
                Text(label).font(.system(size: 10.5)).foregroundStyle(p.faint)
            }
        }
    }

    private func processesCard(_ s: SystemSnapshot, _ p: Palette) -> some View {
        let rows = processMode == 0 ? s.topCPU : s.topMemory
        return GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Label(loc["sys.topProcesses"], systemImage: "chart.bar.fill")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(p.text)
                    Spacer()
                    Picker("", selection: $processMode) {
                        Text(loc["live.cpu"]).tag(0)
                        Text(loc["live.memory"]).tag(1)
                    }
                    .pickerStyle(.segmented)
                    .labelsHidden()
                    .frame(width: 170)
                }
                if rows.isEmpty {
                    Text(loc["sys.collecting"]).font(.system(size: 12)).foregroundStyle(p.subtext).padding(.vertical, 12)
                } else {
                    ForEach(rows) { row in
                        HStack(spacing: 10) {
                            if let bundle = row.bundlePath {
                                AppIconView(path: bundle, size: 26)
                            } else {
                                Image(systemName: "gearshape.2")
                                    .frame(width: 26, height: 26)
                                    .foregroundStyle(p.subtext)
                            }
                            Text(row.count > 1 ? "\(row.name)  (\(row.count))" : row.name)
                                .font(.system(size: 12.5, weight: .semibold))
                                .foregroundStyle(p.text)
                                .lineLimit(1)
                            Spacer()
                            Text(String(format: "%.1f%%", row.cpu))
                                .font(.system(size: 12))
                                .foregroundStyle(processMode == 0 ? p.text : p.subtext)
                                .frame(width: 58, alignment: .trailing)
                            Text(Fmt.bytes(row.memory))
                                .font(.system(size: 12))
                                .foregroundStyle(processMode == 1 ? p.text : p.subtext)
                                .frame(width: 76, alignment: .trailing)
                            Button {
                                requestEnd(row)
                            } label: {
                                Image(systemName: "xmark.circle.fill").foregroundStyle(Palette.danger.opacity(0.85))
                            }
                            .buttonStyle(.plain)
                            .help(loc["sys.endTask"])
                        }
                        .padding(.vertical, 3)
                    }
                    .animation(.easeInOut(duration: 0.3), value: rows.map(\.name))
                }
                if let message {
                    Text(message).font(.system(size: 11.5)).foregroundStyle(Palette.warning)
                }
            }
        }
    }

    private func thisMacCard(_ s: SystemSnapshot, _ p: Palette) -> some View {
        let i = monitor.info
        return GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 8) {
                Label(loc["sys.thisMac"], systemImage: "laptopcomputer")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(p.text)
                InfoRow(symbol: "desktopcomputer", label: loc["sys.model"],
                        value: [i.modelName, i.modelID].filter { !$0.isEmpty }.joined(separator: " · "))
                InfoRow(symbol: "cpu", label: loc["right.chip"], value: i.chip)
                InfoRow(symbol: "square.stack.3d.up", label: loc["sys.arch"], value: i.architecture)
                if !i.gpu.isEmpty { InfoRow(symbol: "display", label: loc["sys.gpu"], value: i.gpu) }
                InfoRow(symbol: "memorychip", label: loc["right.memory"], value: i.memory)
                InfoRow(symbol: "apple.logo", label: "macOS", value: i.os)
                InfoRow(symbol: "clock", label: loc["sys.uptime"], value: Fmt.duration(s.uptime, loc))
            }
        }
    }

    private func drivesCard(_ s: SystemSnapshot, _ p: Palette) -> some View {
        GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                Label(loc["sys.drives"], systemImage: "internaldrive")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(p.text)
                ForEach(s.drives) { drive in
                    VStack(alignment: .leading, spacing: 5) {
                        HStack {
                            Text(drive.name).font(.system(size: 12, weight: .semibold)).foregroundStyle(p.text).lineLimit(1)
                            Spacer()
                            Text(loc.t("live.diskFree", ["free": Fmt.bytes(drive.free), "total": Fmt.bytes(drive.total)]))
                                .font(.system(size: 10.5)).foregroundStyle(p.faint)
                        }
                        GradientProgressBar(value: drive.usedFraction, height: 6)
                    }
                }
            }
        }
    }

    private func batteryCard(_ b: BatteryInfo, _ p: Palette) -> some View {
        GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 8) {
                Label(loc.t(b.charging ? "live.batteryCharging" : "live.battery", ["percent": "\(b.percent)"]),
                      systemImage: b.charging ? "battery.100.bolt" : "battery.75")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(p.text)
                GradientProgressBar(value: Double(b.percent) / 100, height: 6)
                let details = [
                    b.onAC ? loc["sys.onAC"] : b.minutesLeft.map { loc.t("sys.timeLeft", ["time": Fmt.duration(TimeInterval($0 * 60), loc)]) },
                    b.health.map { loc.t("sys.health", ["health": "\($0)"]) },
                    b.cycles.map { loc.t("sys.cycles", ["cycles": "\($0)"]) },
                ].compactMap { $0 }
                Text(details.joined(separator: " · "))
                    .font(.system(size: 11)).foregroundStyle(p.subtext)
            }
        }
    }

    // MARK: Helpers

    private func requestEnd(_ row: ProcessRow) {
        guard !SystemMonitor.protectedProcesses.contains(row.name) else {
            message = loc.t("sys.protected", ["name": row.name])
            return
        }
        message = nil
        state.confirm = ConfirmRequest(
            title: loc.t("sys.endTitle", ["name": row.name]),
            message: row.count > 1 ? loc.t("sys.endMany", ["count": "\(row.count)"]) : loc["sys.endOne"],
            details: row.bundlePath.map { [$0] } ?? [],
            confirmTitle: loc["sys.endTask"],
            symbol: "xmark.octagon.fill"
        ) {
            if monitor.end(row) {
                ActivityLog.append("log.endTask", row.name)
            } else {
                message = loc.t("sys.endFailed", ["name": row.name])
            }
        }
    }
}

// MARK: - Thermal state presentation

extension ProcessInfo.ThermalState {
    var shortKey: String {
        switch self {
        case .nominal: return "sys.thermal.nominalShort"
        case .fair: return "sys.thermal.fairShort"
        case .serious: return "sys.thermal.seriousShort"
        case .critical: return "sys.thermal.criticalShort"
        @unknown default: return "sys.thermal.nominalShort"
        }
    }

    var longKey: String {
        switch self {
        case .nominal: return "sys.thermal.nominal"
        case .fair: return "sys.thermal.fair"
        case .serious: return "sys.thermal.serious"
        case .critical: return "sys.thermal.critical"
        @unknown default: return "sys.thermal.nominal"
        }
    }

    var color: Color {
        switch self {
        case .nominal: return Palette.success
        case .fair: return Palette.warning
        case .serious: return Color(hex: 0xFF7A45)
        case .critical: return Palette.danger
        @unknown default: return Palette.success
        }
    }

    /// Gauge fill used when no temperature sensor can be read.
    var fraction: Double {
        switch self {
        case .nominal: return 0.25
        case .fair: return 0.5
        case .serious: return 0.75
        case .critical: return 1
        @unknown default: return 0.25
        }
    }
}
