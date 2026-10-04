import SwiftUI

extension Color {
    /// Builds a color from a 0xRRGGBB literal.
    init(hex: UInt32, opacity: Double = 1) {
        self.init(
            .sRGB,
            red: Double((hex >> 16) & 0xFF) / 255,
            green: Double((hex >> 8) & 0xFF) / 255,
            blue: Double(hex & 0xFF) / 255,
            opacity: opacity
        )
    }
}

/// Oblivion "Graphite": neutral warm greys, hairline borders and a single indigo accent.
/// The same values are used by the Windows app.
struct Palette {
    let dark: Bool

    init(_ scheme: ColorScheme) { dark = scheme == .dark }

    /// Window / content canvas.
    var tintTop: Color { dark ? Color(hex: 0x141416) : Color(hex: 0xF7F7F5) }
    var tintBottom: Color { tintTop }
    /// Sidebar surface (drawn over a light vibrancy layer).
    var rail: Color { dark ? Color(hex: 0x19191C, opacity: 0.86) : Color(hex: 0xF0F0ED, opacity: 0.86) }
    var panel: Color { rail }
    var content: Color { tintTop }
    var right: Color { dark ? Color(hex: 0x19191C) : Color(hex: 0xF0F0ED) }
    var navSelected: Color { dark ? Color(hex: 0x27272C) : Color.white }
    var navHover: Color { dark ? Color(hex: 0x202024) : Color(hex: 0xE7E7E3) }
    var card: Color { dark ? Color(hex: 0x1C1C1F) : Color.white }
    var cardHover: Color { dark ? Color(hex: 0x222226) : Color(hex: 0xFBFBF9) }
    var cardStrong: Color { dark ? Color(hex: 0x26262A) : Color(hex: 0xEFEFEC) }
    var stroke: Color { dark ? Color(hex: 0x2A2A2F) : Color(hex: 0xE4E3DF) }
    var strokeStrong: Color { dark ? Color(hex: 0x3A3A41) : Color(hex: 0xD2D1CC) }
    var track: Color { dark ? Color(hex: 0x2C2C31) : Color(hex: 0xE8E7E3) }
    var text: Color { dark ? Color(hex: 0xEDEDEF) : Color(hex: 0x18181B) }
    var subtext: Color { dark ? Color(hex: 0xA3A3AA) : Color(hex: 0x5E5E66) }
    var faint: Color { dark ? Color(hex: 0x6E6E76) : Color(hex: 0x93939B) }
    /// Accent for text and small icons, tuned for contrast on this background.
    var accentText: Color { dark ? Color(hex: 0xA3B4FF) : Color(hex: 0x3A52C9) }
    var accentSoft: Color { Palette.accent.opacity(dark ? 0.17 : 0.10) }
    var shadow: Color { dark ? Color.black.opacity(0.35) : Color(hex: 0x18181B, opacity: 0.08) }

    static let accent = Color(hex: 0x4F6BED)
    static let accent2 = accent
    static let danger = Color(hex: 0xE5484D)
    static let success = Color(hex: 0x30A46C)
    static let warning = Color(hex: 0xF2A33A)
    static let info = accent

    /// Kept as a gradient type for existing call sites; it is a flat accent now.
    static var brandGradient: LinearGradient {
        LinearGradient(colors: [accent, accent], startPoint: .top, endPoint: .bottom)
    }

    static func signature(_ dark: Bool) -> LinearGradient {
        let c = dark ? Color(hex: 0xEDEDEF) : Color(hex: 0x18181B)
        return LinearGradient(colors: [c, c], startPoint: .leading, endPoint: .trailing)
    }

    /// Legacy per-feature colour pairs collapse to one tone: red stays red (destructive),
    /// everything else becomes the accent.
    static func tone(_ hexes: [UInt32]) -> Color {
        isDestructive(hexes) ? danger : accent
    }

    static func isDestructive(_ hexes: [UInt32]) -> Bool {
        guard let h = hexes.first else { return false }
        let r = (h >> 16) & 0xFF, g = (h >> 8) & 0xFF, b = h & 0xFF
        return r > 0xC8 && g < 0x80 && b < 0x80
    }

    static func gradient(_ hexes: [UInt32]) -> LinearGradient {
        let c = tone(hexes)
        return LinearGradient(colors: [c, c], startPoint: .topLeading, endPoint: .bottomTrailing)
    }
}
