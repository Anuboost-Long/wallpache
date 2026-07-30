import AppKit
import AVFoundation

/// One display's live wallpaper: its window, its player, and its settings.
///
/// A session is the only object that knows both halves of the engine, which
/// keeps window code out of the player and vice versa.
@MainActor
final class WallpaperSession {
    let displayID: CGDirectDisplayID

    private(set) var configuration: DisplayWallpaperConfiguration
    private(set) var wallpaperID: UUID

    private let windowController: WallpaperWindowController
    private let player: VideoLoopPlayer

    var onPlaybackFailure: ((CGDirectDisplayID, Error?) -> Void)?

    init(
        screen: NSScreen,
        displayID: CGDirectDisplayID,
        wallpaperID: UUID,
        videoURL: URL,
        videoPixelSize: CGSize?,
        configuration: DisplayWallpaperConfiguration
    ) {
        self.displayID = displayID
        self.wallpaperID = wallpaperID
        self.configuration = configuration

        windowController = WallpaperWindowController(screen: screen)
        player = VideoLoopPlayer(
            url: videoURL,
            isMuted: configuration.isMuted,
            rate: configuration.playbackRate
        )

        windowController.attach(player: player.player)
        windowController.apply(scalingMode: configuration.scalingMode, videoPixelSize: videoPixelSize)
        windowController.show()

        player.onUnrecoverableFailure = { [weak self] error in
            guard let self else { return }
            self.onPlaybackFailure?(self.displayID, error)
        }
    }

    // MARK: - Transport

    func play() {
        player.play()
    }

    func pause() {
        player.pause()
    }

    /// Tears the session down completely; the plain macOS wallpaper reappears.
    func stop() {
        player.stop()
        windowController.close()
    }

    // MARK: - Updates

    func update(configuration newConfiguration: DisplayWallpaperConfiguration, videoPixelSize: CGSize?) {
        if newConfiguration.scalingMode != configuration.scalingMode {
            windowController.apply(scalingMode: newConfiguration.scalingMode, videoPixelSize: videoPixelSize)
        }
        if newConfiguration.isMuted != configuration.isMuted {
            player.setMuted(newConfiguration.isMuted)
        }
        if newConfiguration.playbackRate != configuration.playbackRate {
            player.setRate(newConfiguration.playbackRate)
        }
        configuration = newConfiguration
    }

    /// Swaps the video without recreating the window, so applying a different
    /// wallpaper never flashes the desktop.
    func replaceVideo(wallpaperID newID: UUID, url: URL, videoPixelSize: CGSize?) {
        wallpaperID = newID
        player.replaceVideo(url: url)
        windowController.apply(scalingMode: configuration.scalingMode, videoPixelSize: videoPixelSize)
    }

    func move(to screen: NSScreen) {
        windowController.move(to: screen)
    }

    /// Re-establishes both halves after wake, a Finder restart, or a display
    /// reconfiguration. Idempotent by design: it can run on every event.
    func recover(shouldPlay: Bool) {
        windowController.reassertDesktopPlacement()
        player.recoverIfNeeded()
        if shouldPlay {
            player.play()
        } else {
            player.pause()
        }
    }
}
