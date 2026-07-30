import AVFoundation
import AppKit

/// Layer-hosting view that renders the wallpaper video.
///
/// Layout runs whenever the display resolution changes, so frame updates are
/// wrapped in a disabled `CATransaction`: an implicit animation on a wallpaper
/// reads as a glitch.
final class WallpaperPlayerView: NSView {
    private let playerLayer = AVPlayerLayer()

    /// Natural pixel size of the current video, needed by `.center` scaling.
    var videoPixelSize: CGSize? {
        didSet { needsLayout = true }
    }

    var scalingMode: ScalingMode = .fill {
        didSet {
            playerLayer.videoGravity = scalingMode.videoGravity
            needsLayout = true
        }
    }

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        playerLayer.backgroundColor = NSColor.black.cgColor
        playerLayer.videoGravity = scalingMode.videoGravity
        // Layer-hosting view: assign the layer, then opt into layer backing.
        layer = playerLayer
        wantsLayer = true
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func layout() {
        super.layout()

        CATransaction.begin()
        CATransaction.setDisableActions(true)
        playerLayer.frame = videoFrame()
        CATransaction.commit()
    }

    func setPlayer(_ player: AVPlayer?) {
        playerLayer.player = player
    }

    /// `hitTest` returning nil keeps clicks passing through to the desktop even
    /// if the window's mouse handling ever changes.
    override func hitTest(_ point: NSPoint) -> NSView? { nil }

    // MARK: - Private

    private func videoFrame() -> CGRect {
        guard scalingMode.usesNaturalSize, let videoPixelSize, videoPixelSize.width > 0 else {
            return bounds
        }

        // Natural size is in pixels; convert to points so a Retina display
        // shows the video at its true physical size.
        let scale = window?.backingScaleFactor ?? 1
        let size = CGSize(
            width: min(videoPixelSize.width / scale, bounds.width),
            height: min(videoPixelSize.height / scale, bounds.height)
        )

        return CGRect(
            x: bounds.midX - size.width / 2,
            y: bounds.midY - size.height / 2,
            width: size.width,
            height: size.height
        )
    }
}
