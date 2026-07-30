import AVFoundation
import Foundation
import os

/// A seamlessly looping, muted-by-default video player.
///
/// `AVPlayerLooper` keeps the queue topped up, so nothing replaces the player
/// item on every loop. The player can rebuild itself from the source URL after
/// a failure or a stall, which is what makes the wallpaper survive sleep/wake
/// cycles and transient decoder errors.
@MainActor
final class VideoLoopPlayer {
    /// Called when playback fails in a way the player could not repair itself.
    var onUnrecoverableFailure: ((Error?) -> Void)?

    let player = AVQueuePlayer()
    private(set) var url: URL

    private var looper: AVPlayerLooper?
    private var statusObservation: NSKeyValueObservation?
    private var healthObservation: NSKeyValueObservation?
    private var notificationObservers: [NSObjectProtocol] = []
    private var rebuildAttempts = 0
    private var isPlaying = false
    private var rate: Float = 1.0

    private static let maximumRebuildAttempts = 3

    init(url: URL, isMuted: Bool, rate: Float) {
        self.url = url
        self.rate = rate

        player.isMuted = isMuted
        player.actionAtItemEnd = .advance
        // A wallpaper must never keep the display awake.
        player.preventsDisplaySleepDuringVideoPlayback = false

        observePlaybackHealth()
        buildLooper()
    }

    deinit {
        statusObservation?.invalidate()
        healthObservation?.invalidate()
        notificationObservers.forEach(NotificationCenter.default.removeObserver)
    }

    // MARK: - Transport

    func play() {
        isPlaying = true
        player.playImmediately(atRate: rate)
    }

    func pause() {
        isPlaying = false
        player.pause()
    }

    func stop() {
        pause()
        looper?.disableLooping()
        looper = nil
        player.removeAllItems()
        statusObservation?.invalidate()
        statusObservation = nil
        healthObservation?.invalidate()
        healthObservation = nil
        notificationObservers.forEach(NotificationCenter.default.removeObserver)
        notificationObservers.removeAll()
    }

    func setMuted(_ isMuted: Bool) {
        player.isMuted = isMuted
    }

    func setRate(_ newRate: Float) {
        rate = newRate
        if isPlaying {
            player.rate = newRate
        }
    }

    func replaceVideo(url newURL: URL) {
        url = newURL
        rebuildAttempts = 0
        rebuild()
    }

    /// Rebuilds the looper when the player is not in a healthy state. Safe to
    /// call after every wake, display change, or policy transition.
    func recoverIfNeeded() {
        let itemFailed = player.currentItem?.status == .failed
        let looperFailed = looper?.status == .failed
        let queueEmpty = player.items().isEmpty

        guard itemFailed || looperFailed || queueEmpty else {
            if isPlaying, player.rate == 0 {
                player.playImmediately(atRate: rate)
            }
            return
        }

        Log.playback.info("Recovering player for \(self.url.lastPathComponent, privacy: .public)")
        rebuild()
    }

    // MARK: - Private

    private func rebuild() {
        let wasPlaying = isPlaying

        looper?.disableLooping()
        looper = nil
        player.removeAllItems()
        buildLooper()

        if wasPlaying {
            play()
        }
    }

    private func buildLooper() {
        let asset = AVURLAsset(url: url)
        let item = AVPlayerItem(asset: asset)
        let looper = AVPlayerLooper(player: player, templateItem: item)
        self.looper = looper

        statusObservation?.invalidate()
        statusObservation = looper.observe(\.status, options: [.new]) { [weak self] looper, _ in
            MainActor.assumeIsolated {
                guard looper.status == .failed else { return }
                self?.handleFailure(looper.error)
            }
        }
    }

    /// Watches for the two situations worth reacting to, and for the evidence
    /// that playback is healthy again.
    private func observePlaybackHealth() {
        let center = NotificationCenter.default

        notificationObservers = [
            center.addObserver(forName: .AVPlayerItemFailedToPlayToEndTime, object: nil, queue: .main) { [weak self] notification in
                MainActor.assumeIsolated {
                    guard let self, self.owns(notification.object) else { return }
                    self.handleFailure(notification.userInfo?[AVPlayerItemFailedToPlayToEndTimeErrorKey] as? Error)
                }
            },
            // A stall is usually transient; nudge playback before rebuilding.
            center.addObserver(forName: .AVPlayerItemPlaybackStalled, object: nil, queue: .main) { [weak self] notification in
                MainActor.assumeIsolated {
                    guard let self, self.isPlaying, self.owns(notification.object) else { return }
                    Log.playback.info("Playback stalled for \(self.url.lastPathComponent, privacy: .public)")
                    self.player.playImmediately(atRate: self.rate)
                }
            }
        ]

        // Reaching `.playing` proves the current build works, so the rebuild
        // budget resets and a long session cannot exhaust it.
        healthObservation = player.observe(\.timeControlStatus, options: [.new]) { [weak self] player, _ in
            MainActor.assumeIsolated {
                guard player.timeControlStatus == .playing else { return }
                self?.rebuildAttempts = 0
            }
        }
    }

    /// Looper items are copies of the template, so notifications are matched
    /// against the queue this player actually owns.
    private func owns(_ object: Any?) -> Bool {
        guard let item = object as? AVPlayerItem else { return false }
        return player.items().contains(item)
    }

    private func handleFailure(_ error: Error?) {
        guard rebuildAttempts < Self.maximumRebuildAttempts else {
            Log.playback.error("Giving up on \(self.url.lastPathComponent, privacy: .public) after \(self.rebuildAttempts) rebuilds")
            onUnrecoverableFailure?(error)
            return
        }

        rebuildAttempts += 1
        Log.playback.error("Playback problem (attempt \(self.rebuildAttempts, privacy: .public)): \(error?.localizedDescription ?? "unknown", privacy: .public)")
        rebuild()
    }
}
