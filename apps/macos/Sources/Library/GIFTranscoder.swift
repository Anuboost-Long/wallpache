import AVFoundation
import CoreGraphics
import CoreVideo
import Foundation
import ImageIO
import UniformTypeIdentifiers
import os

/// Converts an animated GIF into an H.264 movie at import time.
///
/// Nothing downstream understands GIF: playback, thumbnails, stills, and the
/// desktop picture all go through AVFoundation. Converting once on the way in
/// leaves every one of those paths untouched, and the library ends up holding a
/// file that behaves like any other wallpaper.
nonisolated enum GIFTranscoder {
    /// What the movie will contain, known before a single frame is encoded.
    struct Summary: Sendable {
        let frameCount: Int
        let duration: Double
        let pixelSize: CGSize
    }

    /// Countless GIFs declare a delay of 0 or 1 hundredths. Browsers show those
    /// at 100ms, so matching that is what makes a converted GIF run at the speed
    /// the user is used to seeing.
    private static let shortestHonouredDelay = 0.02
    private static let substituteDelay = 0.1

    static func isGIF(_ url: URL) -> Bool {
        url.pathExtension.lowercased() == "gif"
    }

    /// Reads frame timing and size without decoding any pixels.
    static func inspect(url: URL) throws -> Summary {
        let name = url.lastPathComponent

        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
              CGImageSourceGetType(source) == UTType.gif.identifier as CFString else {
            throw WallpaperError.notPlayable(name: name)
        }

        let frameCount = CGImageSourceGetCount(source)
        guard frameCount > 0, let size = pixelSize(of: source) else {
            throw WallpaperError.noVideoTrack(name: name)
        }

        let duration = (0..<frameCount).reduce(0.0) { total, index in
            total + delay(of: source, at: index)
        }
        guard duration > 0 else {
            throw WallpaperError.emptyDuration(name: name)
        }

        return Summary(frameCount: frameCount, duration: duration, pixelSize: size)
    }

    /// One frame for the import tray, decoded straight from the GIF so nothing
    /// has to be converted before the user has confirmed the import.
    static func previewFrame(url: URL, maximumPixelWidth: CGFloat) -> CGImage? {
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil) else { return nil }

        let options: [CFString: Any] = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceThumbnailMaxPixelSize: Int(maximumPixelWidth)
        ]
        return CGImageSourceCreateThumbnailAtIndex(source, 0, options as CFDictionary)
    }

    /// Writes `sourceURL` to `destination` as H.264 and reports what it wrote.
    ///
    /// The source is validated before the writer is created, so a GIF that
    /// cannot be read leaves nothing behind. A failure part way through removes
    /// the half-written movie: an unplayable file must never enter the library.
    @discardableResult
    static func transcode(gifAt sourceURL: URL, to destination: URL) async throws -> VideoMetadata {
        let name = sourceURL.lastPathComponent
        let summary = try inspect(url: sourceURL)

        guard let source = CGImageSourceCreateWithURL(sourceURL as CFURL, nil) else {
            throw WallpaperError.notPlayable(name: name)
        }

        // H.264 encodes in 16x16 macroblocks over a 4:2:0 grid, so odd
        // dimensions are rejected outright by the writer.
        let width = evenDimension(summary.pixelSize.width)
        let height = evenDimension(summary.pixelSize.height)

        let written: CMTime
        do {
            written = try await write(
                source: source,
                frameCount: summary.frameCount,
                width: width,
                height: height,
                to: destination
            )
        } catch {
            try? FileManager.default.removeItem(at: destination)
            throw error is WallpaperError
                ? error
                : WallpaperError.importFailed(name: name, reason: error.localizedDescription)
        }

        Log.library.info("Converted a GIF of \(summary.frameCount, privacy: .public) frames")
        // What was written, rather than what the GIF claimed: a frame the
        // decoder could not produce is skipped, and the record has to match the
        // movie the rest of the app is about to play.
        return VideoMetadata(
            duration: written.seconds,
            pixelSize: CGSize(width: width, height: height)
        )
    }

    // MARK: - Private

    /// Returns the point the movie ends at, which is what the record records.
    private static func write(
        source: CGImageSource,
        frameCount: Int,
        width: Int,
        height: Int,
        to destination: URL
    ) async throws -> CMTime {
        let writer = try AVAssetWriter(outputURL: destination, fileType: .mov)
        let input = AVAssetWriterInput(
            mediaType: .video,
            outputSettings: [
                AVVideoCodecKey: AVVideoCodecType.h264,
                AVVideoWidthKey: width,
                AVVideoHeightKey: height
            ]
        )
        input.expectsMediaDataInRealTime = false

        let adaptor = AVAssetWriterInputPixelBufferAdaptor(
            assetWriterInput: input,
            sourcePixelBufferAttributes: [
                kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB,
                kCVPixelBufferWidthKey as String: width,
                kCVPixelBufferHeightKey as String: height
            ]
        )

        writer.add(input)
        guard writer.startWriting() else {
            throw writer.error ?? CocoaError(.fileWriteUnknown)
        }
        writer.startSession(atSourceTime: .zero)

        var presentationTime = CMTime.zero

        for index in 0..<frameCount {
            guard let image = CGImageSourceCreateImageAtIndex(source, index, nil),
                  let pool = adaptor.pixelBufferPool,
                  let buffer = makePixelBuffer(pool: pool, image: image, width: width, height: height) else {
                continue
            }

            while !input.isReadyForMoreMediaData {
                try await Task.sleep(nanoseconds: 1_000_000)
            }

            adaptor.append(buffer, withPresentationTime: presentationTime)
            presentationTime = CMTimeAdd(
                presentationTime,
                CMTime(seconds: delay(of: source, at: index), preferredTimescale: 600)
            )
        }

        input.markAsFinished()
        // Without this the movie would end on the last frame's timestamp, so
        // that frame would be held for no time at all when the loop comes round.
        writer.endSession(atSourceTime: presentationTime)
        await writer.finishWriting()

        guard writer.status == .completed else {
            throw writer.error ?? CocoaError(.fileWriteUnknown)
        }

        return presentationTime
    }

    /// Draws one frame onto black. GIF transparency has nowhere to go in H.264,
    /// and black is what an unfilled corner of a wallpaper looks like anyway.
    private static func makePixelBuffer(
        pool: CVPixelBufferPool,
        image: CGImage,
        width: Int,
        height: Int
    ) -> CVPixelBuffer? {
        var buffer: CVPixelBuffer?
        guard CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, pool, &buffer) == kCVReturnSuccess,
              let buffer else { return nil }

        CVPixelBufferLockBaseAddress(buffer, [])
        defer { CVPixelBufferUnlockBaseAddress(buffer, []) }

        guard let context = CGContext(
            data: CVPixelBufferGetBaseAddress(buffer),
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: CVPixelBufferGetBytesPerRow(buffer),
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue | CGBitmapInfo.byteOrder32Big.rawValue
        ) else { return nil }

        context.setFillColor(CGColor(red: 0, green: 0, blue: 0, alpha: 1))
        context.fill(CGRect(x: 0, y: 0, width: width, height: height))
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        return buffer
    }

    private static func pixelSize(of source: CGImageSource) -> CGSize? {
        guard let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = properties[kCGImagePropertyPixelWidth] as? Int,
              let height = properties[kCGImagePropertyPixelHeight] as? Int,
              width > 0, height > 0 else { return nil }

        return CGSize(width: width, height: height)
    }

    private static func delay(of source: CGImageSource, at index: Int) -> Double {
        guard let properties = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any],
              let gif = properties[kCGImagePropertyGIFDictionary] as? [CFString: Any] else {
            return substituteDelay
        }

        let declared = gif[kCGImagePropertyGIFUnclampedDelayTime] as? Double
            ?? gif[kCGImagePropertyGIFDelayTime] as? Double
            ?? 0

        return declared < shortestHonouredDelay ? substituteDelay : declared
    }

    private static func evenDimension(_ value: CGFloat) -> Int {
        max(Int(value) / 2 * 2, 2)
    }
}
