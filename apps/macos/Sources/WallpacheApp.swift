import SwiftUI

@main
struct WallpacheApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        MenuBarExtra {
            MenuBarView()
                .environmentObject(appDelegate.coordinator)
        } label: {
            MenuBarLabel()
        }
        .menuBarExtraStyle(.window)

        Window("Wallpache", id: MainWindow.id) {
            MainWindowView()
                .environmentObject(appDelegate.coordinator)
        }
        .windowResizability(.contentMinSize)
        .defaultSize(width: 820, height: 560)
    }
}

/// The status-bar icon.
///
/// It also carries the first-run behaviour: a menu-bar-only app is easy to miss,
/// so the library window is opened once, the first time Wallpache runs.
private struct MenuBarLabel: View {
    @Environment(\.openWindow) private var openWindow
    @AppStorage("hasOpenedLibraryOnFirstRun") private var hasOpenedOnFirstRun = false

    var body: some View {
        Image("MenuBarIcon")
            .accessibilityLabel("Wallpache")
            .task {
                guard !hasOpenedOnFirstRun else { return }
                hasOpenedOnFirstRun = true
                openWindow(id: MainWindow.id)
                MainWindow.activate()
            }
    }
}

/// Identifier and presentation helper for the single settings/library window.
enum MainWindow {
    static let id = "wallpache.main"

    /// An accessory app is not active, so the window has to be raised
    /// explicitly after `openWindow` or it can appear behind other apps.
    static func activate() {
        NSApp.activate(ignoringOtherApps: true)
    }
}
