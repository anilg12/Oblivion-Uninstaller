import AppKit
import SwiftUI

enum AppInfo {
    static var version: String {
        Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "3.1.0"
    }

    static let github = URL(string: "https://github.com/anilg12")!
    static let repository = URL(string: "https://github.com/anilg12/Oblivion-Uninstaller")!
    static let releases = URL(string: "https://github.com/anilg12/Oblivion-Uninstaller/releases")!
}

// about sheet (i button or the app menu)
struct AboutView: View {
    @EnvironmentObject private var loc: Loc
    @EnvironmentObject private var prefs: Prefs
    @Environment(\.dismiss) private var dismiss
    @Environment(\.colorScheme) private var scheme
    @State private var ink: CGFloat = 0

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider().overlay(p.stroke).padding(.horizontal, 28).padding(.top, 20).padding(.bottom, 22)
            avatar
                .padding(.horizontal, 28)

            VStack(alignment: .leading, spacing: 6) {
                SignatureText(size: 30)
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
            .padding(.top, 14)

            Divider().overlay(p.stroke).padding(.horizontal, 28).padding(.vertical, 18)

            VStack(alignment: .leading, spacing: 9) {
                Text(loc["about.whatsNew"])
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(p.text)
                    .padding(.bottom, 2)
                ForEach(1...7, id: \.self) { i in
                    HStack(alignment: .top, spacing: 10) {
                        Image(systemName: "checkmark")
                            .foregroundStyle(p.subtext)
                            .font(.system(size: 10, weight: .semibold))
                            .frame(width: 18, height: 18)
                            .background(Circle().fill(p.cardStrong))
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
            .padding(.top, 20)
            .padding(.bottom, 24)
        }
        .frame(width: 560)
        .background(p.dark ? Color(hex: 0x1E1E21) : Color.white)
        .onAppear {
            if prefs.calmMotion {
                ink = 1
            } else {
                withAnimation(.easeOut(duration: 0.9).delay(0.15)) { ink = 1 }
            }
        }
    }

    private var header: some View {
        let p = Palette(scheme)
        return HStack(spacing: 12) {
            LogoMark(size: 44)
            VStack(alignment: .leading, spacing: 2) {
                Text("Oblivion").font(.system(size: 17, weight: .semibold)).foregroundStyle(p.text)
                Text(loc.t("about.version", ["version": AppInfo.version]))
                    .font(.system(size: 11.5)).foregroundStyle(p.faint)
            }
            Spacer()
            Button { dismiss() } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(p.subtext)
                    .frame(width: 28, height: 28)
                    .background(Circle().fill(p.cardStrong))
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 28)
        .padding(.top, 26)
    }

    private var avatar: some View {
        let p = Palette(scheme)
        return Text("AG")
            .font(.system(size: 19, weight: .semibold))
            .foregroundStyle(p.text)
            .frame(width: 56, height: 56)
            .background(Circle().fill(p.cardStrong))
            .overlay(Circle().stroke(p.stroke, lineWidth: 1))
    }
}
