import AppKit
import os

/// Enumerates displays and reports changes.
///
/// Docking, undocking, and waking emit several screen-parameter notifications
/// in quick succession, so changes are debounced into one reconcile pass.
@MainActor
final class DisplayManager {
    var onDisplaysChanged: (([DisplayDescriptor]) -> Void)?

    private(set) var displays: [DisplayDescriptor] = []
    private var observer: NSObjectProtocol?
    private var pendingReconcile: DispatchWorkItem?

    private static let debounceInterval: TimeInterval = 0.5

    func start() {
        guard observer == nil else { return }

        displays = Self.currentDisplays()
        observer = NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.scheduleReconcile() }
        }
    }

    func stop() {
        pendingReconcile?.cancel()
        pendingReconcile = nil
        if let observer {
            NotificationCenter.default.removeObserver(observer)
        }
        observer = nil
    }

    /// Refreshes the cached list immediately and notifies if it changed. Used
    /// after wake, where the notification may already have been coalesced.
    func refresh() {
        let updated = Self.currentDisplays()
        guard updated != displays else { return }

        displays = updated
        Log.displays.info("Displays changed: \(updated.map(\.name).joined(separator: ", "), privacy: .public)")
        onDisplaysChanged?(updated)
    }

    static func currentDisplays() -> [DisplayDescriptor] {
        NSScreen.screens.compactMap(\.displayDescriptor)
    }

    // MARK: - Private

    private func scheduleReconcile() {
        pendingReconcile?.cancel()

        let work = DispatchWorkItem { [weak self] in
            MainActor.assumeIsolated {
                self?.pendingReconcile = nil
                self?.refresh()
            }
        }
        pendingReconcile = work
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.debounceInterval, execute: work)
    }
}
