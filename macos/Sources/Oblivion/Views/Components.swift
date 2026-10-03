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
    /// Slow breathing glow; off when motion is reduced.
    var glow = true

    var body: some View {
        let mark = Image(systemName: "trash.fill")
            .font(.system(size: size * 0.44, weight: .bold))
            .foregroundStyle(.white)
            .frame(width: size, height: size)
            .background(RoundedRectangle(cornerRadius: size * 0.3, style: .continuous).fill(Palette.brandGradient))
        if glow {
            mark.phaseAnimator([false, true]) { content, on in
                content.shadow(color: Palette.accent.opacity(on ? 0.8 : 0.35), radius: on ? 16 : 7)
            } animation: { _ in
                .easeInOut(duration: 2.4)
            }
        } else {
            mark.shadow(color: Palette.accent.opacity(0.45), radius: 8)
        }
    }
}

/// Round "i" button that opens About.
struct InfoButton: View {
    let help: String
    let action: () -> Void
    @State private var hover = false
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            Image(systemName: "info")
                .font(.system(size: 11, weight: .heavy))
                .foregroundStyle(hover ? Color.white : p.subtext)
                .frame(width: 24, height: 24)
                .background(Circle().fill(hover ? AnyShapeStyle(Palette.brandGradient) : AnyShapeStyle(p.cardStrong)))
                .overlay(Circle().stroke(p.stroke, lineWidth: 1))
                .contentShape(Circle())
        }
        .buttonStyle(.plain)
        .help(help)
        .scaleEffect(hover ? 1.1 : 1)
        .animation(.spring(response: 0.25, dampingFraction: 0.7), value: hover)
        .onHover { hover = $0 }
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

// MARK: - Live gauges

/// Circular gauge (0…1) with a gradient arc; value changes animate smoothly.
struct RingGauge: View {
    var value: Double
    var colors: [UInt32] = [0x6E5BFF, 0xB45BFF]
    var lineWidth: CGFloat = 8
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        ZStack {
            Circle().stroke(p.cardStrong, lineWidth: lineWidth)
            Circle()
                .trim(from: 0, to: max(0.002, min(1, value.isFinite ? value : 0)))
                .stroke(Palette.gradient(colors), style: StrokeStyle(lineWidth: lineWidth, lineCap: .round))
                .rotationEffect(.degrees(-90))
                .animation(.easeOut(duration: 0.65), value: value)
        }
        .padding(lineWidth / 2)
    }
}

/// Compact ring gauge with the value in the middle and a caption below.
struct MiniGauge: View {
    let title: String
    let value: Double
    let text: String
    var colors: [UInt32] = [0x6E5BFF, 0xB45BFF]
    var size: CGFloat = 64
    var animate = true
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 6) {
            ZStack {
                RingGauge(value: value, colors: colors, lineWidth: size * 0.09)
                Text(text)
                    .font(.system(size: size * 0.2, weight: .bold, design: .rounded))
                    .foregroundStyle(p.text)
                    .lineLimit(1)
                    .minimumScaleFactor(0.6)
                    .padding(.horizontal, size * 0.14)
                    .contentTransition(.numericText())
                    .animation(animate ? .snappy : nil, value: text)
            }
            .frame(width: size, height: size)
            Text(title)
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(p.subtext)
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity)
    }
}

/// A one-off particle burst (about a second) for "done" moments; draws nothing afterwards
/// and is skipped entirely when motion is reduced.
struct Burst: View {
    var count = 28
    var spread: Double = 90
    @EnvironmentObject private var prefs: Prefs
    @State private var start = Date()
    @State private var finished = false

    private static let colors: [Color] = [Color(hex: 0x6E5BFF), Color(hex: 0xB45BFF), Color(hex: 0x22C55E),
                                          Color(hex: 0x3A8DFF), Color(hex: 0xF5A524), Color(hex: 0xFF7AC6)]

    var body: some View {
        if prefs.calmMotion || finished {
            Color.clear.allowsHitTesting(false)
        } else {
            TimelineView(.animation) { context in
                Canvas { ctx, size in
                    let t = context.date.timeIntervalSince(start)
                    let progress = min(1, max(0, t / 1.05))
                    let eased = 1 - pow(1 - progress, 3)
                    let center = CGPoint(x: size.width / 2, y: size.height / 2)
                    ctx.opacity = 1 - progress * progress
                    for i in 0..<count {
                        let angle = Double(i) / Double(count) * 2 * Double.pi + Double(i % 3) * 0.21
                        let distance = eased * spread * (0.65 + Double((i * 37) % 40) / 100)
                        let x = center.x + CGFloat(cos(angle) * distance)
                        let y = center.y + CGFloat(sin(angle) * distance + progress * progress * 26)
                        let r: CGFloat = i % 3 == 0 ? 4 : 2.8
                        ctx.fill(Path(ellipseIn: CGRect(x: x - r, y: y - r, width: r * 2, height: r * 2)),
                                 with: .color(Self.colors[i % Self.colors.count]))
                    }
                }
            }
            .allowsHitTesting(false)
            .task {
                try? await Task.sleep(nanoseconds: 1_150_000_000)
                finished = true
            }
        }
    }
}

/// Small area chart for a rolling history of values.
struct Sparkline: View {
    let values: [Double]
    var maximum: Double? = nil
    var color: Color = Color(hex: 0x8B7BFF)

    var body: some View {
        GeometryReader { geo in
            let w = geo.size.width
            let h = geo.size.height
            let top = maximum ?? max(values.max() ?? 1, 1e-9) * 1.15
            let step = values.count > 1 ? w / CGFloat(values.count - 1) : w
            let points = values.enumerated().map { item -> CGPoint in
                let ratio = min(max(item.element / top, 0), 1)
                return CGPoint(x: CGFloat(item.offset) * step, y: h - CGFloat(ratio) * (h - 2) - 1)
            }
            if points.count > 1 {
                ZStack {
                    Path { path in
                        path.move(to: CGPoint(x: 0, y: h))
                        for point in points { path.addLine(to: point) }
                        path.addLine(to: CGPoint(x: w, y: h))
                        path.closeSubpath()
                    }
                    .fill(LinearGradient(colors: [color.opacity(0.35), color.opacity(0)], startPoint: .top, endPoint: .bottom))
                    Path { path in path.addLines(points) }
                        .stroke(color, style: StrokeStyle(lineWidth: 1.6, lineJoin: .round))
                }
            }
        }
    }
}

/// Pulsing "live" indicator. Skipped when motion is reduced.
struct LiveDot: View {
    var color: Color = Palette.success
    @EnvironmentObject private var prefs: Prefs
    @State private var pulse = false

    var body: some View {
        ZStack {
            Circle().fill(color.opacity(0.4))
                .frame(width: 14, height: 14)
                .scaleEffect(pulse ? 1 : 0.4)
                .opacity(pulse ? 0 : 1)
            Circle().fill(color).frame(width: 7, height: 7)
        }
        .frame(width: 14, height: 14)
        .onAppear {
            guard !prefs.calmMotion else { return }
            withAnimation(.easeOut(duration: 1.7).repeatForever(autoreverses: false)) { pulse = true }
        }
    }
}

/// Fade + rise entrance, once, when a card first appears.
struct AppearIn: ViewModifier {
    var delay: Double = 0
    @EnvironmentObject private var prefs: Prefs
    @State private var shown = false

    func body(content: Content) -> some View {
        content
            .opacity(shown ? 1 : 0)
            .offset(y: shown ? 0 : 12)
            .onAppear {
                if prefs.calmMotion {
                    shown = true
                } else {
                    withAnimation(.spring(response: 0.5, dampingFraction: 0.86).delay(delay)) { shown = true }
                }
            }
    }
}

extension View {
    func appearIn(_ delay: Double = 0) -> some View { modifier(AppearIn(delay: delay)) }
}

// MARK: - Confirmation sheet

/// A destructive action waiting for the user's OK, with the exact list of what it affects.
struct ConfirmRequest: Identifiable {
    let id = UUID()
    var title: String
    var message: String
    var details: [String] = []
    var confirmTitle: String
    var destructive = true
    var symbol = "exclamationmark.triangle.fill"
    let action: () -> Void
}

struct ConfirmSheet: View {
    let request: ConfirmRequest
    @EnvironmentObject private var loc: Loc
    @Environment(\.dismiss) private var dismiss
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 14) {
                GradientBadge(symbol: request.symbol,
                              colors: request.destructive ? [0xFF6B6B, 0xE5484D] : [0xF5A524, 0xFF7A45], size: 44)
                Text(request.title)
                    .font(.system(size: 17, weight: .bold, design: .rounded))
                    .foregroundStyle(p.text)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Text(request.message)
                .font(.system(size: 12.5))
                .foregroundStyle(p.subtext)
                .fixedSize(horizontal: false, vertical: true)
            if !request.details.isEmpty {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 4) {
                        ForEach(Array(request.details.prefix(400).enumerated()), id: \.offset) { _, line in
                            Text(line)
                                .font(.system(size: 11))
                                .foregroundStyle(p.subtext)
                                .lineLimit(1)
                                .truncationMode(.middle)
                        }
                        if request.details.count > 400 {
                            Text("+ \(request.details.count - 400)").font(.system(size: 11)).foregroundStyle(p.faint)
                        }
                    }
                    .padding(10)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .frame(maxHeight: 190)
                .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(p.card))
            }
            HStack {
                Spacer()
                Button(loc["action.cancel"]) { dismiss() }
                    .buttonStyle(OBButtonStyle(kind: .secondary))
                    .keyboardShortcut(.cancelAction)
                Button(request.confirmTitle) {
                    dismiss()
                    request.action()
                }
                .buttonStyle(OBButtonStyle(kind: request.destructive ? .danger : .primary))
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(24)
        .frame(width: 500)
        .background(LinearGradient(colors: [p.tintTop, p.tintBottom], startPoint: .topLeading, endPoint: .bottomTrailing))
    }
}

/// Tri-state checkbox for "select everything in this group".
struct TriStateCheckbox: View {
    let state: Bool?
    let action: () -> Void
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        Button(action: action) {
            ZStack {
                RoundedRectangle(cornerRadius: 4, style: .continuous)
                    .fill(state == false ? Color.clear : Palette.accent)
                RoundedRectangle(cornerRadius: 4, style: .continuous)
                    .stroke(state == false ? p.subtext : Palette.accent, lineWidth: 1.2)
                if state == true {
                    Image(systemName: "checkmark").font(.system(size: 9, weight: .heavy)).foregroundStyle(.white)
                } else if state == nil {
                    Image(systemName: "minus").font(.system(size: 9, weight: .heavy)).foregroundStyle(.white)
                }
            }
            .frame(width: 15, height: 15)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}
