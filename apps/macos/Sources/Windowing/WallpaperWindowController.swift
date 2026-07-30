import AVFoundation
import AppKit

/// Owns the wallpaper window for exactly one display.
@MainActor
final class WallpaperWindowController {
    private let window: WallpaperWindow
    private let playerView: WallpaperPlayerView

    init(screen: NSScreen) {
        window = WallpaperWindow(screen: screen)
        playerView = WallpaperPlayerView(frame: NSRect(origin: .zero, size: screen.frame.size))
        playerView.autoresizingMask = [.width, .height]
        window.contentView = playerView
    }

    func show() {
        // `orderFrontRegardless` is required: an accessory app is not active,
        // so an ordinary `orderFront` can be ignored.
        window.orderFrontRegardless()
    }

    func close() {
        playerView.setPlayer(nil)
        window.orderOut(nil)
        window.close()
    }

    func attach(player: AVPlayer?) {
        playerView.setPlayer(player)
    }

    func apply(scalingMode: ScalingMode, videoPixelSize: CGSize?) {
        playerView.videoPixelSize = videoPixelSize
        playerView.scalingMode = scalingMode
    }

    /// Follows a display that changed resolution, arrangement, or rotation.
    func move(to screen: NSScreen) {
        guard window.frame != screen.frame else { return }
        window.setFrame(screen.frame, display: true)
        playerView.frame = NSRect(origin: .zero, size: screen.frame.size)
        playerView.needsLayout = true
    }

    /// Re-asserts desktop placement. Finder restarts and some display changes
    /// can leave the window ordered incorrectly; this is cheap and idempotent.
    func reassertDesktopPlacement() {
        window.level = DesktopWindowLevel.wallpaper
        window.orderFrontRegardless()
    }
}
