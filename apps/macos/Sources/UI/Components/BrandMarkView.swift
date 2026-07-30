import SwiftUI

/// The Wallpache icon mark from the icon pack, drawn at a given point size.
struct BrandMarkView: View {
    var size: CGFloat = 32

    var body: some View {
        Image("BrandMark")
            .resizable()
            .interpolation(.high)
            .aspectRatio(contentMode: .fit)
            .frame(width: size, height: size)
            .accessibilityHidden(true)
    }
}

/// Icon, name, and tagline, with the icon's wave gradient as the rule beneath.
/// Used at the top of the main window so it reads as Wallpache rather than a
/// generic tab bar.
struct BrandHeader: View {
    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 12) {
                BrandMarkView(size: 40)

                VStack(alignment: .leading, spacing: 1) {
                    Text("Wallpache")
                        .font(.title3.weight(.semibold))
                    Text(Brand.tagline)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Spacer()
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
            .background(Brand.headerBackground)

            Rectangle()
                .fill(Brand.wave)
                .frame(height: 2)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Wallpache. \(Brand.tagline)")
    }
}
