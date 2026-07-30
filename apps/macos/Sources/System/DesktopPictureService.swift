import AppKit
import os

/// Sets the real macOS desktop picture so a still image sits underneath the
/// video window.
///
/// The wallpaper window only exists while the app runs. Without this, quitting
/// or pausing reveals whatever unrelated picture macOS was already showing.
/// Pointing the system wallpaper at the video's own thumbnail makes that
/// fallback match the live wallpaper instead.
///
/// The picture the user had before is remembered per display so switching the
/// option off puts their own wallpaper back rather than stranding them with a
/// video frame they have to undo in System Settings.
@MainActor
final class DesktopPictureService {
    private let workspace: NSWorkspace

    init(workspace: NSWorkspace = .shared) {
        self.workspace = workspace
    }

    /// The picture macOS is showing on `screen` right now.
    func currentPictureURL(for screen: NSScreen) -> URL? {
        workspace.desktopImageURL(for: screen)
    }

    /// Points the system wallpaper at `imageURL`. Returns `false` when macOS
    /// rejects it; a failed desktop picture is cosmetic, so callers log rather
    /// than surface an error.
    @discardableResult
    func setPicture(_ imageURL: URL, on screen: NSScreen, scalingMode: ScalingMode) -> Bool {
        do {
            try workspace.setDesktopImageURL(imageURL, for: screen, options: options(for: scalingMode))
            return true
        } catch {
            Log.displays.error("Desktop picture update failed: \(error.localizedDescription, privacy: .public)")
            return false
        }
    }

    /// Maps the app's scaling modes onto the system wallpaper equivalents so the
    /// still frame is framed the same way as the video above it.
    private func options(for scalingMode: ScalingMode) -> [NSWorkspace.DesktopImageOptionKey: Any] {
        let scaling: NSImageScaling
        let allowClipping: Bool

        switch scalingMode {
        case .fill:
            scaling = .scaleProportionallyUpOrDown
            allowClipping = true
        case .fit:
            scaling = .scaleProportionallyUpOrDown
            allowClipping = false
        case .stretch:
            scaling = .scaleAxesIndependently
            allowClipping = false
        case .center:
            scaling = .scaleNone
            allowClipping = false
        }

        return [
            .imageScaling: NSNumber(value: scaling.rawValue),
            .allowClipping: NSNumber(value: allowClipping),
            .fillColor: NSColor.black
        ]
    }
}
