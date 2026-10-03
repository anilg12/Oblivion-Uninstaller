import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var prefs: Prefs
    @EnvironmentObject private var state: AppState
    @EnvironmentObject private var loc: Loc
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                PageHeader(loc["nav.settings"], loc["set.subtitle"])

                settingRow(symbol: "paintpalette.fill", colors: [0x6E5BFF, 0xB45BFF],
                           title: loc["set.appearance"], detail: loc["set.appearance.d"], p: p) {
                    Picker("", selection: $prefs.appearance) {
                        Text(loc["set.system"]).tag("system")
                        Text(loc["set.light"]).tag("light")
                        Text(loc["set.dark"]).tag("dark")
                    }
                    .pickerStyle(.segmented)
                    .labelsHidden()
                    .frame(width: 260)
                }
                .appearIn(0)

                settingRow(symbol: "globe", colors: [0x3A8DFF, 0x6F5BFF],
                           title: loc["set.language"], detail: loc["set.language.d"], p: p) {
                    Picker("", selection: $loc.lang) {
                        Text("Türkçe").tag("tr")
                        Text("English").tag("en")
                    }
                    .pickerStyle(.segmented)
                    .labelsHidden()
                    .frame(width: 200)
                }
                .appearIn(0.04)

                settingRow(symbol: "gauge.with.dots.needle.67percent", colors: [0x18C29C, 0x2E8BFF],
                           title: loc["set.livePanel"], detail: loc["set.livePanel.d"], p: p) {
                    Toggle("", isOn: $prefs.showLivePanel).toggleStyle(.switch).labelsHidden()
                }
                .appearIn(0.08)

                settingRow(symbol: "figure.walk.motion", colors: [0x9B5BFF, 0xE15BBE],
                           title: loc["set.motion"], detail: loc["set.motion.d"], p: p) {
                    Toggle("", isOn: $prefs.reduceMotion).toggleStyle(.switch).labelsHidden()
                }
                .appearIn(0.12)

                settingRow(symbol: "hand.raised.fill", colors: [0xFF9F45, 0xF5A524],
                           title: loc["set.confirm"], detail: loc["set.confirm.d"], p: p) {
                    Toggle("", isOn: $prefs.confirmBeforeDelete).toggleStyle(.switch).labelsHidden()
                }
                .appearIn(0.16)

                settingRow(symbol: "lock.shield.fill", colors: [0xFF6B6B, 0xE5484D],
                           title: loc["set.fda"], detail: state.hasFullDiskAccess ? loc["set.fda.on"] : loc["set.fda.off"], p: p) {
                    HStack(spacing: 8) {
                        Chip(text: state.hasFullDiskAccess ? loc["set.granted"] : loc["set.notGranted"],
                             color: state.hasFullDiskAccess ? Palette.success : Palette.warning)
                        Button(loc["fda.open"]) { SystemInfo.openFullDiskAccessSettings() }
                            .buttonStyle(OBButtonStyle(kind: .secondary))
                        Button { state.refreshSystem() } label: { Image(systemName: "arrow.clockwise") }
                            .buttonStyle(OBButtonStyle(kind: .secondary))
                    }
                }
                .appearIn(0.2)

                about(p)
                    .appearIn(0.24)
            }
            .padding(26)
        }
    }

    private func settingRow<Control: View>(symbol: String, colors: [UInt32], title: String, detail: String,
                                           p: Palette, @ViewBuilder control: () -> Control) -> some View {
        HStack(spacing: 14) {
            GradientBadge(symbol: symbol, colors: colors, size: 40)
            VStack(alignment: .leading, spacing: 3) {
                Text(title).font(.system(size: 14, weight: .semibold)).foregroundStyle(p.text)
                Text(detail).font(.system(size: 12)).foregroundStyle(p.subtext)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 12)
            control()
        }
        .padding(16)
        .background(RoundedRectangle(cornerRadius: 16, style: .continuous).fill(p.card))
        .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).stroke(p.stroke, lineWidth: 1))
    }

    private func about(_ p: Palette) -> some View {
        HStack(alignment: .center, spacing: 14) {
            LogoMark(size: 54, glow: !prefs.calmMotion)
            VStack(alignment: .leading, spacing: 4) {
                Text("Oblivion").font(.system(size: 22, weight: .heavy, design: .rounded)).foregroundStyle(p.text)
                Text(loc.t("set.version", ["version": AppInfo.version]))
                    .font(.system(size: 12)).foregroundStyle(p.subtext)
                Text(loc["set.about"]).font(.system(size: 12.5)).foregroundStyle(p.subtext)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 12)
            Button { state.showAbout = true } label: {
                Label(loc["set.aboutOpen"], systemImage: "info.circle")
            }
            .buttonStyle(OBButtonStyle(kind: .secondary))
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 18, style: .continuous).fill(p.card))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(p.stroke, lineWidth: 1))
    }
}
