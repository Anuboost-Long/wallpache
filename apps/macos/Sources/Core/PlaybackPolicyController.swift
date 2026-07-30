import Foundation
import os

/// Decides whether wallpapers may play right now.
///
/// The whole rule set is one derived value:
///
/// ```text
/// shouldPlay = wallpaperEnabled
///     AND userDidNotPause
///     AND systemIsAwake
///     AND energyPolicyAllowsPlayback
/// ```
///
/// Keeping it in one place means the coordinator never has to reason about
/// sleep, thermal state, and Low Power Mode separately.
@MainActor
final class PlaybackPolicyController {
    /// Why playback is currently suspended, for display in the menu bar.
    enum SuspensionReason: Equatable {
        case notEnabled
        case userPaused
        case systemAsleep
        case screenLocked
        case lowPowerMode
        case thermalPressure

        var description: String {
            switch self {
            case .notEnabled: return "No wallpaper running"
            case .userPaused: return "Paused"
            case .systemAsleep: return "Paused while the Mac sleeps"
            case .screenLocked: return "Paused while the screen is locked"
            case .lowPowerMode: return "Paused in Low Power Mode"
            case .thermalPressure: return "Paused while the Mac is hot"
            }
        }
    }

    /// Fired whenever the decision flips, never on redundant input changes.
    var onDecisionChanged: ((Bool) -> Void)?

    var isWallpaperEnabled = false { didSet { decisionDidChange(from: oldValue, to: isWallpaperEnabled) } }
    var isUserPaused = false { didSet { decisionDidChange(from: oldValue, to: isUserPaused) } }
    var isSystemAsleep = false { didSet { decisionDidChange(from: oldValue, to: isSystemAsleep) } }
    var isScreenLocked = false { didSet { decisionDidChange(from: oldValue, to: isScreenLocked) } }
    var preferences = EnergyPreferences() { didSet { decisionDidChange(from: oldValue, to: preferences) } }

    private let power: PowerConditions
    private var lastDecision = false

    init(power: PowerConditions) {
        self.power = power
    }

    var shouldPlay: Bool { suspensionReason == nil }

    var suspensionReason: SuspensionReason? {
        if !isWallpaperEnabled { return .notEnabled }
        if isUserPaused { return .userPaused }
        if isSystemAsleep { return .systemAsleep }
        if preferences.pauseWhenScreenLocked, isScreenLocked { return .screenLocked }
        if preferences.pauseInLowPowerMode, power.isLowPowerModeEnabled { return .lowPowerMode }
        if preferences.pauseOnHighThermalState, power.thermalState.isUnderPressure { return .thermalPressure }
        return nil
    }

    /// Called when an external input changed without going through a property,
    /// such as a Low Power Mode or thermal notification.
    func reevaluate() {
        publishIfChanged()
    }

    // MARK: - Private

    private func decisionDidChange<T: Equatable>(from oldValue: T, to newValue: T) {
        guard oldValue != newValue else { return }
        publishIfChanged()
    }

    private func publishIfChanged() {
        let decision = shouldPlay
        guard decision != lastDecision else { return }

        lastDecision = decision
        Log.policy.info("shouldPlay: \(decision, privacy: .public) (\(self.suspensionReason?.description ?? "running", privacy: .public))")
        onDecisionChanged?(decision)
    }
}
