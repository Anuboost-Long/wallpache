import AVFoundation
import CoreVideo
import Foundation

/// Writes a tiny real H.264 movie so import, validation, and thumbnail code can
/// be exercised without bundling a fixture.
enum SampleVideo {
    static let size = 64
    static let frameCount = 10
    static let frameRate: Int32 = 10

    /// `size` is overridable so a test can produce a source large enough for the
    /// still and the thumbnail to come out at different resolutions.
    static func write(to url: URL, size: Int = size) async throws {
        let writer = try AVAssetWriter(outputURL: url, fileType: .mov)
        let input = AVAssetWriterInput(
            mediaType: .video,
            outputSettings: [
                AVVideoCodecKey: AVVideoCodecType.h264,
                AVVideoWidthKey: size,
                AVVideoHeightKey: size
            ]
        )
        input.expectsMediaDataInRealTime = false

        let adaptor = AVAssetWriterInputPixelBufferAdaptor(
            assetWriterInput: input,
            sourcePixelBufferAttributes: [
                kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB,
                kCVPixelBufferWidthKey as String: size,
                kCVPixelBufferHeightKey as String: size
            ]
        )

        writer.add(input)
        writer.startWriting()
        writer.startSession(atSourceTime: .zero)

        for frame in 0..<frameCount {
            guard let pool = adaptor.pixelBufferPool,
                  let buffer = makePixelBuffer(pool: pool, brightness: UInt8(frame * 20)) else {
                throw CocoaError(.fileWriteUnknown)
            }
            while !input.isReadyForMoreMediaData {
                try await Task.sleep(nanoseconds: 1_000_000)
            }
            adaptor.append(buffer, withPresentationTime: CMTime(value: Int64(frame), timescale: frameRate))
        }

        input.markAsFinished()
        await writer.finishWriting()
    }

    private static func makePixelBuffer(pool: CVPixelBufferPool, brightness: UInt8) -> CVPixelBuffer? {
        var buffer: CVPixelBuffer?
        guard CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, pool, &buffer) == kCVReturnSuccess,
              let buffer else { return nil }

        CVPixelBufferLockBaseAddress(buffer, [])
        defer { CVPixelBufferUnlockBaseAddress(buffer, []) }

        if let base = CVPixelBufferGetBaseAddress(buffer) {
            let length = CVPixelBufferGetBytesPerRow(buffer) * CVPixelBufferGetHeight(buffer)
            memset(base, Int32(brightness), length)
        }
        return buffer
    }
}
