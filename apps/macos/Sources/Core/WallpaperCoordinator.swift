import AppKit
import Combine
import os

/// Central orchestrator. It owns the persisted configuration and keeps the live
/// sessions in sync with it.
///
/// Every mutation follows the same shape: change `configuration`, persist, then
/// call `synchronizeSessions()`. Because reconciliation is the only code that
/// creates or destroys sessions, applying the same wallpaper twice can never
/// produce duplicate windows, and a display that disappears cannot leave a
/// stale player behind.
@MainActor
final class WallpaperCoordinator: ObservableObject {
    // MARK: - Published state

    @Published private(set) var library: [WallpaperRecord] = []
    @Published private(set) var displays: [DisplayDescriptor] = []
    @Published private(set) var displayConfigurations: [DisplayWallpaperConfiguration] = []
    @Published private(set) var isWallpaperEnabled = false
    @Published private(set) var isUserPaused = false
    @Published private(set) var statusMessage = "No wallpaper running"
    @Published private(set) var isImporting = false
    @Published private(set) var loginItemState: LoginItemService.State = .disabled
    @Published var alertMessage: String?

    @Published var energyPreferences = EnergyPreferences() {
        didSet {
            guard energyPreferences != oldValue else { return }
            policy.preferences = energyPreferences
            configuration.energyPreferences = energyPreferences
            persist()
        }
    }

    // MARK: - Dependencies

    private let settingsStore: SettingsStore
    private let libraryService: WallpaperLibraryService
    private let displayManager: DisplayManager
    private let workspaceMonitor: WorkspaceMonitor
    private let powerMonitor: PowerMonitor
    private let policy: PlaybackPolicyController
    private let loginItemService: LoginItemService
    private let desktopPictureService: DesktopPictureService

    private var configuration: AppConfiguration
    private var sessions: [CGDirectDisplayID: WallpaperSession] = [:]
    private var hasStarted = false

    /// What was last handed to the system per display, so reconciliation can
    /// skip redundant desktop picture updates.
    private struct AppliedDesktopPicture: Equatable {
        let url: URL
        let scalingMode: ScalingMode
    }

    private var appliedDesktopPictures: [CGDirectDisplayID: AppliedDesktopPicture] = [:]

    var storage: WallpaperStorage { libraryService.storage }

    init(
        settingsStore: SettingsStore,
        libraryService: WallpaperLibraryService,
        displayManager: DisplayManager,
        workspaceMonitor: WorkspaceMonitor,
        powerMonitor: PowerMonitor,
        loginItemService: LoginItemService,
        desktopPictureService: DesktopPictureService
    ) {
        self.settingsStore = settingsStore
        self.libraryService = libraryService
        self.displayManager = displayManager
        self.workspaceMonitor = workspaceMonitor
        self.powerMonitor = powerMonitor
        self.loginItemService = loginItemService
        self.desktopPictureService = desktopPictureService
        self.policy = PlaybackPolicyController(power: powerMonitor)
        self.configuration = settingsStore.load()
    }

    // MARK: - Lifecycle

    /// Restores the last configuration and starts observing the system. Safe to
    /// call once; later calls are ignored.
    func start() {
        guard !hasStarted else { return }
        hasStarted = true

        library = configuration.library
        energyPreferences = configuration.energyPreferences
        policy.preferences = configuration.energyPreferences
        loginItemState = loginItemService.state

        connectMonitors()
        displayManager.start()
        workspaceMonitor.start()
        powerMonitor.start()

        displays = displayManager.displays
        pruneMissingWallpapers()
        // Clears files left behind by a crash between copying and saving.
        libraryService.removeOrphanedFiles(keeping: configuration.library)

        policy.isWallpaperEnabled = configuration.isWallpaperEnabled
        isWallpaperEnabled = configuration.isWallpaperEnabled

        synchronizeSessions()
        backfillStills()
        Log.lifecycle.info("Coordinator started with \(self.library.count, privacy: .public) wallpapers")
    }

    /// Generates full-resolution stills for entries imported before stills
    /// existed. Runs off the main actor after launch: it decodes a frame per
    /// wallpaper, which is far too slow to block startup on.
    private func backfillStills() {
        let pending = configuration.library.filter { $0.stillRelativePath == nil }
        guard !pending.isEmpty else { return }

        Task { [libraryService] in
            let updated = await libraryService.backfillStills(for: pending)
            guard !updated.isEmpty else { return }

            for record in updated {
                guard let index = configuration.library.firstIndex(where: { $0.id == record.id }) else { continue }
                configuration.library[index].stillRelativePath = record.stillRelativePath
            }

            library = configuration.library
            persist()

            // The desktop picture may have been set from the low-resolution
            // thumbnail before the still existed, so let it be re-applied.
            appliedDesktopPictures.removeAll()
            synchronizeSessions()
            Log.library.info("Backfilled \(updated.count, privacy: .public) stills")
        }
    }

    /// Stops playback and observation. The wallpaper windows close, so the
    /// normal macOS wallpaper is visible again.
    func shutdown() {
        sessions.values.forEach { $0.stop() }
        sessions.removeAll()
        displayManager.stop()
        workspaceMonitor.stop()
        powerMonitor.stop()
        persist()
    }

    // MARK: - Import

    /// Shows the open panel and imports the chosen files.
    func presentImportPanel() async {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowedContentTypes = WallpaperLibraryService.supportedContentTypes
        panel.prompt = "Import"
        panel.message = "Choose an .mp4, .mov, or .m4v video to use as a live wallpaper."

        // An accessory app is not active, so the panel needs to be brought up
        // explicitly or it can open behind other windows.
        NSApp.activate(ignoringOtherApps: true)
        guard panel.runModal() == .OK else { return }

        await importVideos(at: panel.urls)
    }

    /// Imports files, e.g. from the open panel or a drag and drop.
    func importVideos(at urls: [URL]) async {
        guard !urls.isEmpty else { return }

        isImporting = true
        defer { isImporting = false }

        var failures: [String] = []
        for url in urls {
            do {
                let record = try await libraryService.importVideo(at: url, existing: configuration.library)
                if !configuration.library.contains(where: { $0.id == record.id }) {
                    configuration.library.append(record)
                }
            } catch {
                failures.append(Self.describe(error))
                Log.library.error("Import failed: \(error.localizedDescription, privacy: .public)")
            }
        }

        library = configuration.library
        persist()

        if !failures.isEmpty {
            alertMessage = failures.joined(separator: "\n")
        }
    }

    /// Renames one library entry. Only the display name changes: stored files
    /// are named by UUID, so nothing moves on disk and no session is disturbed.
    /// An empty or unchanged name is ignored rather than rejected with an error.
    func rename(_ record: WallpaperRecord, to newName: String) {
        let trimmed = newName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, trimmed != record.name,
              let index = configuration.library.firstIndex(where: { $0.id == record.id }) else { return }

        configuration.library[index].name = trimmed
        library = configuration.library
        persist()
        Log.library.info("Renamed a wallpaper")
    }

    func delete(_ record: WallpaperRecord) {
        configuration.library.removeAll { $0.id == record.id }
        configuration.removeAssignments(ofWallpaper: record.id)
        libraryService.delete(record)

        library = configuration.library
        persist()
        synchronizeSessions()
    }

    /// Removes every imported video and stops all wallpapers.
    func deleteAllImportedFiles() {
        configuration.library.forEach(libraryService.delete)
        configuration.library.removeAll()
        configuration.displayConfigurations.removeAll()
        configuration.isWallpaperEnabled = false

        library = []
        persist()
        setWallpaperEnabled(false)
    }

    func revealStorageInFinder() {
        NSWorkspace.shared.activateFileViewerSelecting([storage.videosDirectory])
    }

    // MARK: - Applying wallpapers

    func apply(_ record: WallpaperRecord, to displayID: CGDirectDisplayID) {
        var displayConfig = configuration.configuration(forDisplay: displayID)
            ?? DisplayWallpaperConfiguration(displayID: displayID)
        displayConfig.wallpaperID = record.id
        configuration.setConfiguration(displayConfig)

        activateAndSynchronize()
    }

    func applyToAllDisplays(_ record: WallpaperRecord) {
        for display in displays {
            var displayConfig = configuration.configuration(forDisplay: display.id)
                ?? DisplayWallpaperConfiguration(displayID: display.id)
            displayConfig.wallpaperID = record.id
            configuration.setConfiguration(displayConfig)
        }

        activateAndSynchronize()
    }

    /// Removes the wallpaper from one display without touching the others.
    func clearWallpaper(from displayID: CGDirectDisplayID) {
        guard var displayConfig = configuration.configuration(forDisplay: displayID) else { return }
        displayConfig.wallpaperID = nil
        configuration.setConfiguration(displayConfig)

        // Nothing left to show anywhere means the wallpaper is simply off.
        if !configuration.displayConfigurations.contains(where: { $0.wallpaperID != nil }) {
            configuration.isWallpaperEnabled = false
        }

        persist()
        syncEnabledState()
        synchronizeSessions()
    }

    func stopAll() {
        configuration.isWallpaperEnabled = false
        persist()
        setWallpaperEnabled(false)
    }

    func setPaused(_ paused: Bool) {
        isUserPaused = paused
        policy.isUserPaused = paused
        updateStatus()
    }

    // MARK: - Per-display settings

    func setScalingMode(_ mode: ScalingMode, for displayID: CGDirectDisplayID) {
        updateDisplayConfiguration(displayID) { $0.scalingMode = mode }
    }

    func setPlaybackRate(_ rate: Float, for displayID: CGDirectDisplayID) {
        updateDisplayConfiguration(displayID) { $0.playbackRate = rate }
    }

    func setMuted(_ isMuted: Bool, for displayID: CGDirectDisplayID) {
        updateDisplayConfiguration(displayID) { $0.isMuted = isMuted }
    }

    /// Mirrors the wallpaper's still frame onto the macOS desktop picture, so the
    /// display keeps a matching background when the app is not running.
    func setDesktopPicture(_ enabled: Bool, for displayID: CGDirectDisplayID) {
        updateDisplayConfiguration(displayID) { $0.setsDesktopPicture = enabled }
    }

    func displayConfiguration(for displayID: CGDirectDisplayID) -> DisplayWallpaperConfiguration {
        configuration.configuration(forDisplay: displayID)
            ?? DisplayWallpaperConfiguration(displayID: displayID)
    }

    func wallpaper(withID id: UUID?) -> WallpaperRecord? {
        configuration.wallpaper(withID: id)
    }

    // MARK: - Login item

    func setLaunchAtLogin(_ enabled: Bool) {
        loginItemState = loginItemService.setEnabled(enabled)
    }

    func openLoginItemSettings() {
        loginItemService.openSystemSettings()
    }

    // MARK: - Session reconciliation

    /// Makes the running sessions match `configuration` exactly.
    private func synchronizeSessions() {
        let screensByID = Dictionary(
            NSScreen.screens.compactMap { screen in screen.displayID.map { ($0, screen) } },
            uniquingKeysWith: { first, _ in first }
        )

        // 1. Displays that went away, or a fully disabled wallpaper.
        for (displayID, session) in sessions
        where screensByID[displayID] == nil || !configuration.isWallpaperEnabled {
            session.stop()
            sessions[displayID] = nil
        }

        if configuration.isWallpaperEnabled {
            for (displayID, screen) in screensByID {
                synchronizeSession(displayID: displayID, screen: screen)
            }
        }

        // Deliberately outside the enabled check: the still image is what the
        // user sees once playback stops, so it must survive a stopped wallpaper.
        synchronizeDesktopPictures(screensByID: screensByID)

        displayConfigurations = displays.map { displayConfiguration(for: $0.id) }
        updatePlayback()
    }

    private func synchronizeSession(displayID: CGDirectDisplayID, screen: NSScreen) {
        let displayConfig = displayConfiguration(for: displayID)

        guard let record = configuration.wallpaper(withID: displayConfig.wallpaperID),
              libraryService.fileExists(for: record) else {
            // No assignment, or the file vanished: fall back to the plain
            // macOS wallpaper for this display only.
            sessions.removeValue(forKey: displayID)?.stop()
            return
        }

        let videoURL = storage.videoURL(for: record)

        if let session = sessions[displayID] {
            session.move(to: screen)
            if session.wallpaperID != record.id {
                session.replaceVideo(wallpaperID: record.id, url: videoURL, videoPixelSize: record.pixelSize)
            }
            session.update(configuration: displayConfig, videoPixelSize: record.pixelSize)
        } else {
            let session = WallpaperSession(
                screen: screen,
                displayID: displayID,
                wallpaperID: record.id,
                videoURL: videoURL,
                videoPixelSize: record.pixelSize,
                configuration: displayConfig
            )
            session.onPlaybackFailure = { [weak self] failedDisplayID, error in
                self?.handlePlaybackFailure(displayID: failedDisplayID, error: error)
            }
            sessions[displayID] = session
            Log.playback.info("Started session on display \(displayID, privacy: .public)")
        }
    }

    // MARK: - Desktop picture

    /// Makes each display's system wallpaper match its assigned still frame, and
    /// puts the user's own picture back where the option is off.
    private func synchronizeDesktopPictures(screensByID: [CGDirectDisplayID: NSScreen]) {
        for (displayID, screen) in screensByID {
            let displayConfig = displayConfiguration(for: displayID)

            // The full-resolution still, not the grid thumbnail: this image is
            // about to fill a whole display.
            guard displayConfig.setsDesktopPicture,
                  let record = configuration.wallpaper(withID: displayConfig.wallpaperID),
                  let stillURL = storage.stillURL(for: record) ?? storage.thumbnailURL(for: record),
                  FileManager.default.fileExists(atPath: stillURL.path) else {
                restoreDesktopPicture(displayID: displayID, screen: screen)
                continue
            }

            // Re-setting the same picture on every reconciliation would hit the
            // system on each wake and display change for no reason.
            let desired = AppliedDesktopPicture(url: stillURL, scalingMode: displayConfig.scalingMode)
            guard appliedDesktopPictures[displayID] != desired else { continue }

            rememberCurrentPicture(displayID: displayID, screen: screen)

            if desktopPictureService.setPicture(stillURL, on: screen, scalingMode: displayConfig.scalingMode) {
                appliedDesktopPictures[displayID] = desired
                Log.displays.info("Desktop picture set on display \(displayID, privacy: .public)")
            }
        }
    }

    /// Stores the wallpaper the user had, once per display, so it can be put
    /// back later. Our own stills are never recorded as the previous picture.
    private func rememberCurrentPicture(displayID: CGDirectDisplayID, screen: NSScreen) {
        let key = String(displayID)
        guard configuration.previousDesktopPictures[key] == nil,
              let current = desktopPictureService.currentPictureURL(for: screen),
              !current.path.hasPrefix(storage.thumbnailsDirectory.path) else { return }

        configuration.previousDesktopPictures[key] = current.absoluteString
        persist()
    }

    private func restoreDesktopPicture(displayID: CGDirectDisplayID, screen: NSScreen) {
        appliedDesktopPictures[displayID] = nil

        let key = String(displayID)
        guard let stored = configuration.previousDesktopPictures.removeValue(forKey: key) else { return }
        persist()

        // A picture that has since been deleted is simply left alone rather than
        // replaced with an error.
        guard let url = URL(string: stored),
              FileManager.default.fileExists(atPath: url.path) else { return }

        desktopPictureService.setPicture(url, on: screen, scalingMode: .fill)
        Log.displays.info("Desktop picture restored on display \(displayID, privacy: .public)")
    }

    private func updatePlayback() {
        let shouldPlay = policy.shouldPlay
        for session in sessions.values {
            if shouldPlay {
                session.play()
            } else {
                session.pause()
            }
        }

        updateStatus()
    }

    /// Re-asserts window placement and repairs players. Runs after wake and
    /// after display reconfiguration, where either half can be disturbed.
    private func recoverSessions() {
        let shouldPlay = policy.shouldPlay
        sessions.values.forEach { $0.recover(shouldPlay: shouldPlay) }
    }

    // MARK: - System events

    private func connectMonitors() {
        displayManager.onDisplaysChanged = { [weak self] updated in
            guard let self else { return }
            self.displays = updated
            self.synchronizeSessions()
            self.recoverSessions()
        }

        workspaceMonitor.onWillSleep = { [weak self] in
            self?.policy.isSystemAsleep = true
        }

        workspaceMonitor.onDidWake = { [weak self] in
            guard let self else { return }
            self.policy.isSystemAsleep = false
            // Screens are revalidated first: waking on a dock can change the
            // display set without a separate notification.
            self.displayManager.refresh()
            self.displays = self.displayManager.displays
            self.synchronizeSessions()
            self.recoverSessions()
        }

        workspaceMonitor.onScreenLockChanged = { [weak self] isLocked in
            self?.policy.isScreenLocked = isLocked
        }

        powerMonitor.onChange = { [weak self] in
            self?.policy.reevaluate()
        }

        policy.onDecisionChanged = { [weak self] _ in
            self?.updatePlayback()
        }
    }

    private func handlePlaybackFailure(displayID: CGDirectDisplayID, error: Error?) {
        let record = configuration.wallpaper(withID: displayConfiguration(for: displayID).wallpaperID)
        Log.playback.error("Unrecoverable playback failure on display \(displayID, privacy: .public)")

        sessions.removeValue(forKey: displayID)?.stop()
        clearWallpaper(from: displayID)

        let name = record?.name ?? "This wallpaper"
        alertMessage = "\(name) stopped playing and was removed from that display. \(error?.localizedDescription ?? "")"
            .trimmingCharacters(in: .whitespaces)
    }

    // MARK: - Helpers

    private func activateAndSynchronize() {
        configuration.isWallpaperEnabled = true
        isUserPaused = false
        policy.isUserPaused = false
        persist()
        syncEnabledState()
        synchronizeSessions()
    }

    private func setWallpaperEnabled(_ enabled: Bool) {
        configuration.isWallpaperEnabled = enabled
        syncEnabledState()
        synchronizeSessions()
    }

    private func syncEnabledState() {
        isWallpaperEnabled = configuration.isWallpaperEnabled
        policy.isWallpaperEnabled = configuration.isWallpaperEnabled
    }

    private func updateDisplayConfiguration(
        _ displayID: CGDirectDisplayID,
        _ mutate: (inout DisplayWallpaperConfiguration) -> Void
    ) {
        var displayConfig = displayConfiguration(for: displayID)
        mutate(&displayConfig)
        configuration.setConfiguration(displayConfig)
        persist()
        synchronizeSessions()
    }

    /// Drops library entries whose file disappeared while the app was closed.
    private func pruneMissingWallpapers() {
        let missing = configuration.library.filter { !libraryService.fileExists(for: $0) }
        guard !missing.isEmpty else { return }

        for record in missing {
            configuration.library.removeAll { $0.id == record.id }
            configuration.removeAssignments(ofWallpaper: record.id)
        }

        library = configuration.library
        persist()
        Log.library.info("Removed \(missing.count, privacy: .public) missing wallpapers")
        alertMessage = missing.count == 1
            ? "“\(missing[0].name)” is no longer available and was removed from your library."
            : "\(missing.count) wallpapers are no longer available and were removed from your library."
    }

    private func updateStatus() {
        if let reason = policy.suspensionReason {
            statusMessage = reason.description
        } else {
            let count = sessions.count
            statusMessage = switch count {
            case 0: "No wallpaper running"
            case 1: "Playing on 1 display"
            default: "Playing on \(count) displays"
            }
        }
    }

    /// Combines the message and its suggestion so the user sees what to do next.
    private static func describe(_ error: Error) -> String {
        let description = error.localizedDescription
        guard let suggestion = (error as? LocalizedError)?.recoverySuggestion else { return description }
        return "\(description) \(suggestion)"
    }

    private func persist() {
        settingsStore.save(configuration)
    }
}
