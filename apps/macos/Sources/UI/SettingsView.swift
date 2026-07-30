import SwiftUI

/// Energy behaviour, startup, storage, and privacy information.
struct SettingsView: View {
    @EnvironmentObject private var coordinator: WallpaperCoordinator
    @State private var isConfirmingDelete = false

    var body: some View {
        Form {
            Section("Startup") {
                Toggle("Launch Wallpache at login", isOn: launchAtLoginBinding)

                if coordinator.loginItemState == .requiresApproval {
                    LabeledContent("Approval needed") {
                        Button("Open System Settings…") {
                            coordinator.openLoginItemSettings()
                        }
                    }
                }
                if coordinator.loginItemState == .unavailable {
                    Text("Launch at login is unavailable until the app is moved to the Applications folder.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }

            Section {
                Toggle("Pause in Low Power Mode", isOn: $coordinator.energyPreferences.pauseInLowPowerMode)
                Toggle("Pause when the Mac gets hot", isOn: $coordinator.energyPreferences.pauseOnHighThermalState)
                Toggle("Pause while the screen is locked", isOn: $coordinator.energyPreferences.pauseWhenScreenLocked)
            } header: {
                Text("Energy Saving")
            } footer: {
                Text("A video wallpaper uses more power than a still image. Higher resolutions and frame rates use more; a 1080p clip is usually a good balance.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section {
                LabeledContent("Imported videos") {
                    Button("Show in Finder") { coordinator.revealStorageInFinder() }
                }
                Button("Delete All Imported Videos…", role: .destructive) {
                    isConfirmingDelete = true
                }
            } header: {
                Text("Storage")
            } footer: {
                Text("Imported videos are copied into Wallpache's own folder so they keep working if the original is moved or deleted.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section("Privacy") {
                Text("Wallpache works entirely on this Mac. Your videos are never uploaded, no account is required, and nothing is collected about the files you use.")
                    .font(.callout)
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
        .confirmationDialog(
            "Delete all imported videos?",
            isPresented: $isConfirmingDelete
        ) {
            Button("Delete All", role: .destructive) { coordinator.deleteAllImportedFiles() }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("This removes every video from Wallpache's library and stops all live wallpapers. The original files on your Mac are not touched.")
        }
    }

    private var launchAtLoginBinding: Binding<Bool> {
        Binding(
            get: { coordinator.loginItemState.isEnabled },
            set: { coordinator.setLaunchAtLogin($0) }
        )
    }
}
