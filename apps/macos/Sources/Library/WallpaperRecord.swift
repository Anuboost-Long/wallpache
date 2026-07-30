import CoreGraphics
import Foundation

/// One imported video in the local library. Paths are stored relative to the
/// storage root so the library keeps working if the container path changes.
nonisolated struct WallpaperRecord: Codable, Identifiable, Hashable, Sendable {
    let id: UUID
    var name: String
    var relativePath: String
    /// Grid-size preview.
    var thumbnailRelativePath: String?
    /// Full-resolution still, used for the preview sheet and as the macOS
    /// desktop picture. Absent for entries imported before stills existed; the
    /// library backfills those on launch.
    var stillRelativePath: String?
    var dateImported: Date
    var duration: Double
    var width: Int?
    var height: Int?
    /// Byte size of the imported copy, used to recognise a repeated import.
    var fileSize: Int64
    /// The file name this was imported from. Duplicate detection uses it rather
    /// than `name`, so renaming an entry cannot cause a re-import to be copied
    /// in a second time. Absent for entries imported before renaming existed.
    var sourceFileName: String?

    var pixelSize: CGSize? {
        guard let width, let height, width > 0, height > 0 else { return nil }
        return CGSize(width: width, height: height)
    }

    var durationDescription: String {
        let total = Int(duration.rounded())
        return String(format: "%d:%02d", total / 60, total % 60)
    }

    var resolutionDescription: String? {
        guard let width, let height else { return nil }
        return "\(width) × \(height)"
    }
}
