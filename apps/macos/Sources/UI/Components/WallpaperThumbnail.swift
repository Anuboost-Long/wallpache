import ImageIO
import SwiftUI

/// Thumbnail image loaded off the main thread, with a placeholder while it
/// loads or when the video has no preview.
///
/// Decoding is capped at `maximumPixelSize` so that pointing this at a
/// full-resolution still costs the view what it draws rather than the whole
/// 4K frame.
struct WallpaperThumbnail: View {
    let url: URL?
    var cornerRadius: CGFloat = 8
    /// Longest edge to decode, in pixels. The default suits a library cell.
    var maximumPixelSize: Int = 640

    @State private var image: NSImage?

    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .fill(Color(nsColor: .quaternaryLabelColor))

            if let image {
                Image(nsImage: image)
                    .resizable()
                    .aspectRatio(contentMode: .fill)
            } else {
                Image(systemName: "film")
                    .font(.title2)
                    .foregroundStyle(.secondary)
            }
        }
        .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
        .task(id: url) {
            image = await Self.loadImage(at: url, maximumPixelSize: maximumPixelSize)
        }
    }

    private static func loadImage(at url: URL?, maximumPixelSize: Int) async -> NSImage? {
        guard let url else { return nil }

        let decoded = await Task.detached(priority: .utility) { () -> CGImage? in
            guard let source = CGImageSourceCreateWithURL(url as CFURL, nil) else { return nil }
            let options: [CFString: Any] = [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceShouldCacheImmediately: true,
                kCGImageSourceThumbnailMaxPixelSize: maximumPixelSize
            ]
            return CGImageSourceCreateThumbnailAtIndex(source, 0, options as CFDictionary)
        }.value

        guard let decoded else { return nil }
        return NSImage(cgImage: decoded, size: .zero)
    }
}
