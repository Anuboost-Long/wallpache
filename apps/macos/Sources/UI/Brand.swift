import SwiftUI

/// The icon pack's palette, so the window chrome matches the app icon.
/// Values are the ones documented in the pack's README.
enum Brand {
    static let tagline = "Bring your desktop to life."

    /// Icon backdrop, top to bottom.
    static let backdropTop = Color(hex: 0x191826)
    static let backdropBottom = Color(hex: 0x0C0B13)

    /// The lavender window frame drawn inside the icon.
    static let frameLight = Color(hex: 0xC9BEF5)
    static let frameDark = Color(hex: 0x8F7FD0)

    /// The wave sweeping across the icon.
    static let waveOrange = Color(hex: 0xFF7A2F)
    static let wavePink = Color(hex: 0xFF3F7D)
    static let wavePurple = Color(hex: 0x8A4CFF)

    /// The wave gradient, used as an accent rule and for highlighted text.
    static let wave = LinearGradient(
        colors: [waveOrange, wavePink, wavePurple],
        startPoint: .leading,
        endPoint: .trailing
    )

    /// A soft version of the icon backdrop for header bars. It stays subtle in
    /// light mode instead of punching a dark slab into the window.
    static let headerBackground = LinearGradient(
        colors: [frameLight.opacity(0.22), wavePurple.opacity(0.12)],
        startPoint: .topLeading,
        endPoint: .bottomTrailing
    )
}

extension Color {
    /// Builds a colour from a 24-bit `0xRRGGBB` literal.
    init(hex: UInt32) {
        self.init(
            .sRGB,
            red: Double((hex >> 16) & 0xFF) / 255,
            green: Double((hex >> 8) & 0xFF) / 255,
            blue: Double(hex & 0xFF) / 255
        )
    }
}
