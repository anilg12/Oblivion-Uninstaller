import AppKit
import SwiftUI

// MARK: - Window chrome (blur / translucency)

/// AppKit vibrancy view — gives the window its frosted-glass look.
struct VisualEffectView: NSViewRepresentable {
    var material: NSVisualEffectView.Material
    var blending: NSVisualEffectView.BlendingMode = .behindWindow

    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = material
        view.blendingMode = blending
        view.state = .active
        return view
    }

    func updateNSView(_ view: NSVisualEffectView, context: Context) {
        view.material = material
        view.blendingMode = blending
    }
}

/// Makes the hosting window translucent and draggable by its background.
struct WindowConfigurator: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async {
            guard let window = view.window else { return }
            window.isOpaque = false
            window.backgroundColor = .clear
            window.titlebarAppearsTransparent = true
            window.isMovableByWindowBackground = true
            window.hasShadow = true
            window.invalidateShadow()
        }
        return view
    }

    func updateNSView(_ nsView: NSView, context: Context) {}
}

struct WindowBackdrop: View {
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        ZStack {
            VisualEffectView(material: scheme == .dark ? .hudWindow : .sidebar)
            LinearGradient(colors: [p.tintTop.opacity(0.86), p.tintBottom.opacity(0.92)],
                           startPoint: .topLeading, endPoint: .bottomTrailing)
            RadialGradient(colors: [Palette.accent.opacity(p.dark ? 0.30 : 0.14), .clear],
                           center: .topLeading, startRadius: 10, endRadius: 560)
            RadialGradient(colors: [Palette.accent2.opacity(p.dark ? 0.24 : 0.10), .clear],
                           center: .bottomTrailing, startRadius: 10, endRadius: 600)
        }
        .ignoresSafeArea()
    }
}

// MARK: - Cards

struct GlassCard<Content: View>: View {
    @Environment(\.colorScheme) private var scheme
    let padding: CGFloat
    let radius: CGFloat
    let content: () -> Content

    init(padding: CGFloat = 16, radius: CGFloat = 16, @ViewBuilder content: @escaping () -> Content) {
        self.padding = padding
        self.radius = radius
        self.content = content
    }

    var body: some View {
        let p = Palette(scheme)
        content()
            .padding(padding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: radius, style: .continuous).fill(p.card))
            .overlay(RoundedRectangle(cornerRadius: radius, style: .continuous).stroke(p.stroke, lineWidth: 1))
    }
}

// MARK: - Buttons

enum OBKind {
    case primary, secondary, danger, ghost
}

struct OBButtonStyle: ButtonStyle {
    var kind: OBKind = .secondary
    var large = false

    func makeBody(configuration: Configuration) -> some View {
        OBButtonBody(configuration: configuration, kind: kind, large: large)
    }
}

private struct OBButtonBody: View {
    let configuration: ButtonStyleConfiguration
    let kind: OBKind
    let large: Bool
    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.colorScheme) private var scheme
    @State private var hover = false

    var body: some View {
        let p = Palette(scheme)
        configuration.label
            .font(.system(size: large ? 15 : 13, weight: .semibold))
            .foregroundStyle(kind == .primary || kind == .danger ? Color.white : p.text)
            .padding(.horizontal, large ? 16 : 13)
            .padding(.vertical, large ? 11 : 7)
            .background(fillView(p))
            .clipShape(RoundedRectangle(cornerRadius: large ? 12 : 10, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: large ? 12 : 10, style: .continuous)
                    .stroke(kind == .secondary ? p.stroke : Color.clear, lineWidth: 1)
            )
            .shadow(color: shadowColor.opacity(hover && isEnabled ? 0.45 : 0), radius: 10, y: 4)
            .scaleEffect(configuration.isPressed ? 0.97 : (hover ? 1.02 : 1))
            .opacity(isEnabled ? 1 : 0.45)
            .animation(.spring(response: 0.25, dampingFraction: 0.7), value: hover)
            .animation(.spring(response: 0.2, dampingFraction: 0.7), value: configuration.isPressed)
            .onHover { hover = $0 && isEnabled }
            .contentShape(Rectangle())
    }

    private var shadowColor: Color {
        switch kind {
        case .primary: return Palette.accent
        case .danger: return Palette.danger
        default: return .clear
        }
    }

    @ViewBuilder
    private func fillView(_ p: Palette) -> some View {
        switch kind {
        case .primary: Palette.brandGradient
        case .danger: Palette.danger
        case .secondary: p.cardStrong
        case .ghost: Color.clear
        }
    }
}

// MARK: - Branding

struct LogoMark: View {
    var size: CGFloat = 42

    var body: some View {
        Image(systemName: "trash.fill")
            .font(.system(size: size * 0.44, weight: .bold))
            .foregroundStyle(.white)
            .frame(width: size, height: size)
            .background(RoundedRectangle(cornerRadius: size * 0.3, style: .continuous).fill(Palette.brandGradient))
            .phaseAnimator([false, true]) { content, glow in
                content.shadow(color: Palette.accent.opacity(glow ? 0.8 : 0.35), radius: glow ? 16 : 7)
            } animation: { _ in
                .easeInOut(duration: 1.8)
            }
    }
}

struct SignatureText: View {
    var size: CGFloat = 30
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        Text("Anıl Gül")
            .font(.custom("SnellRoundhand-Bold", size: size))
            .foregroundStyle(Palette.signature(scheme == .dark))
    }
}

// MARK: - Icons & badges

struct AppIconView: View {
    let path: String
    var size: CGFloat = 28

    var body: some View {
        Image(nsImage: IconCache.shared.icon(for: path))
            .resizable()
            .interpolation(.high)
            .frame(width: size, height: size)
    }
}

/// Large, colorful rounded-square icon used for tools and stats.
struct GradientBadge: View {
    let symbol: String
    let colors: [UInt32]
    var size: CGFloat = 56

    var body: some View {
        Image(systemName: symbol)
            .font(.system(size: size * 0.44, weight: .semibold))
            .foregroundStyle(.white)
            .frame(width: size, height: size)
            .background(RoundedRectangle(cornerRadius: size * 0.28, style: .continuous).fill(Palette.gradient(colors)))
            .shadow(color: Color(hex: colors.last ?? 0x7C6CF6).opacity(0.45), radius: size * 0.2, y: size * 0.08)
    }
}

struct Chip: View {
    let text: String
    let color: Color

    var body: some View {
        Text(text)
            .font(.system(size: 10.5, weight: .bold))
            .foregroundStyle(color)
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background(Capsule().fill(color.opacity(0.16)))
    }
}

// MARK: - Page scaffolding

struct PageHeader<Trailing: View>: View {
    let title: String
    let subtitle: String
    let trailing: Trailing
    @Environment(\.colorScheme) private var scheme

    init(_ title: String, _ subtitle: String, @ViewBuilder trailing: () -> Trailing) {
        self.title = title
        self.subtitle = subtitle
        self.trailing = trailing()
    }

    var body: some View {
        let p = Palette(scheme)
        HStack(alignment: .center, spacing: 12) {
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.system(size: 26, weight: .bold, design: .rounded))
                    .foregroundStyle(p.text)
                Text(subtitle)
                    .font(.system(size: 13))
                    .foregroundStyle(p.subtext)
            }
            Spacer(minLength: 8)
            trailing
        }
    }
}

extension PageHeader where Trailing == EmptyView {
    init(_ title: String, _ subtitle: String) {
        self.init(title, subtitle) { EmptyView() }
    }
}

struct SearchField: View {
    @Binding var text: String
    let placeholder: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        HStack(spacing: 6) {
            Image(systemName: "magnifyingglass").foregroundStyle(p.subtext)
            TextField(placeholder, text: $text)
                .textFieldStyle(.plain)
            if !text.isEmpty {
                Button { text = "" } label: {
                    Image(systemName: "xmark.circle.fill").foregroundStyle(p.faint)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 7)
        .frame(width: 250)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(p.cardStrong))
        .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).stroke(p.stroke, lineWidth: 1))
    }
}

struct EmptyStateView: View {
    let symbol: String
    let text: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 12) {
            Image(systemName: symbol)
                .font(.system(size: 38, weight: .light))
                .foregroundStyle(p.faint)
            Text(text)
                .font(.system(size: 13))
                .foregroundStyle(p.subtext)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 40)
    }
}

struct LoadingView: View {
    let text: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        VStack(spacing: 14) {
            ProgressView().controlSize(.large)
            Text(text).foregroundStyle(Palette(scheme).subtext)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

/// Stat tile with an animated (rolling) number.
struct StatCard: View {
    let symbol: String
    let colors: [UInt32]
    let value: String
    let label: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        GlassCard(padding: 16) {
            VStack(alignment: .leading, spacing: 10) {
                GradientBadge(symbol: symbol, colors: colors, size: 36)
                Text(value)
                    .font(.system(size: 24, weight: .bold, design: .rounded))
                    .foregroundStyle(p.text)
                    .contentTransition(.numericText())
                    .animation(.snappy, value: value)
                    .lineLimit(1)
                    .minimumScaleFactor(0.6)
                Text(label)
                    .font(.system(size: 12))
                    .foregroundStyle(p.subtext)
                    .lineLimit(1)
            }
        }
    }
}

/// Gradient progress bar. `value == nil` shows an indeterminate sweep.
/// Driven by TimelineView rather than repeating animations, so it never
/// interferes with view transitions.
struct GradientProgressBar: View {
    var value: Double? = nil
    var height: CGFloat = 6
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        GeometryReader { geo in
            let width = geo.size.width
            ZStack(alignment: .leading) {
                Capsule().fill(p.cardStrong)
                if let value {
                    Capsule()
                        .fill(Palette.brandGradient)
                        .frame(width: max(height, width * min(max(value, 0), 1)))
                        .animation(.easeOut(duration: 0.3), value: value)
                } else {
                    TimelineView(.animation) { context in
                        let t = context.date.timeIntervalSinceReferenceDate
                        let phase = CGFloat(t.truncatingRemainder(dividingBy: 1.4) / 1.4)
                        Capsule()
                            .fill(Palette.brandGradient)
                            .frame(width: width * 0.32)
                            .offset(x: -width * 0.32 + phase * width * 1.32)
                    }
                }
            }
            .clipShape(Capsule())
        }
        .frame(height: height)
    }
}

/// Small helper for a hover "lift" effect on cards.
struct HoverLift: ViewModifier {
    @State private var hover = false

    func body(content: Content) -> some View {
        content
            .scaleEffect(hover ? 1.015 : 1)
            .shadow(color: Color.black.opacity(hover ? 0.22 : 0), radius: 14, y: 6)
            .animation(.spring(response: 0.3, dampingFraction: 0.75), value: hover)
            .onHover { hover = $0 }
    }
}

extension View {
    func hoverLift() -> some View { modifier(HoverLift()) }
}
