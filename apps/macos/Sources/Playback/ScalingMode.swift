import AVFoundation

/// How a video is fitted into a display.
nonisolated enum ScalingMode: String, Codable, CaseIterable, Identifiable, Sendable {
    /// Preserve aspect ratio, crop the overflow.
    case fill
    /// Preserve aspect ratio, show the whole frame.
    case fit
    /// Ignore aspect ratio and cover the display.
    case stretch
    /// Keep the natural pixel size, centred on a black background.
    case center

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .fill: return "Fill"
        case .fit: return "Fit"
        case .stretch: return "Stretch"
        case .center: return "Center"
        }
    }

    var videoGravity: AVLayerVideoGravity {
        switch self {
        case .fill: return .resizeAspectFill
        case .fit, .center: return .resizeAspect
        case .stretch: return .resize
        }
    }

    /// `center` is the only mode that sizes the layer itself instead of
    /// letting the gravity resolve against the full display bounds.
    var usesNaturalSize: Bool { self == .center }
}
