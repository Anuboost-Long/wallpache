import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Writes a tiny real animated GIF so the transcoder and the GIF import path can
/// be exercised without bundling a fixture.
enum SampleGIF {
    static let size = 64
    static let frameCount = 6
    static let frameDelay = 0.1

    static var duration: Double { Double(frameCount) * frameDelay }

    /// `size` is overridable so a test can produce an odd-sided source, which
    /// H.264 cannot encode as-is.
    static func write(
        to url: URL,
        size: Int = size,
        frameCount: Int = frameCount,
        frameDelay: Double = frameDelay
    ) throws {
        guard let destination = CGImageDestinationCreateWithURL(
            url as CFURL,
            UTType.gif.identifier as CFString,
            frameCount,
            nil
        ) else {
            throw CocoaError(.fileWriteUnknown)
        }

        CGImageDestinationSetProperties(destination, [
            kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: 0]
        ] as CFDictionary)

        for frame in 0..<frameCount {
            guard let image = makeImage(size: size, brightness: Double(frame) / Double(frameCount)) else {
                throw CocoaError(.fileWriteUnknown)
            }

            CGImageDestinationAddImage(destination, image, [
                kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFDelayTime: frameDelay]
            ] as CFDictionary)
        }

        guard CGImageDestinationFinalize(destination) else {
            throw CocoaError(.fileWriteUnknown)
        }
    }

    private static func makeImage(size: Int, brightness: Double) -> CGImage? {
        guard let context = CGContext(
            data: nil,
            width: size,
            height: size,
            bitsPerComponent: 8,
            bytesPerRow: 0,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue
        ) else { return nil }

        context.setFillColor(CGColor(red: brightness, green: brightness, blue: brightness, alpha: 1))
        context.fill(CGRect(x: 0, y: 0, width: size, height: size))
        return context.makeImage()
    }
}
