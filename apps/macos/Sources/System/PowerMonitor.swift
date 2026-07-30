import Foundation
import os

/// The power inputs the playback policy reads. Abstracted so the policy can be
/// unit tested without a real machine state.
@MainActor
protocol PowerConditions: AnyObject {
    var isLowPowerModeEnabled: Bool { get }
    var thermalState: ProcessInfo.ThermalState { get }
}

/// Live Low Power Mode and thermal state, with a change callback.
@MainActor
final class PowerMonitor: PowerConditions {
    var onChange: (() -> Void)?

    private let processInfo: ProcessInfo
    private var observers: [NSObjectProtocol] = []

    init(processInfo: ProcessInfo = .processInfo) {
        self.processInfo = processInfo
    }

    var isLowPowerModeEnabled: Bool { processInfo.isLowPowerModeEnabled }
    var thermalState: ProcessInfo.ThermalState { processInfo.thermalState }

    func start() {
        guard observers.isEmpty else { return }

        let center = NotificationCenter.default
        let names: [Notification.Name] = [
            .NSProcessInfoPowerStateDidChange,
            ProcessInfo.thermalStateDidChangeNotification
        ]

        observers = names.map { name in
            center.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated {
                    guard let self else { return }
                    Log.policy.info("Power conditions changed (lowPower: \(self.isLowPowerModeEnabled, privacy: .public))")
                    self.onChange?()
                }
            }
        }
    }

    func stop() {
        observers.forEach(NotificationCenter.default.removeObserver)
        observers.removeAll()
    }
}

extension ProcessInfo.ThermalState {
    /// `serious` and `critical` mean the machine is already throttling; a
    /// decoding video wallpaper is the first thing worth giving up.
    var isUnderPressure: Bool {
        self == .serious || self == .critical
    }
}
