import CoreGraphics
import Foundation
import ImageIO
import Testing

@testable import Wallpache

/// Exercises the import pipeline against a temporary storage root and a real
/// (tiny) movie file.
struct WallpaperLibraryServiceTests {
    private func makeStorage() throws -> WallpaperStorage {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("WallpacheTests-\(UUID().uuidString)", isDirectory: true)
        let storage = WallpaperStorage(root: root)
        try storage.prepareDirectories()
        return storage
    }

    private func pixelWidth(of url: URL) -> Int? {
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any]
        else { return nil }
        return properties[kCGImagePropertyPixelWidth] as? Int
    }

    private func makeSourceVideo(
        named name: String = "Sample",
        size: Int = SampleVideo.size
    ) async throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("\(name)-\(UUID().uuidString).mov")
        try await SampleVideo.write(to: url, size: size)
        return url
    }

    @Test func importCopiesTheFileIntoStorageAndReadsMetadata() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])

        #expect(service.fileExists(for: record))
        #expect(record.duration > 0)
        #expect(record.pixelSize == CGSize(width: SampleVideo.size, height: SampleVideo.size))
        #expect(record.fileSize > 0)
        // The copy is named after the record, not the source, so two imports of
        // identically named files cannot collide.
        #expect(record.relativePath == "Wallpapers/\(record.id.uuidString).mov")
        // The original is left untouched.
        #expect(FileManager.default.fileExists(atPath: source.path))
    }

    @Test func importGeneratesAThumbnail() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])
        let thumbnail = try #require(storage.thumbnailURL(for: record))

        #expect(FileManager.default.fileExists(atPath: thumbnail.path))
    }

    @Test func importingTheSameFileTwiceReusesTheExistingRecord() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let first = try await service.importVideo(at: source, existing: [])
        let second = try await service.importVideo(at: source, existing: [first])

        #expect(first.id == second.id)
        let stored = try FileManager.default.contentsOfDirectory(atPath: storage.videosDirectory.path)
        #expect(stored.count == 1)
    }

    /// Duplicate detection must key off the source file name, not the display
    /// name, or renaming an entry would let the same video be copied in twice.
    @Test func renamingARecordDoesNotDefeatDuplicateDetection() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        var imported = try await service.importVideo(at: source, existing: [])
        imported.name = "Something Else Entirely"

        let reimported = try await service.importVideo(at: source, existing: [imported])

        #expect(reimported.id == imported.id)
        let stored = try FileManager.default.contentsOfDirectory(atPath: storage.videosDirectory.path)
        #expect(stored.count == 1)
    }

    /// Records written before renaming existed carry no source file name and
    /// must still be recognised through the old display-name comparison.
    @Test func legacyRecordsWithoutASourceNameStillDeduplicate() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        var legacy = try await service.importVideo(at: source, existing: [])
        legacy.sourceFileName = nil

        let reimported = try await service.importVideo(at: source, existing: [legacy])

        #expect(reimported.id == legacy.id)
    }

    /// The still must be generated at a higher resolution than the grid
    /// thumbnail, or it is soft as a preview and unusable as a desktop picture.
    @Test func importGeneratesBothAThumbnailAndAFullResolutionStill() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        // Larger than the thumbnail cap, so the two sizes genuinely differ.
        let source = try await makeSourceVideo(size: 1280)
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])

        let thumbnail = try #require(storage.thumbnailURL(for: record))
        let still = try #require(storage.stillURL(for: record))
        #expect(FileManager.default.fileExists(atPath: thumbnail.path))
        #expect(FileManager.default.fileExists(atPath: still.path))
        #expect(thumbnail != still)

        let thumbnailWidth = try #require(pixelWidth(of: thumbnail))
        let stillWidth = try #require(pixelWidth(of: still))
        #expect(stillWidth > thumbnailWidth)
    }

    /// Entries imported before stills existed must gain one on launch.
    @Test func backfillGeneratesStillsForOlderRecords() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        var legacy = try await service.importVideo(at: source, existing: [])
        try FileManager.default.removeItem(at: #require(storage.stillURL(for: legacy)))
        legacy.stillRelativePath = nil

        let updated = await service.backfillStills(for: [legacy])

        let record = try #require(updated.first)
        #expect(record.id == legacy.id)
        let still = try #require(storage.stillURL(for: record))
        #expect(FileManager.default.fileExists(atPath: still.path))
    }

    /// A backfill pass must not redo work that is already done.
    @Test func backfillSkipsRecordsThatAlreadyHaveAStill() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])
        #expect(await service.backfillStills(for: [record]).isEmpty)
    }

    @Test func deletingARecordRemovesItsStillToo() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])
        let still = try #require(storage.stillURL(for: record))
        service.delete(record)

        #expect(!FileManager.default.fileExists(atPath: still.path))
    }

    @Test func unsupportedExtensionsAreRejected() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = FileManager.default.temporaryDirectory.appendingPathComponent("clip.avi")
        try Data("not a movie".utf8).write(to: source)
        defer { try? FileManager.default.removeItem(at: source) }

        await #expect(throws: WallpaperError.notPlayable(name: "clip.avi")) {
            _ = try await service.importVideo(at: source, existing: [])
        }
    }

    @Test func corruptFilesAreRejectedWithoutEnteringStorage() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = FileManager.default.temporaryDirectory
            .appendingPathComponent("corrupt-\(UUID().uuidString).mov")
        try Data(repeating: 0, count: 2048).write(to: source)
        defer { try? FileManager.default.removeItem(at: source) }

        await #expect(throws: (any Error).self) {
            _ = try await service.importVideo(at: source, existing: [])
        }
        let stored = try FileManager.default.contentsOfDirectory(atPath: storage.videosDirectory.path)
        #expect(stored.isEmpty)
    }

    @Test func missingSourceFilesAreReportedClearly() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = FileManager.default.temporaryDirectory
            .appendingPathComponent("gone-\(UUID().uuidString).mov")

        await #expect(throws: WallpaperError.fileMissing(name: source.lastPathComponent)) {
            _ = try await service.importVideo(at: source, existing: [])
        }
    }

    @Test func deletingARecordRemovesItsFiles() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])
        service.delete(record)

        #expect(!service.fileExists(for: record))
        if let thumbnail = storage.thumbnailURL(for: record) {
            #expect(!FileManager.default.fileExists(atPath: thumbnail.path))
        }
    }

    @Test func deletingARecordTwiceIsHarmless() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])
        service.delete(record)
        service.delete(record)

        #expect(!service.fileExists(for: record))
    }

    @Test func orphanedFilesAreRemovedButKnownFilesAreKept() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let record = try await service.importVideo(at: source, existing: [])
        let orphan = storage.videosDirectory.appendingPathComponent("orphan.mov")
        try Data("stale".utf8).write(to: orphan)

        service.removeOrphanedFiles(keeping: [record])

        #expect(!FileManager.default.fileExists(atPath: orphan.path))
        #expect(service.fileExists(for: record))
    }
}
