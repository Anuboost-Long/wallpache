import SwiftUI

/// The menu-bar panel: status, transport, and the few actions worth reaching
/// without opening a window.
struct MenuBarView: View {
    @EnvironmentObject private var coordinator: WallpaperCoordinator
    @Environment(\.openWindow) private var openWindow

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header

            if coordinator.library.isEmpty {
                emptyState
            } else {
                quickApply
                transportControls
            }

            Divider()
            actions
        }
        .padding(14)
        .frame(width: 300)
    }

    // MARK: - Sections

    private var header: some View {
        HStack(spacing: 10) {
            BrandMarkView(size: 30)

            VStack(alignment: .leading, spacing: 2) {
                Text("Wallpache")
                    .font(.headline)
                Text(coordinator.statusMessage)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }
        }
        .accessibilityElement(children: .combine)
    }

    private var emptyState: some View {
        Text("Import a video to use it as your live wallpaper.")
            .font(.callout)
            .foregroundStyle(.secondary)
            .fixedSize(horizontal: false, vertical: true)
    }

    private var quickApply: some View {
        Menu {
            ForEach(coordinator.library) { record in
                Button(record.name) { coordinator.applyToAllDisplays(record) }
            }
        } label: {
            Label("Apply to All Displays", systemImage: "rectangle.on.rectangle")
        }
        .menuStyle(.borderlessButton)
    }

    private var transportControls: some View {
        HStack(spacing: 8) {
            Button {
                coordinator.setPaused(!coordinator.isUserPaused)
            } label: {
                Label(
                    coordinator.isUserPaused ? "Resume" : "Pause",
                    systemImage: coordinator.isUserPaused ? "play.fill" : "pause.fill"
                )
                .frame(maxWidth: .infinity)
            }
            .disabled(!coordinator.isWallpaperEnabled)

            Button {
                coordinator.stopAll()
            } label: {
                Label("Stop", systemImage: "stop.fill")
                    .frame(maxWidth: .infinity)
            }
            .disabled(!coordinator.isWallpaperEnabled)
        }
        .controlSize(.large)
    }

    private var actions: some View {
        VStack(alignment: .leading, spacing: 6) {
            Button {
                Task { await coordinator.presentImportPanel() }
            } label: {
                Label("Import Video…", systemImage: "square.and.arrow.down")
            }
            .disabled(coordinator.isImporting)

            Button {
                openWindow(id: MainWindow.id)
                MainWindow.activate()
            } label: {
                Label("Wallpaper Library & Settings…", systemImage: "slider.horizontal.3")
            }

            Toggle(isOn: launchAtLoginBinding) {
                Label("Launch at Login", systemImage: "power")
            }
            .toggleStyle(.checkbox)

            if coordinator.loginItemState == .requiresApproval {
                Button("Approve in System Settings…") {
                    coordinator.openLoginItemSettings()
                }
                .font(.caption)
            }

            Divider()

            Button {
                NSApp.terminate(nil)
            } label: {
                Label("Quit Wallpache", systemImage: "xmark.circle")
            }
            .keyboardShortcut("q")
        }
        .buttonStyle(.plain)
    }

    // MARK: - Bindings

    private var launchAtLoginBinding: Binding<Bool> {
        Binding(
            get: { coordinator.loginItemState.isEnabled },
            set: { coordinator.setLaunchAtLogin($0) }
        )
    }
}
