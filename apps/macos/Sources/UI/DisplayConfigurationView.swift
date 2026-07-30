import SwiftUI

/// Per-display wallpaper assignment and playback settings.
struct DisplayConfigurationView: View {
    @EnvironmentObject private var coordinator: WallpaperCoordinator

    var body: some View {
        Form {
            ForEach(coordinator.displays) { display in
                Section {
                    DisplayRow(display: display)
                } header: {
                    HStack {
                        Text(display.name)
                        if display.isPrimary {
                            Text("Main")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                        Spacer()
                        Text(display.resolutionDescription)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .formStyle(.grouped)
    }
}

private struct DisplayRow: View {
    @EnvironmentObject private var coordinator: WallpaperCoordinator
    let display: DisplayDescriptor

    var body: some View {
        Picker("Wallpaper", selection: wallpaperBinding) {
            Text("None").tag(UUID?.none)
            ForEach(coordinator.library) { record in
                Text(record.name).tag(UUID?.some(record.id))
            }
        }

        Picker("Scaling", selection: scalingBinding) {
            ForEach(ScalingMode.allCases) { mode in
                Text(mode.displayName).tag(mode)
            }
        }
        .disabled(!hasWallpaper)

        Picker("Speed", selection: rateBinding) {
            ForEach(DisplayWallpaperConfiguration.supportedRates, id: \.self) { rate in
                Text(String(format: "%.2g×", rate)).tag(rate)
            }
        }
        .disabled(!hasWallpaper)

        Toggle("Play audio", isOn: audioBinding)
            .disabled(!hasWallpaper)

        Toggle("Match desktop picture", isOn: desktopPictureBinding)
            .disabled(!hasWallpaper)
        Text("Sets a still frame from this video as the macOS wallpaper, so this display still matches when Wallpache is not running.")
            .font(.caption)
            .foregroundStyle(.secondary)

        if hasWallpaper {
            Button("Remove Wallpaper", role: .destructive) {
                coordinator.clearWallpaper(from: display.id)
            }
        }
    }

    // MARK: - State

    private var configuration: DisplayWallpaperConfiguration {
        coordinator.displayConfiguration(for: display.id)
    }

    private var hasWallpaper: Bool {
        configuration.wallpaperID != nil
    }

    private var wallpaperBinding: Binding<UUID?> {
        Binding(
            get: { configuration.wallpaperID },
            set: { newValue in
                guard let newValue, let record = coordinator.wallpaper(withID: newValue) else {
                    coordinator.clearWallpaper(from: display.id)
                    return
                }
                coordinator.apply(record, to: display.id)
            }
        )
    }

    private var scalingBinding: Binding<ScalingMode> {
        Binding(
            get: { configuration.scalingMode },
            set: { coordinator.setScalingMode($0, for: display.id) }
        )
    }

    private var rateBinding: Binding<Float> {
        Binding(
            get: { configuration.playbackRate },
            set: { coordinator.setPlaybackRate($0, for: display.id) }
        )
    }

    /// The stored value is "muted"; the switch reads better as "play audio".
    private var audioBinding: Binding<Bool> {
        Binding(
            get: { !configuration.isMuted },
            set: { coordinator.setMuted(!$0, for: display.id) }
        )
    }

    private var desktopPictureBinding: Binding<Bool> {
        Binding(
            get: { configuration.setsDesktopPicture },
            set: { coordinator.setDesktopPicture($0, for: display.id) }
        )
    }
}
