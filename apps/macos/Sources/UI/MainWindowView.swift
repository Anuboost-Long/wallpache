import SwiftUI

/// The app's only window. Library, per-display setup, and settings live here so
/// the menu bar can stay small.
struct MainWindowView: View {
    @EnvironmentObject private var coordinator: WallpaperCoordinator

    var body: some View {
        VStack(spacing: 0) {
            BrandHeader()

            TabView {
                LibraryView()
                    .tabItem { Label("Library", systemImage: "square.grid.2x2") }

                DisplayConfigurationView()
                    .tabItem { Label("Displays", systemImage: "display") }

                SettingsView()
                    .tabItem { Label("Settings", systemImage: "gearshape") }
            }
        }
        .frame(minWidth: 720, minHeight: 480)
        .alert(
            "Wallpache",
            isPresented: Binding(
                get: { coordinator.alertMessage != nil },
                set: { if !$0 { coordinator.alertMessage = nil } }
            )
        ) {
            Button("OK", role: .cancel) { coordinator.alertMessage = nil }
        } message: {
            Text(coordinator.alertMessage ?? "")
        }
    }
}
