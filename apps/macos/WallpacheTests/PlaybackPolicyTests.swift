import Foundation
import Testing

@testable import Wallpache

/// Stubbed power conditions so the policy can be tested without a real machine
/// state.
@MainActor
private final class StubPowerConditions: PowerConditions {
    var isLowPowerModeEnabled = false
    var thermalState: ProcessInfo.ThermalState = .nominal
}

@MainActor
struct PlaybackPolicyTests {
    private func makePolicy() -> (PlaybackPolicyController, StubPowerConditions) {
        let power = StubPowerConditions()
        let policy = PlaybackPolicyController(power: power)
        policy.isWallpaperEnabled = true
        return (policy, power)
    }

    @Test func playsWhenEverythingIsNormal() {
        let (policy, _) = makePolicy()
        #expect(policy.shouldPlay)
        #expect(policy.suspensionReason == nil)
    }

    @Test func doesNotPlayWhenNoWallpaperIsEnabled() {
        let (policy, _) = makePolicy()
        policy.isWallpaperEnabled = false

        #expect(!policy.shouldPlay)
        #expect(policy.suspensionReason == .notEnabled)
    }

    @Test func userPauseWinsOverEverythingElse() {
        let (policy, _) = makePolicy()
        policy.isUserPaused = true

        #expect(policy.suspensionReason == .userPaused)
    }

    @Test func sleepSuspendsPlayback() {
        let (policy, _) = makePolicy()
        policy.isSystemAsleep = true

        #expect(policy.suspensionReason == .systemAsleep)
    }

    @Test func lowPowerModeIsHonouredOnlyWhenEnabled() {
        let (policy, power) = makePolicy()
        power.isLowPowerModeEnabled = true
        #expect(policy.suspensionReason == .lowPowerMode)

        policy.preferences.pauseInLowPowerMode = false
        #expect(policy.shouldPlay)
    }

    @Test(arguments: [ProcessInfo.ThermalState.serious, .critical])
    func thermalPressureSuspendsPlayback(state: ProcessInfo.ThermalState) {
        let (policy, power) = makePolicy()
        power.thermalState = state
        policy.reevaluate()

        #expect(policy.suspensionReason == .thermalPressure)
    }

    @Test(arguments: [ProcessInfo.ThermalState.nominal, .fair])
    func moderateThermalStatesKeepPlaying(state: ProcessInfo.ThermalState) {
        let (policy, power) = makePolicy()
        power.thermalState = state
        policy.reevaluate()

        #expect(policy.shouldPlay)
    }

    @Test func screenLockIsHonouredOnlyWhenEnabled() {
        let (policy, _) = makePolicy()
        policy.isScreenLocked = true
        #expect(policy.suspensionReason == .screenLocked)

        policy.preferences.pauseWhenScreenLocked = false
        #expect(policy.shouldPlay)
    }

    @Test func decisionChangesAreReportedOncePerTransition() {
        let (policy, _) = makePolicy()
        var decisions: [Bool] = []
        policy.onDecisionChanged = { decisions.append($0) }

        policy.isUserPaused = true
        policy.isUserPaused = true      // no change
        policy.isScreenLocked = true    // already suspended, decision unchanged
        policy.isScreenLocked = false
        policy.isUserPaused = false

        #expect(decisions == [false, true])
    }

    /// Sleep and wake happen repeatedly during a workday; the policy has to be
    /// idempotent across them.
    @Test func repeatedSleepWakeCyclesRestorePlayback() {
        let (policy, _) = makePolicy()

        for _ in 0..<5 {
            policy.isSystemAsleep = true
            #expect(!policy.shouldPlay)
            policy.isSystemAsleep = false
            #expect(policy.shouldPlay)
        }
    }
}
