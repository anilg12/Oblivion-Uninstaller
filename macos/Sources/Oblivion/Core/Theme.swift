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

/// Oblivion's Daccord-inspired palette (navy / violet), with a light variant.
struct Palette {
    let dark: Bool

    init(_ scheme: ColorScheme) { dark = scheme == .dark }

    var tintTop: Color { dark ? Color(hex: 0x2C2F66) : Color(hex: 0xEEECFF) }
    var tintBottom: Color { dark ? Color(hex: 0x191B36) : Color(hex: 0xDDD9F8) }
    var rail: Color { dark ? Color(hex: 0x1C1F3D, opacity: 0.94) : Color(hex: 0xE2DFFA, opacity: 0.94) }
    var panel: Color { dark ? Color(hex: 0x2B3164, opacity: 0.86) : Color.white.opacity(0.72) }
    var content: Color { dark ? Color(hex: 0x22244B, opacity: 0.82) : Color.white.opacity(0.62) }
    var right: Color { dark ? Color(hex: 0x2C2F48, opacity: 0.9) : Color(hex: 0xF7F6FF, opacity: 0.88) }
    var navSelected: Color { dark ? Color(hex: 0x13152B) : Color(hex: 0x2B2F58) }
    var card: Color { dark ? Color.white.opacity(0.055) : Color.black.opacity(0.035) }
    var cardStrong: Color { dark ? Color.white.opacity(0.10) : Color.black.opacity(0.06) }
    var stroke: Color { dark ? Color.white.opacity(0.08) : Color.black.opacity(0.08) }
    var text: Color { dark ? Color.white : Color(hex: 0x1B1D3A) }
    var subtext: Color { dark ? Color.white.opacity(0.62) : Color(hex: 0x1B1D3A, opacity: 0.62) }
    var faint: Color { dark ? Color.white.opacity(0.38) : Color(hex: 0x1B1D3A, opacity: 0.4) }

    static let accent = Color(hex: 0x7C6CF6)
    static let accent2 = Color(hex: 0xB45BFF)
    static let danger = Color(hex: 0xE5484D)
    static let success = Color(hex: 0x22C55E)
    static let warning = Color(hex: 0xF5A524)
    static let info = Color(hex: 0x3A8DFF)

    static var brandGradient: LinearGradient {
        LinearGradient(colors: [Color(hex: 0x6E5BFF), Color(hex: 0xB45BFF)],
                       startPoint: .topLeading, endPoint: .bottomTrailing)
    }

    static func signature(_ dark: Bool) -> LinearGradient {
        let colors = dark
            ? [Color(hex: 0x8FC0FF), Color(hex: 0xD7ABFF)]
            : [Color(hex: 0x4F6BFF), Color(hex: 0x9B4BE0)]
        return LinearGradient(colors: colors, startPoint: .leading, endPoint: .trailing)
    }

    static func gradient(_ hexes: [UInt32]) -> LinearGradient {
        LinearGradient(colors: hexes.map { Color(hex: $0) }, startPoint: .topLeading, endPoint: .bottomTrailing)
    }
}
