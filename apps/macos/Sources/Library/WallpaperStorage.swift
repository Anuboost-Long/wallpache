import Foundation

/// Owns the on-disk layout of imported wallpapers.
///
/// ```text
/// ~/Library/Application Support/Wallpache/
/// ├── Wallpapers/<uuid>.<ext>
/// ├── Stills/<uuid>.png       full resolution, for preview and desktop picture
/// └── Thumbnails/<uuid>.png   grid size
/// ```
///
/// In a sandboxed build this resolves inside the application container, which
/// keeps imported files readable without security-scoped bookmarks.
nonisolated struct WallpaperStorage: Sendable {
    static let videosDirectoryName = "Wallpapers"
    static let thumbnailsDirectoryName = "Thumbnails"
    static let stillsDirectoryName = "Stills"

    let root: URL

    var videosDirectory: URL {
        root.appendingPathComponent(Self.videosDirectoryName, isDirectory: true)
    }

    var thumbnailsDirectory: URL {
        root.appendingPathComponent(Self.thumbnailsDirectoryName, isDirectory: true)
    }

    var stillsDirectory: URL {
        root.appendingPathComponent(Self.stillsDirectoryName, isDirectory: true)
    }

    init(root: URL) {
        self.root = root
    }

    /// The default location inside Application Support.
    static func makeDefault(fileManager: FileManager = .default) throws -> WallpaperStorage {
        do {
            let support = try fileManager.url(
                for: .applicationSupportDirectory,
                in: .userDomainMask,
                appropriateFor: nil,
                create: true
            )
            let storage = WallpaperStorage(root: support.appendingPathComponent("Wallpache", isDirectory: true))
            try storage.prepareDirectories(fileManager: fileManager)
            return storage
        } catch let error as WallpaperError {
            throw error
        } catch {
            throw WallpaperError.storageUnavailable(reason: error.localizedDescription)
        }
    }

    func prepareDirectories(fileManager: FileManager = .default) throws {
        for directory in [videosDirectory, thumbnailsDirectory, stillsDirectory] {
            try fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        }
    }

    func url(forRelativePath path: String) -> URL {
        root.appendingPathComponent(path)
    }

    func videoURL(for record: WallpaperRecord) -> URL {
        url(forRelativePath: record.relativePath)
    }

    func thumbnailURL(for record: WallpaperRecord) -> URL? {
        record.thumbnailRelativePath.map(url(forRelativePath:))
    }

    /// The full-resolution still. Records imported before stills existed have
    /// none, so callers fall back to the thumbnail.
    func stillURL(for record: WallpaperRecord) -> URL? {
        record.stillRelativePath.map(url(forRelativePath:))
    }

    func relativePath(directory: String, fileName: String) -> String {
        "\(directory)/\(fileName)"
    }
}
