import Foundation
import Testing

@testable import Wallpache

/// Exercises the staging tray: what a dropped file becomes before the user
/// confirms, and what a confirmed batch does.
@MainActor
struct ImportQueueTests {
    private func makeStorage() throws -> WallpaperStorage {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("WallpacheTests-\(UUID().uuidString)", isDirectory: true)
        let storage = WallpaperStorage(root: root)
        try storage.prepareDirectories()
        return storage
    }

    private func makeSourceVideo() async throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("Sample-\(UUID().uuidString).mov")
        try await SampleVideo.write(to: url)
        return url
    }

    /// Inspection and importing run on their own tasks, so the tests wait for
    /// the tray to settle rather than assuming it already has.
    private func wait(for condition: () -> Bool) async throws {
        for _ in 0..<300 where !condition() {
            try await Task.sleep(nanoseconds: 20_000_000)
        }
        #expect(condition())
    }

    @Test func stagedVideosAreInspectedButNotImported() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let queue = ImportQueue { _ = try await service.importVideo(at: $0, existing: []) }
        queue.stage([source])

        try await wait { queue.items.first?.state == .ready }

        let item = try #require(queue.items.first)
        #expect(item.detail != nil)
        #expect(item.preview != nil)
        // Nothing is copied until the user confirms.
        #expect(try FileManager.default.contentsOfDirectory(atPath: storage.videosDirectory.path).isEmpty)
    }

    @Test func unsupportedFilesAreRejectedWithAReason() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = FileManager.default.temporaryDirectory
            .appendingPathComponent("Notes-\(UUID().uuidString).txt")
        try Data("not a video".utf8).write(to: source)
        defer { try? FileManager.default.removeItem(at: source) }

        let queue = ImportQueue { _ = try await service.importVideo(at: $0, existing: []) }
        queue.stage([source])

        try await wait { queue.items.first?.problem != nil }
        #expect(queue.readyItems.isEmpty)
    }

    @Test func theSameFileIsNotStagedTwice() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let queue = ImportQueue { _ = try await service.importVideo(at: $0, existing: []) }
        queue.stage([source, source])
        queue.stage([source])

        #expect(queue.items.count == 1)
    }

    @Test func removingAStagedFileLeavesItOutOfTheImport() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let kept = try await makeSourceVideo()
        let dropped = try await makeSourceVideo()
        defer {
            try? FileManager.default.removeItem(at: kept)
            try? FileManager.default.removeItem(at: dropped)
        }

        let queue = ImportQueue { _ = try await service.importVideo(at: $0, existing: []) }
        queue.stage([kept, dropped])

        try await wait { queue.readyItems.count == 2 }

        let unwanted = try #require(queue.items.first { $0.url == dropped })
        queue.remove(unwanted)
        queue.importReadyItems()

        try await wait { queue.items.isEmpty }
        #expect(try FileManager.default.contentsOfDirectory(atPath: storage.videosDirectory.path).count == 1)
    }

    @Test func confirmingImportsEveryReadyFileAndClearsTheTray() async throws {
        let storage = try makeStorage()
        defer { try? FileManager.default.removeItem(at: storage.root) }

        let service = WallpaperLibraryService(storage: storage)
        let sources = [try await makeSourceVideo(), try await makeSourceVideo()]
        defer { sources.forEach { try? FileManager.default.removeItem(at: $0) } }

        let queue = ImportQueue { _ = try await service.importVideo(at: $0, existing: []) }
        queue.stage(sources)

        try await wait { queue.readyItems.count == sources.count }

        queue.importReadyItems()
        try await wait { queue.items.isEmpty }

        #expect(!queue.isImporting)
        #expect(try FileManager.default.contentsOfDirectory(atPath: storage.videosDirectory.path).count == sources.count)
    }

    @Test func aFailedImportStaysInTheTrayWithItsError() async throws {
        let source = try await makeSourceVideo()
        defer { try? FileManager.default.removeItem(at: source) }

        let queue = ImportQueue { throw WallpaperError.importFailed(name: $0.lastPathComponent, reason: "disk full") }
        queue.stage([source])

        try await wait { queue.readyItems.count == 1 }

        queue.importReadyItems()
        try await wait { !queue.isImporting && queue.items.first?.problem != nil }

        #expect(queue.items.count == 1)
    }
}
