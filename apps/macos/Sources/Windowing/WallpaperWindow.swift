import AppKit

/// Borderless, non-interactive window that hosts the video for one display.
///
/// It must never take focus: the desktop has to keep behaving like the desktop.
final class WallpaperWindow: NSWindow {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    init(screen: NSScreen) {
        super.init(
            contentRect: screen.frame,
            styleMask: [.borderless],
            backing: .buffered,
            defer: false
        )
        // Screen frames are in global coordinates, so setting the frame is what
        // actually places the window on the intended display.
        setFrame(screen.frame, display: false)

        isOpaque = true
        hasShadow = false
        backgroundColor = .black
        ignoresMouseEvents = true
        isMovable = false
        isRestorable = false
        // The controller owns the lifetime; `close()` must not deallocate.
        isReleasedWhenClosed = false
        displaysWhenScreenProfileChanges = true
        animationBehavior = .none
        collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle, .fullScreenNone]
        level = DesktopWindowLevel.wallpaper
    }
}

/// The window level calculation lives alone so it can be adjusted for a future
/// macOS release without touching the engine.
///
/// The goal is: above the static desktop picture, below Finder's desktop icons.
enum DesktopWindowLevel {
    static var wallpaper: NSWindow.Level {
        NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.desktopWindow)) + 1)
    }
}
