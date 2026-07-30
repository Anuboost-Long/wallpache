import AVKit
import SwiftUI

/// Plays a wallpaper in a window before the user commits it to a display.
///
/// It reuses `VideoLoopPlayer`, so the preview loops exactly the way the real
/// wallpaper will.
struct WallpaperPreviewView: View {
    let record: WallpaperRecord
    let videoURL: URL
    let thumbnailURL: URL?
    let onApply: () -> Void

    @Environment(\.dismiss) private var dismiss
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var loopPlayer: VideoLoopPlayer?

    var body: some View {
        VStack(spacing: 12) {
            preview
                .frame(width: 640, height: 360)
                .background(Color.black, in: RoundedRectangle(cornerRadius: 10))

            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(record.name)
                        .font(.headline)
                    Text(subtitle)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Spacer()

                Button("Close") { dismiss() }
                    .keyboardShortcut(.cancelAction)
                Button("Apply to All Displays") {
                    onApply()
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(16)
        .onDisappear {
            loopPlayer?.stop()
            loopPlayer = nil
        }
        .task {
            // Reduced Motion means the preview waits for an explicit start.
            if !reduceMotion { startPreview() }
        }
    }

    @ViewBuilder
    private var preview: some View {
        if let loopPlayer {
            VideoPlayer(player: loopPlayer.player)
                .clipShape(RoundedRectangle(cornerRadius: 10))
        } else {
            ZStack {
                // 640 points, so twice that on a 2x display.
                WallpaperThumbnail(url: thumbnailURL, cornerRadius: 10, maximumPixelSize: 1280)
                Button {
                    startPreview()
                } label: {
                    Label("Play Preview", systemImage: "play.circle.fill")
                        .font(.title3)
                        .padding(10)
                }
                .buttonStyle(.borderedProminent)
            }
        }
    }

    private var subtitle: String {
        [record.durationDescription, record.resolutionDescription]
            .compactMap { $0 }
            .joined(separator: " · ")
    }

    private func startPreview() {
        guard loopPlayer == nil else { return }
        let player = VideoLoopPlayer(url: videoURL, isMuted: true, rate: 1.0)
        player.play()
        loopPlayer = player
    }
}
