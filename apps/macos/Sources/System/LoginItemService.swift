import Foundation
import ServiceManagement
import os

/// Wraps `SMAppService` so Service Management details stay out of the UI.
@MainActor
final class LoginItemService {
    enum State: Equatable {
        case enabled
        case disabled
        /// macOS requires the user to approve the item in System Settings.
        case requiresApproval
        case unavailable

        var isEnabled: Bool { self == .enabled }
    }

    private let service: SMAppService

    init(service: SMAppService = .mainApp) {
        self.service = service
    }

    var state: State {
        switch service.status {
        case .enabled: return .enabled
        case .notRegistered: return .disabled
        case .requiresApproval: return .requiresApproval
        case .notFound: return .unavailable
        @unknown default: return .unavailable
        }
    }

    /// Returns the resulting state. Registration can fail when the user has
    /// denied the login item, which is reported instead of thrown so the UI can
    /// simply show the real state.
    @discardableResult
    func setEnabled(_ enabled: Bool) -> State {
        do {
            if enabled {
                try service.register()
            } else if service.status != .notRegistered {
                try service.unregister()
            }
        } catch {
            Log.lifecycle.error("Login item update failed: \(error.localizedDescription, privacy: .public)")
        }
        return state
    }

    /// Opens the Login Items pane so the user can approve a blocked item.
    func openSystemSettings() {
        SMAppService.openSystemSettingsLoginItems()
    }
}
