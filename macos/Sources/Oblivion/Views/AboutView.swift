import AppKit
import SwiftUI

enum AppInfo {
    static var version: String {
        Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "3.0.0"
    }

    static let github = URL(string: "https://github.com/anilg12")!
    static let repository = URL(string: "https://github.com/anilg12/Oblivion-Uninstaller")!
    static let releases = URL(string: "https://github.com/anilg12/Oblivion-Uninstaller/releases")!
}

/// About Oblivion: who made it, links and what's new. Opened from the ⓘ button or the app menu.
struct AboutView: View {
    @EnvironmentObject private var loc: Loc
    @EnvironmentObject private var prefs: Prefs
    @Environment(\.dismiss) private var dismiss
    @Environment(\.colorScheme) private var scheme
    @State private var ink: CGFloat = 0
    @State private var spin = false

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 0) {
            header
            avatar
                .padding(.horizontal, 28)
                .padding(.top, -52)

            VStack(alignment: .leading, spacing: 6) {
                SignatureText(size: 44)
                    .mask(alignment: .leading) {
                        GeometryReader { geo in
                            Rectangle().frame(width: geo.size.width * ink)
                        }
                    }
                Text(loc["about.role"])
                    .font(.system(size: 13.5))
                    .foregroundStyle(p.subtext)
                HStack(spacing: 8) {
                    Button { NSWorkspace.shared.open(AppInfo.github) } label: {
                        Label("github.com/anilg12", systemImage: "chevron.left.forwardslash.chevron.right")
                    }
                    .buttonStyle(OBButtonStyle(kind: .primary))
                    Button { NSWorkspace.shared.open(AppInfo.repository) } label: {
                        Label(loc["about.repo"], systemImage: "star")
                    }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    Button { NSWorkspace.shared.open(AppInfo.releases) } label: {
                        Label(loc["about.releases"], systemImage: "arrow.down.circle")
                    }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                }
                .padding(.top, 8)
            }
            .padding(.horizontal, 28)
            .padding(.top, 10)

            Divider().overlay(p.stroke).padding(.horizontal, 28).padding(.vertical, 16)

            VStack(alignment: .leading, spacing: 9) {
                Label(loc["about.whatsNew"], systemImage: "sparkles")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(p.text)
                ForEach(1...7, id: \.self) { i in
                    HStack(alignment: .top, spacing: 10) {
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundStyle(Palette.success)
                            .font(.system(size: 13))
                        Text(loc["about.new\(i)"])
                            .font(.system(size: 12.5))
                            .foregroundStyle(p.text)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    .appearIn(0.15 + Double(i) * 0.05)
                }
            }
            .padding(.horizontal, 28)

            HStack {
                Text(loc["about.copyright"])
                    .font(.system(size: 11))
                    .foregroundStyle(p.faint)
                Spacer()
                Button(loc["action.close"]) { dismiss() }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    .keyboardShortcut(.cancelAction)
            }
            .padding(.horizontal, 28)
            .padding(.vertical, 20)
        }
        .frame(width: 560)
        .background(LinearGradient(colors: [p.tintTop, p.tintBottom], startPoint: .topLeading, endPoint: .bottomTrailing))
        .onAppear {
            if prefs.calmMotion {
                ink = 1
            } else {
                withAnimation(.easeInOut(duration: 1.3).delay(0.25)) { ink = 1 }
                spin = true
            }
        }
    }

    private var header: some View {
        ZStack(alignment: .topLeading) {
            // The decorative circles live in an overlay so they never change the header's layout.
            Palette.brandGradient
                .overlay(alignment: .topLeading) {
                    ZStack(alignment: .topLeading) {
                        Circle().fill(Color.white.opacity(0.12)).frame(width: 260, height: 260).offset(x: 360, y: -130)
                        Circle().fill(Color.white.opacity(0.08)).frame(width: 150, height: 150).offset(x: -50, y: 80)
                        Circle().fill(Color.white.opacity(0.07)).frame(width: 70, height: 70).offset(x: 300, y: 110)
                    }
                }
            HStack(spacing: 10) {
                Image(nsImage: NSApp.applicationIconImage)
                    .resizable()
                    .frame(width: 34, height: 34)
                VStack(alignment: .leading, spacing: 1) {
                    Text("Oblivion").font(.system(size: 15, weight: .bold)).foregroundStyle(.white)
                    Text(loc.t("about.version", ["version": AppInfo.version]))
                        .font(.system(size: 11)).foregroundStyle(.white.opacity(0.8))
                }
            }
            .padding(24)
            HStack {
                Spacer()
                Button { dismiss() } label: {
                    Image(systemName: "xmark")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(.white)
                        .frame(width: 28, height: 28)
                        .background(Circle().fill(Color.white.opacity(0.18)))
                }
                .buttonStyle(.plain)
                .padding(16)
            }
        }
        .frame(height: 150)
        .clipped()
    }

    private var avatar: some View {
        ZStack {
            Circle()
                .stroke(AngularGradient(colors: [Color(hex: 0x8FC0FF), Color(hex: 0xB45BFF), Color(hex: 0xFF7AC6), Color(hex: 0x8FC0FF)],
                                        center: .center), lineWidth: 4)
                .frame(width: 104, height: 104)
                .rotationEffect(.degrees(spin ? 360 : 0))
                .animation(spin ? .linear(duration: 9).repeatForever(autoreverses: false) : .default, value: spin)
            Circle().fill(Palette(scheme).tintTop).frame(width: 92, height: 92)
            Circle().fill(Palette.brandGradient).frame(width: 84, height: 84)
            Text("AG")
                .font(.system(size: 32, weight: .bold, design: .rounded))
                .foregroundStyle(.white)
        }
    }
}
