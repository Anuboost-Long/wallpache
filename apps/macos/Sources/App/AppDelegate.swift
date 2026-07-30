import AppKit
import SwiftUI
import os

/// Composition root and lifecycle owner.
///
/// The coordinator is built here so its dependencies are wired in exactly one
/// place, and so the wallpaper is restored only after the app has fully
/// launched.
final class AppDelegate: NSObject, NSApplicationDelegate {
    private(set) lazy var coordinator: WallpaperCoordinator = Self.makeCoordinator()

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Menu-bar utility: no Dock icon, no menu bar takeover.
        NSApp.setActivationPolicy(.accessory)
        coordinator.start()
    }

    func applicationWillTerminate(_ notification: Notification) {
        coordinator.shutdown()
    }

    func applicationSupportsSecureRestorableState(_ app: NSApplication) -> Bool {
        true
    }

    // MARK: - Private

    private static func makeCoordinator() -> WallpaperCoordinator {
        let storage: WallpaperStorage
        do {
            storage = try WallpaperStorage.makeDefault()
        } catch {
            // Without storage the library cannot work, but the app should still
            // launch and explain itself rather than crash on start.
            Log.lifecycle.error("Storage unavailable: \(error.localizedDescription, privacy: .public)")
            storage = WallpaperStorage(root: FileManager.default.temporaryDirectory
                .appendingPathComponent("Wallpache", isDirectory: true))
        }

        return WallpaperCoordinator(
            settingsStore: SettingsStore(),
            libraryService: WallpaperLibraryService(storage: storage),
            displayManager: DisplayManager(),
            workspaceMonitor: WorkspaceMonitor(),
            powerMonitor: PowerMonitor(),
            loginItemService: LoginItemService(),
            desktopPictureService: DesktopPictureService()
        )
    }
}
