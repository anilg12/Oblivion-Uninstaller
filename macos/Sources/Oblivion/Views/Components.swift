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
            VisualEffectView(material: .sidebar)
            p.tintTop.opacity(0.35)
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

    init(padding: CGFloat = 18, radius: CGFloat = 12, @ViewBuilder content: @escaping () -> Content) {
        self.padding = padding
        self.radius = radius
        self.content = content
    }

    var body: some View {
        let p = Palette(scheme)
        content()
            .padding(padding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                RoundedRectangle(cornerRadius: min(radius, 12), style: .continuous)
                    .fill(p.card)
                    .shadow(color: p.shadow, radius: 1.5, y: 1)
            )
            .overlay(RoundedRectangle(cornerRadius: min(radius, 12), style: .continuous).stroke(p.stroke, lineWidth: 1))
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
        let shape = RoundedRectangle(cornerRadius: large ? 10 : 8, style: .continuous)
        configuration.label
            .font(.system(size: large ? 14 : 13, weight: .medium))
            .foregroundStyle(kind == .primary || kind == .danger ? Color.white : p.text)
            .padding(.horizontal, large ? 18 : 13)
            .padding(.vertical, large ? 10 : 6.5)
            .background(fillView(p))
            .overlay(p.text.opacity(configuration.isPressed ? 0.10 : (hover ? 0.05 : 0)).allowsHitTesting(false))
            .clipShape(shape)
            .overlay(shape.stroke(kind == .secondary ? p.strokeStrong : Color.clear, lineWidth: 1))
            .shadow(color: kind == .ghost ? .clear : p.shadow, radius: 1, y: 1)
            .opacity(isEnabled ? 1 : 0.4)
            .animation(.easeOut(duration: 0.12), value: hover)
            .onHover { hover = $0 && isEnabled }
            .contentShape(Rectangle())
    }

    @ViewBuilder
    private func fillView(_ p: Palette) -> some View {
        switch kind {
        case .primary: Palette.accent
        case .danger: Palette.danger
        case .secondary: p.card
        case .ghost: Color.clear
        }
    }
}

// MARK: - Branding

struct LogoMark: View {
    var size: CGFloat = 42
    /// Kept for existing call sites; the mark no longer animates.
    var glow = true

    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: size * 0.225, style: .continuous)
                .fill(LinearGradient(colors: [Color(hex: 0x2E3038), Color(hex: 0x16171B)], startPoint: .top, endPoint: .bottom))
            RoundedRectangle(cornerRadius: size * 0.225, style: .continuous)
                .strokeBorder(Color.white.opacity(0.08), lineWidth: max(0.5, size * 0.006))
            OblivionGlyph(lineWidth: size * 0.098)
                .frame(width: size * 0.53, height: size * 0.53)
        }
        .frame(width: size, height: size)
    }
}

/// The ring-and-particles mark on its own (no tile).
struct OblivionGlyph: View {
    private struct Particle {
        let deg: Double
        let out: CGFloat
        let size: CGFloat
        let opacity: Double
    }

    /// Angle, distance past the ring (in line widths), radius (in line widths), opacity.
    private static let particles = [
        Particle(deg: -46, out: 0.10, size: 0.36, opacity: 1.0),
        Particle(deg: -39, out: 1.15, size: 0.24, opacity: 0.8),
        Particle(deg: -33, out: 2.0, size: 0.14, opacity: 0.55),
    ]

    var lineWidth: CGFloat
    var ring: Color = Color(hex: 0xF2F2F4)
    var dot: Color = Color(hex: 0x8EA2FF)

    var body: some View {
        GeometryReader { geo in
            let d = min(geo.size.width, geo.size.height)
            let r = d / 2
            let c = CGPoint(x: geo.size.width / 2, y: geo.size.height / 2)
            ZStack {
                // Opening from -66° to -26° (clockwise from 3 o'clock, y down): the arc covers the other 320°.
                Circle()
                    .trim(from: 0, to: 320.0 / 360.0)
                    .stroke(ring, style: StrokeStyle(lineWidth: lineWidth, lineCap: .round))
                    .rotationEffect(.degrees(-26))
                    .frame(width: d, height: d)
                ForEach(Self.particles.indices, id: \.self) { i in
                    let p = Self.particles[i]
                    let rr = r + lineWidth * p.out
                    let a = p.deg * .pi / 180
                    Circle()
                        .fill(dot.opacity(p.opacity))
                        .frame(width: lineWidth * p.size * 2, height: lineWidth * p.size * 2)
                        .position(x: c.x + rr * CGFloat(cos(a)), y: c.y + rr * CGFloat(sin(a)))
                }
            }
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
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(hover ? p.text : p.subtext)
                .frame(width: 24, height: 24)
                .background(Circle().fill(hover ? p.navHover : p.cardStrong))
                .overlay(Circle().stroke(p.stroke, lineWidth: 1))
                .contentShape(Circle())
        }
        .buttonStyle(.plain)
        .help(help)
        .animation(.easeOut(duration: 0.12), value: hover)
        .onHover { hover = $0 }
    }
}

struct SignatureText: View {
    var size: CGFloat = 30
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        Text("Anıl Gül")
            .font(.system(size: size * 0.8, weight: .semibold))
            .foregroundStyle(Palette(scheme).text)
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

/// Quiet icon tile used for tools and stats. `colors` only decides whether the tool is
/// destructive (red icon); everything else is monochrome.
struct GradientBadge: View {
    let symbol: String
    let colors: [UInt32]
    var size: CGFloat = 56
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        let shape = RoundedRectangle(cornerRadius: size * 0.26, style: .continuous)
        Image(systemName: symbol)
            .font(.system(size: size * 0.42, weight: .regular))
            .foregroundStyle(Palette.isDestructive(colors) ? Palette.danger : p.text)
            .frame(width: size, height: size)
            .background(shape.fill(p.cardStrong))
            .overlay(shape.stroke(p.stroke, lineWidth: 1))
    }
}

struct Chip: View {
    let text: String
    let color: Color

    var body: some View {
        Text(text)
            .font(.system(size: 10.5, weight: .semibold))
            .foregroundStyle(color)
            .padding(.horizontal, 7)
            .padding(.vertical, 2.5)
            .background(RoundedRectangle(cornerRadius: 6, style: .continuous).fill(color.opacity(0.13)))
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
            VStack(alignment: .leading, spacing: 6) {
                Text(title)
                    .font(.system(size: 26, weight: .semibold))
                    .tracking(-0.3)
                    .foregroundStyle(p.text)
                Text(subtitle)
                    .font(.system(size: 13.5))
                    .foregroundStyle(p.subtext)
                    .lineSpacing(2)
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
        .background(RoundedRectangle(cornerRadius: 8, style: .continuous).fill(p.card))
        .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(p.strokeStrong, lineWidth: 1))
    }
}

struct EmptyStateView: View {
    let symbol: String
    let text: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 14) {
            Image(systemName: symbol)
                .font(.system(size: 22, weight: .regular))
                .foregroundStyle(p.subtext)
                .frame(width: 56, height: 56)
                .background(Circle().fill(p.cardStrong))
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
                GradientBadge(symbol: symbol, colors: colors, size: 32)
                Text(value)
                    .font(.system(size: 24, weight: .semibold))
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
                Capsule().fill(p.track)
                if let value {
                    Capsule()
                        .fill(Palette.accent)
                        .frame(width: max(height, width * min(max(value, 0), 1)))
                        .animation(.easeOut(duration: 0.3), value: value)
                } else {
                    TimelineView(.animation) { context in
                        let t = context.date.timeIntervalSinceReferenceDate
                        let phase = CGFloat(t.truncatingRemainder(dividingBy: 1.4) / 1.4)
                        Capsule()
                            .fill(Palette.accent)
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

/// Quiet hover feedback for clickable cards: a slightly stronger outline and shadow, no scaling.
struct HoverLift: ViewModifier {
    @State private var hover = false
    @Environment(\.colorScheme) private var scheme

    func body(content: Content) -> some View {
        let p = Palette(scheme)
        content
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous)
                .stroke(p.strokeStrong, lineWidth: 1).opacity(hover ? 1 : 0).allowsHitTesting(false))
            .shadow(color: p.shadow.opacity(hover ? 1 : 0), radius: 8, y: 3)
            .animation(.easeOut(duration: 0.15), value: hover)
            .onHover { hover = $0 }
    }
}

extension View {
    func hoverLift() -> some View { modifier(HoverLift()) }
}

// MARK: - Live gauges

/// Circular gauge (0…1) in the accent colour; value changes ease briefly.
struct RingGauge: View {
    var value: Double
    var colors: [UInt32] = [0x4F6BED]
    var lineWidth: CGFloat = 8
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        ZStack {
            Circle().stroke(p.track, lineWidth: lineWidth)
            Circle()
                .trim(from: 0, to: max(0.002, min(1, value.isFinite ? value : 0)))
                .stroke(Palette.tone(colors), style: StrokeStyle(lineWidth: lineWidth, lineCap: .round))
                .rotationEffect(.degrees(-90))
                .animation(.easeOut(duration: 0.35), value: value)
        }
        .padding(lineWidth / 2)
    }
}

/// Compact ring gauge with the value in the middle and a caption below.
struct MiniGauge: View {
    let title: String
    let value: Double
    let text: String
    var colors: [UInt32] = [0x4F6BED]
    var size: CGFloat = 64
    var animate = true
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        let p = Palette(scheme)
        VStack(spacing: 6) {
            ZStack {
                RingGauge(value: value, colors: colors, lineWidth: size * 0.09)
                Text(text)
                    .font(.system(size: size * 0.2, weight: .semibold))
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

/// A one-off particle burst (about a second) for "done" moments, built from ordinary animated
/// shapes. Skipped when motion is reduced or the graphics hardware can't do effects.
struct Burst: View {
    var count = 28
    var spread: Double = 90
    @EnvironmentObject private var prefs: Prefs
    @State private var fired = false
    @State private var finished = false

    private static let colors: [Color] = [Color(hex: 0x4F6BED), Color(hex: 0x8EA2FF), Color(hex: 0x30A46C),
                                          Color(hex: 0xC9CCD6)]

    var body: some View {
        if prefs.calmMotion || finished || !GraphicsSupport.richEffects {
            Color.clear.allowsHitTesting(false)
        } else {
            ZStack {
                ForEach(0..<count, id: \.self) { i in
                    let angle = Double(i) / Double(count) * 2 * Double.pi + Double(i % 3) * 0.21
                    let distance = spread * (0.65 + Double((i * 37) % 40) / 100)
                    let size: CGFloat = i % 3 == 0 ? 8 : 5.6
                    Circle()
                        .fill(Self.colors[i % Self.colors.count])
                        .frame(width: size, height: size)
                        .offset(x: fired ? CGFloat(cos(angle) * distance) : 0,
                                y: fired ? CGFloat(sin(angle) * distance) + 14 : 0)
                        .scaleEffect(fired ? 0.5 : 1)
                        .opacity(fired ? 0 : 1)
                }
            }
            .allowsHitTesting(false)
            .onAppear {
                withAnimation(.easeOut(duration: 1.0)) { fired = true }
            }
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
    var color: Color = Palette.accent

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
                    .fill(LinearGradient(colors: [color.opacity(0.18), color.opacity(0)], startPoint: .top, endPoint: .bottom))
                    Path { path in path.addLines(points) }
                        .stroke(color, style: StrokeStyle(lineWidth: 1.5, lineJoin: .round))
                }
            }
        }
    }
}

/// "Live" indicator: a steady dot with a soft halo. It does not pulse, so an idle window
/// never has to redraw.
struct LiveDot: View {
    var color: Color = Palette.success

    var body: some View {
        ZStack {
            Circle().fill(color.opacity(0.22)).frame(width: 12, height: 12)
            Circle().fill(color).frame(width: 6, height: 6)
        }
        .frame(width: 14, height: 14)
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
            .offset(y: shown ? 0 : 8)
            .onAppear {
                if prefs.calmMotion {
                    shown = true
                } else {
                    withAnimation(.easeOut(duration: 0.28).delay(delay * 0.6)) { shown = true }
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
                    .font(.system(size: 17, weight: .semibold))
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
                .background(RoundedRectangle(cornerRadius: 8, style: .continuous).fill(p.cardStrong))
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
        .padding(28)
        .frame(width: 500)
        .background(p.dark ? Color(hex: 0x1E1E21) : Color.white)
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
                    Image(systemName: "checkmark").font(.system(size: 9, weight: .semibold)).foregroundStyle(.white)
                } else if state == nil {
                    Image(systemName: "minus").font(.system(size: 9, weight: .semibold)).foregroundStyle(.white)
                }
            }
            .frame(width: 15, height: 15)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}
