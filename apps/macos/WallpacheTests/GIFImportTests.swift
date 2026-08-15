import CoreGraphics
import Foundation
import Testing

@testable import Wallpache

/// GIFs are converted to H.264 on the way in, so what these check is that the
/// library ends up holding a playable movie that matches the source.
struct GIFImportTests {
    private func makeStorage() throws -> WallpaperStorage {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("WallpacheTests-\(UUID().uuidString)", isDirectory: true)
        let storage = WallpaperStorage(root: root)
        try storage.prepareDirectories()
        return storage
    }

    private func makeSourceGIF(size: Int = SampleGIF.size) throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("Sample-\(UUID().uuidString).gif")
        try SampleGIF.write(to: url, size: size)
        return url
    }

    @Test func inspectReportsTheFramesAndRunTime() throws {
        let source = try makeSourceGIF()
        defer { try? FileManager.default.removeItem(at: source) }

        let summary = try GIFTranscoder.inspect(url: source)

        #expect(summary.frameCount == SampleGIF.frameCount)
        #expect(abs(summary.duration - SampleGIF.duration) < 0.05)
        #expect(summary.pixelSize == CGSize(width: SampleGIF.size, height: SampleGIF.size))
    }

    @Test func aFileThatIsNotAGIFIsRejectedBeforeAnythingIsWritten() async throws {
        let source = FileManager.default.temporaryDirectory
            .appendingPathComponent("Fake-\(UUID().uuidString).gif")
        try Data("not a gif".utf8).write(to: source)
        defer { try? FileManager.default.removeItem(at: source) }

        let destination = FileManager.default.temporaryDirectory
            .appendingPathComponent("Out-\(UUID().uuidString).mov")

        await #expect(throws: WallpaperError.self) {
            try await GIFTranscoder.transcode(gifAt: source, to: destination)
        }
        #expect(!FileManager.default.fileExists(atPath: destination.path))
    }

    @Test func transcodingProducesAPlayableMovie() async throws {
        let source = try makeSourceGIF()
        let destination = FileManager.default.temporaryDirectory
            .appendingPathComponent("Out-\(UUID().uuidString).mov")
        defer {
            try? FileManager.default.removeItem(at: source)
            try? FileManager.default.removeItem(at: destination)
        }

        let reported = try await GIFTranscoder.transcode(gifAt: source, to: destination)
        // Read back through the same validator the import path uses for videos:
        // whatever was written has to pass as an ordinary wallpaper.
        let metadata = try await VideoMetadataReader.read(url: destination)

        #expect(abs(metadata.duration - SampleGIF.duration) < 0.05)
        #expect(metadata.pixelSize == CGSize(width: SampleGIF.size, height: SampleGIF.size))
        #expect(reported.pixelSize == metadata.pixelSize)
    }

    /// H.264 cannot encode odd dimensions, so the transcoder trims to even ones.
    @Test func oddSidedSourcesAreEncodedAtEvenDimensions() async throws {
        let source = try makeSourceGIF(size: 65)
        let destination = FileManager.default.temporaryDirectory
            .appendingPathComponent("Out-\(UUID().uuidString).mov")
        defer {
            try? FileManager.default.removeItem(at: source)
            try? FileManager.default.removeItem(at: destination)
        }

        try await GIFTranscoder.transcode(gifAt: source, to: destination)
        let metadata = try await VideoMetadataReader.read(url: destination)

        #expect(metadata.pixelSize == CGSize(width: 64, height: 64))
    }

    @Test func importStoresAConvertedMovieWithItsPreviews() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try makeSourceGIF()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])

        #expect(record.relativePath == "Wallpapers/\(record.id.uuidString).mov")
        #expect(service.fileExists(for: record))
        #expect(abs(record.duration - SampleGIF.duration) < 0.05)
        #expect(record.pixelSize == CGSize(width: SampleGIF.size, height: SampleGIF.size))
        #expect(record.thumbnailRelativePath != nil)
        #expect(record.stillRelativePath != nil)
        // The name keeps the source's, and the original is left untouched.
        #expect(record.sourceFileName == source.lastPathComponent)
        #expect(FileManager.default.fileExists(atPath: source.path))
    }

    @Test func importingTheSameGIFTwiceReusesTheExistingRecord() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try makeSourceGIF()
        defer { try? FileManager.default.removeItem(at: source) }

        let first = try await service.importVideo(at: source, existing: [])
        let second = try await service.importVideo(at: source, existing: [first])

        #expect(first.id == second.id)
        #expect(try FileManager.default.contentsOfDirectory(atPath: storage.videosDirectory.path).count == 1)
    }

    /// The tray is main-actor state, unlike the transcoder above it.
    @MainActor
    @Test func aStagedGIFIsShownWithItsRunTimeAndAPreview() async throws {
        let source = try makeSourceGIF()
        defer { try? FileManager.default.removeItem(at: source) }

        let queue = ImportQueue { _ in }
        queue.stage([source])

        for _ in 0..<300 where queue.items.first?.state == .inspecting {
            try await Task.sleep(nanoseconds: 20_000_000)
        }

        let item = try #require(queue.items.first)
        #expect(item.state == .ready)
        #expect(item.preview != nil)
        #expect(item.detail == "0:01 · 64 × 64")
    }
}
