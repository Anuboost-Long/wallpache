import Foundation

/// Errors surfaced to the user. Every case describes a recoverable situation:
/// the app falls back to the plain macOS wallpaper rather than crashing.
nonisolated enum WallpaperError: LocalizedError, Equatable {
    case fileMissing(name: String)
    case notPlayable(name: String)
    case protectedContent(name: String)
    case noVideoTrack(name: String)
    case emptyDuration(name: String)
    case importFailed(name: String, reason: String)
    case storageUnavailable(reason: String)

    var errorDescription: String? {
        switch self {
        case .fileMissing(let name):
            return "“\(name)” could not be found. It may have been moved or deleted."
        case .notPlayable(let name):
            return "“\(name)” cannot be played on this Mac. Its format may be unsupported."
        case .protectedContent(let name):
            return "“\(name)” is protected by DRM and cannot be used as a wallpaper."
        case .noVideoTrack(let name):
            return "“\(name)” does not contain a video track."
        case .emptyDuration(let name):
            return "“\(name)” has no playable duration."
        case .importFailed(let name, let reason):
            return "“\(name)” could not be imported. \(reason)"
        case .storageUnavailable(let reason):
            return "Wallpache could not open its storage folder. \(reason)"
        }
    }

    var recoverySuggestion: String? {
        switch self {
        case .notPlayable, .noVideoTrack, .emptyDuration, .protectedContent:
            return "Try an H.264 or HEVC .mp4, .mov, or .m4v file."
        case .fileMissing:
            return "Import the video again."
        case .importFailed, .storageUnavailable:
            return nil
        }
    }
}
