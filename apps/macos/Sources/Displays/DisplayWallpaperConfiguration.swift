import CoreGraphics
import Foundation

/// The persisted wallpaper assignment for one display.
nonisolated struct DisplayWallpaperConfiguration: Codable, Hashable, Identifiable, Sendable {
    var displayID: CGDirectDisplayID
    var wallpaperID: UUID?
    var scalingMode: ScalingMode
    var playbackRate: Float
    var isMuted: Bool
    /// Whether the video's still frame is also set as the macOS desktop
    /// picture, so the display keeps a matching background once the app quits.
    var setsDesktopPicture: Bool

    var id: CGDirectDisplayID { displayID }

    init(
        displayID: CGDirectDisplayID,
        wallpaperID: UUID? = nil,
        scalingMode: ScalingMode = .fill,
        playbackRate: Float = 1.0,
        isMuted: Bool = true,
        setsDesktopPicture: Bool = false
    ) {
        self.displayID = displayID
        self.wallpaperID = wallpaperID
        self.scalingMode = scalingMode
        self.playbackRate = playbackRate
        self.isMuted = isMuted
        self.setsDesktopPicture = setsDesktopPicture
    }

    /// Decoding tolerates configurations written by older builds so that a
    /// settings upgrade never wipes a user's assignments.
    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        displayID = try container.decode(CGDirectDisplayID.self, forKey: .displayID)
        wallpaperID = try container.decodeIfPresent(UUID.self, forKey: .wallpaperID)
        scalingMode = try container.decodeIfPresent(ScalingMode.self, forKey: .scalingMode) ?? .fill
        playbackRate = try container.decodeIfPresent(Float.self, forKey: .playbackRate) ?? 1.0
        isMuted = try container.decodeIfPresent(Bool.self, forKey: .isMuted) ?? true
        setsDesktopPicture = try container.decodeIfPresent(Bool.self, forKey: .setsDesktopPicture) ?? false
    }
}

extension DisplayWallpaperConfiguration {
    /// Playback rates offered in the UI.
    static let supportedRates: [Float] = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0]
}
