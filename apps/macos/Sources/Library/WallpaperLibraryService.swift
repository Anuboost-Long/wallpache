import AVFoundation
import Foundation
import UniformTypeIdentifiers
import os

/// Imports, validates, and removes local wallpapers.
///
/// Imported files are copied into app-controlled storage. That keeps a
/// wallpaper working after the source is moved, renamed, or unplugged with an
/// external drive, and avoids security-scoped bookmark bookkeeping.
/// `@unchecked` only because `FileManager` carries no `Sendable` annotation;
/// the file operations used here are documented as thread-safe.
nonisolated struct WallpaperLibraryService: @unchecked Sendable {
    /// Container types accepted at import time.
    static let supportedContentTypes: [UTType] = {
        var types: [UTType] = [.mpeg4Movie, .quickTimeMovie]
        if let m4v = UTType("com.apple.m4v-video") {
            types.append(m4v)
        }
        return types
    }()

    static let supportedExtensions: Set<String> = ["mp4", "mov", "m4v"]

    let storage: WallpaperStorage
    private let fileManager: FileManager

    init(storage: WallpaperStorage, fileManager: FileManager = .default) {
        self.storage = storage
        self.fileManager = fileManager
    }

    /// Validates `sourceURL`, copies it into storage, and returns the new
    /// record. `existing` is used to skip re-importing the same file twice.
    func importVideo(at sourceURL: URL, existing: [WallpaperRecord]) async throws -> WallpaperRecord {
        let name = sourceURL.lastPathComponent

        guard Self.supportedExtensions.contains(sourceURL.pathExtension.lowercased()) else {
            throw WallpaperError.notPlayable(name: name)
        }

        // Validate before copying so an unusable file never enters storage.
        let metadata = try await VideoMetadataReader.read(url: sourceURL, fileManager: fileManager)
        let sourceSize = fileSize(of: sourceURL)

        if let duplicate = existing.first(where: { isDuplicate($0, of: sourceURL, size: sourceSize) }) {
            Log.library.info("Skipping duplicate import of \(name, privacy: .public)")
            return duplicate
        }

        let id = UUID()
        let fileName = "\(id.uuidString).\(sourceURL.pathExtension.lowercased())"
        let relativePath = storage.relativePath(directory: WallpaperStorage.videosDirectoryName, fileName: fileName)
        let destination = storage.url(forRelativePath: relativePath)

        do {
            try storage.prepareDirectories(fileManager: fileManager)
            try fileManager.copyItem(at: sourceURL, to: destination)
        } catch {
            throw WallpaperError.importFailed(name: name, reason: error.localizedDescription)
        }

        let thumbnailPath = await makeImage(
            for: id,
            videoURL: destination,
            duration: metadata.duration,
            directory: WallpaperStorage.thumbnailsDirectoryName,
            maximumPixelWidth: ThumbnailGenerator.thumbnailPixelWidth
        )
        let stillPath = await makeImage(
            for: id,
            videoURL: destination,
            duration: metadata.duration,
            directory: WallpaperStorage.stillsDirectoryName,
            maximumPixelWidth: ThumbnailGenerator.stillPixelWidth
        )

        return WallpaperRecord(
            id: id,
            name: sourceURL.deletingPathExtension().lastPathComponent,
            relativePath: relativePath,
            thumbnailRelativePath: thumbnailPath,
            stillRelativePath: stillPath,
            dateImported: Date(),
            duration: metadata.duration,
            width: metadata.pixelSize.map { Int($0.width) },
            height: metadata.pixelSize.map { Int($0.height) },
            fileSize: fileSize(of: destination),
            sourceFileName: name
        )
    }

    /// Deletes the imported copy and both generated images. Missing files are
    /// ignored: removing a record must always succeed from the user's point of
    /// view.
    func delete(_ record: WallpaperRecord) {
        let urls = [
            storage.videoURL(for: record),
            storage.thumbnailURL(for: record),
            storage.stillURL(for: record)
        ]
        for url in urls.compactMap({ $0 }) {
            try? fileManager.removeItem(at: url)
        }
    }

    /// Generates the full-resolution still for entries imported before stills
    /// existed, and returns the records that gained one so the caller can
    /// persist them. Entries whose video has gone are skipped.
    func backfillStills(for records: [WallpaperRecord]) async -> [WallpaperRecord] {
        var updated: [WallpaperRecord] = []

        for record in records where record.stillRelativePath == nil {
            guard fileExists(for: record) else { continue }

            let path = await makeImage(
                for: record.id,
                videoURL: storage.videoURL(for: record),
                duration: record.duration,
                directory: WallpaperStorage.stillsDirectoryName,
                maximumPixelWidth: ThumbnailGenerator.stillPixelWidth
            )
            guard let path else { continue }

            var record = record
            record.stillRelativePath = path
            updated.append(record)
        }

        return updated
    }

    func fileExists(for record: WallpaperRecord) -> Bool {
        fileManager.fileExists(atPath: storage.videoURL(for: record).path)
    }

    /// Removes stored files that no record points at, e.g. after a crash
    /// between the copy and the configuration save.
    func removeOrphanedFiles(keeping records: [WallpaperRecord]) {
        let keep = Set(records.flatMap { record in
            [record.relativePath, record.thumbnailRelativePath, record.stillRelativePath]
                .compactMap { $0 }
                .map { ($0 as NSString).lastPathComponent }
        })

        for directory in [storage.videosDirectory, storage.thumbnailsDirectory, storage.stillsDirectory] {
            let contents = (try? fileManager.contentsOfDirectory(atPath: directory.path)) ?? []
            for file in contents where !keep.contains(file) {
                try? fileManager.removeItem(at: directory.appendingPathComponent(file))
            }
        }
    }

    // MARK: - Private

    private func makeImage(
        for id: UUID,
        videoURL: URL,
        duration: Double,
        directory: String,
        maximumPixelWidth: CGFloat
    ) async -> String? {
        let relativePath = storage.relativePath(
            directory: directory,
            fileName: "\(id.uuidString).png"
        )
        let destination = storage.url(forRelativePath: relativePath)
        let generated = await ThumbnailGenerator.generate(
            from: videoURL,
            to: destination,
            duration: duration,
            maximumPixelWidth: maximumPixelWidth
        )
        return generated ? relativePath : nil
    }

    /// A repeat import is recognised by identical source file name and byte
    /// size. Records imported before renaming existed have no stored source
    /// name, so those fall back to the display name they were created with.
    private func isDuplicate(_ record: WallpaperRecord, of sourceURL: URL, size: Int64) -> Bool {
        let matchesName = record.sourceFileName.map { $0 == sourceURL.lastPathComponent }
            ?? (record.name == sourceURL.deletingPathExtension().lastPathComponent)

        return matchesName
            && record.fileSize == size
            && size > 0
            && fileExists(for: record)
    }

    private func fileSize(of url: URL) -> Int64 {
        let attributes = try? fileManager.attributesOfItem(atPath: url.path)
        return (attributes?[.size] as? NSNumber)?.int64Value ?? 0
    }
}
