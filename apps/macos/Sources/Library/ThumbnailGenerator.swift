import AVFoundation
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers
import os

/// Renders still frames from an imported video.
///
/// Two sizes are produced per import, because one file cannot serve both jobs:
///
/// - A **still** at the video's own resolution, for the preview sheet and for
///   use as the macOS desktop picture. Anything smaller is visibly soft the
///   moment it is scaled up to a display.
/// - A **thumbnail** at grid size, so a library of 4K wallpapers does not load
///   tens of megabytes of pixels just to draw a row of cells.
///
/// Both are generated once at import time; nothing regenerates them while a
/// wallpaper is playing.
nonisolated enum ThumbnailGenerator {
    /// Grid size. Ample for the largest library cell on a 2x display.
    static let thumbnailPixelWidth: CGFloat = 640
    /// Full size, bounded so an 8K source cannot produce an enormous PNG.
    static let stillPixelWidth: CGFloat = 3840

    /// Writes a PNG preview and returns `true` on success. A missing image is
    /// cosmetic, so failures are logged rather than thrown.
    @discardableResult
    static func generate(
        from videoURL: URL,
        to destination: URL,
        duration: Double,
        maximumPixelWidth: CGFloat = thumbnailPixelWidth
    ) async -> Bool {
        let asset = AVURLAsset(url: videoURL)
        let generator = AVAssetImageGenerator(asset: asset)
        generator.appliesPreferredTrackTransform = true
        generator.maximumSize = CGSize(width: maximumPixelWidth, height: maximumPixelWidth)
        generator.requestedTimeToleranceBefore = CMTime(seconds: 1, preferredTimescale: 600)
        generator.requestedTimeToleranceAfter = CMTime(seconds: 1, preferredTimescale: 600)

        // Prefer a frame just inside the video; the very first frame is often
        // black on fade-in clips.
        let seconds = min(max(duration * 0.1, 0.5), max(duration - 0.1, 0))
        let time = CMTime(seconds: seconds, preferredTimescale: 600)

        do {
            let (image, _) = try await generator.image(at: time)
            return write(image, to: destination)
        } catch {
            Log.library.error("Thumbnail generation failed for \(videoURL.lastPathComponent, privacy: .public): \(error.localizedDescription, privacy: .public)")
            return false
        }
    }

    private static func write(_ image: CGImage, to destination: URL) -> Bool {
        guard let output = CGImageDestinationCreateWithURL(
            destination as CFURL,
            UTType.png.identifier as CFString,
            1,
            nil
        ) else { return false }

        CGImageDestinationAddImage(output, image, nil)
        return CGImageDestinationFinalize(output)
    }
}
