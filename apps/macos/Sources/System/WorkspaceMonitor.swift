import AppKit
import os

/// Observes sleep, wake, and screen lock so playback can follow the machine's
/// state. Callbacks are delivered on the main queue.
@MainActor
final class WorkspaceMonitor {
    var onWillSleep: (() -> Void)?
    var onDidWake: (() -> Void)?
    var onScreenLockChanged: ((Bool) -> Void)?

    private var workspaceObservers: [NSObjectProtocol] = []
    private var distributedObservers: [NSObjectProtocol] = []

    func start() {
        guard workspaceObservers.isEmpty else { return }

        let center = NSWorkspace.shared.notificationCenter
        workspaceObservers = [
            center.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated {
                    Log.lifecycle.info("System will sleep")
                    self?.onWillSleep?()
                }
            },
            center.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated {
                    Log.lifecycle.info("System did wake")
                    self?.onDidWake?()
                }
            },
            // Fast user switching hides the desktop just like a lock does.
            center.addObserver(forName: NSWorkspace.sessionDidResignActiveNotification, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated { self?.onScreenLockChanged?(true) }
            },
            center.addObserver(forName: NSWorkspace.sessionDidBecomeActiveNotification, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated { self?.onScreenLockChanged?(false) }
            }
        ]

        startObservingScreenLock()
    }

    func stop() {
        let center = NSWorkspace.shared.notificationCenter
        workspaceObservers.forEach(center.removeObserver)
        workspaceObservers.removeAll()

        let distributed = DistributedNotificationCenter.default()
        distributedObservers.forEach(distributed.removeObserver)
        distributedObservers.removeAll()
    }

    // MARK: - Private

    /// Screen lock arrives through the distributed notification centre. It is
    /// treated as a best-effort signal: delivery is not guaranteed on every
    /// macOS release or sandbox configuration, and playback stays correct
    /// without it.
    private func startObservingScreenLock() {
        let center = DistributedNotificationCenter.default()
        let names = [
            (Notification.Name("com.apple.screenIsLocked"), true),
            (Notification.Name("com.apple.screenIsUnlocked"), false)
        ]

        distributedObservers = names.map { name, isLocked in
            center.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated {
                    Log.lifecycle.info("Screen locked: \(isLocked, privacy: .public)")
                    self?.onScreenLockChanged?(isLocked)
                }
            }
        }
    }
}
