import AVFoundation
import Foundation

nonisolated struct VideoMetadata: Sendable {
    let duration: Double
    /// Display size after the preferred track transform, in pixels.
    let pixelSize: CGSize?
}

/// Validates a candidate video and extracts the metadata the library needs.
///
/// Validation is deliberately strict: a file that reaches a `WallpaperSession`
/// must be playable, so the failure is reported at import time rather than as a
/// black desktop later.
nonisolated enum VideoMetadataReader {
    static func read(url: URL, fileManager: FileManager = .default) async throws -> VideoMetadata {
        let name = url.lastPathComponent

        guard fileManager.fileExists(atPath: url.path) else {
            throw WallpaperError.fileMissing(name: name)
        }

        let asset = AVURLAsset(
            url: url,
            options: [AVURLAssetPreferPreciseDurationAndTimingKey: true]
        )

        let isPlayable: Bool
        let hasProtectedContent: Bool
        do {
            (isPlayable, hasProtectedContent) = try await asset.load(.isPlayable, .hasProtectedContent)
        } catch {
            throw WallpaperError.notPlayable(name: name)
        }

        guard !hasProtectedContent else { throw WallpaperError.protectedContent(name: name) }
        guard isPlayable else { throw WallpaperError.notPlayable(name: name) }

        let videoTracks = (try? await asset.loadTracks(withMediaType: .video)) ?? []
        guard let track = videoTracks.first else {
            throw WallpaperError.noVideoTrack(name: name)
        }

        let duration = try await asset.load(.duration).seconds
        guard duration.isFinite, duration > 0 else {
            throw WallpaperError.emptyDuration(name: name)
        }

        let pixelSize = try? await Self.displaySize(of: track)
        return VideoMetadata(duration: duration, pixelSize: pixelSize)
    }

    /// Natural size corrected for rotation metadata, so portrait videos report
    /// portrait dimensions.
    private static func displaySize(of track: AVAssetTrack) async throws -> CGSize {
        let (naturalSize, transform) = try await track.load(.naturalSize, .preferredTransform)
        let transformed = naturalSize.applying(transform)
        return CGSize(width: abs(transformed.width), height: abs(transformed.height))
    }
}
