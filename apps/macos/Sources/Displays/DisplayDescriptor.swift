import AppKit

/// A snapshot of one connected display, keyed by an identifier that survives
/// reordering, sleep, and reconnection.
nonisolated struct DisplayDescriptor: Identifiable, Hashable, Sendable {
    let id: CGDirectDisplayID
    let name: String
    let frame: CGRect
    let backingScaleFactor: CGFloat
    let isPrimary: Bool

    /// Pixel resolution, useful when explaining energy impact to the user.
    var pixelSize: CGSize {
        CGSize(width: frame.width * backingScaleFactor, height: frame.height * backingScaleFactor)
    }

    var resolutionDescription: String {
        "\(Int(pixelSize.width)) × \(Int(pixelSize.height))"
    }
}

extension NSScreen {
    /// The stable Core Graphics display ID. Never persist `NSScreen.screens`
    /// indexes: display ordering changes when monitors are docked.
    var displayID: CGDirectDisplayID? {
        let key = NSDeviceDescriptionKey("NSScreenNumber")
        return (deviceDescription[key] as? NSNumber)?.uint32Value
    }

    var displayDescriptor: DisplayDescriptor? {
        guard let displayID else { return nil }
        return DisplayDescriptor(
            id: displayID,
            name: localizedName,
            frame: frame,
            backingScaleFactor: backingScaleFactor,
            isPrimary: CGDisplayIsMain(displayID) != 0
        )
    }
}
