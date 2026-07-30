import Foundation
import Testing

@testable import Wallpache

struct AppConfigurationTests {
    private func makeRecord(name: String = "Clip") -> WallpaperRecord {
        WallpaperRecord(
            id: UUID(),
            name: name,
            relativePath: "Wallpapers/\(UUID().uuidString).mp4",
            thumbnailRelativePath: nil,
            dateImported: Date(),
            duration: 12,
            width: 1920,
            height: 1080,
            fileSize: 1024
        )
    }

    @Test func displayAssignmentsAreMatchedByStableDisplayID() {
        var configuration = AppConfiguration()
        configuration.setConfiguration(DisplayWallpaperConfiguration(displayID: 42, scalingMode: .fit))

        #expect(configuration.configuration(forDisplay: 42)?.scalingMode == .fit)
        #expect(configuration.configuration(forDisplay: 7) == nil)
    }

    @Test func reassigningADisplayReplacesInsteadOfDuplicating() {
        var configuration = AppConfiguration()
        configuration.setConfiguration(DisplayWallpaperConfiguration(displayID: 1, scalingMode: .fill))
        configuration.setConfiguration(DisplayWallpaperConfiguration(displayID: 1, scalingMode: .stretch))

        #expect(configuration.displayConfigurations.count == 1)
        #expect(configuration.configuration(forDisplay: 1)?.scalingMode == .stretch)
    }

    @Test func removingAWallpaperClearsOnlyItsAssignments() {
        let kept = makeRecord(name: "Kept")
        let removed = makeRecord(name: "Removed")

        var configuration = AppConfiguration()
        configuration.library = [kept, removed]
        configuration.setConfiguration(DisplayWallpaperConfiguration(displayID: 1, wallpaperID: removed.id))
        configuration.setConfiguration(DisplayWallpaperConfiguration(displayID: 2, wallpaperID: kept.id))

        configuration.removeAssignments(ofWallpaper: removed.id)

        #expect(configuration.configuration(forDisplay: 1)?.wallpaperID == nil)
        #expect(configuration.configuration(forDisplay: 2)?.wallpaperID == kept.id)
    }

    @Test func wallpaperLookupToleratesMissingIdentifiers() {
        var configuration = AppConfiguration()
        let record = makeRecord()
        configuration.library = [record]

        #expect(configuration.wallpaper(withID: record.id)?.name == record.name)
        #expect(configuration.wallpaper(withID: nil) == nil)
        #expect(configuration.wallpaper(withID: UUID()) == nil)
    }

    // MARK: - Persistence

    @Test func configurationSurvivesAnEncodeDecodeRoundTrip() throws {
        var original = AppConfiguration()
        original.library = [makeRecord(name: "Aurora")]
        original.isWallpaperEnabled = true
        original.energyPreferences.pauseInLowPowerMode = false
        original.setConfiguration(
            DisplayWallpaperConfiguration(
                displayID: 99,
                wallpaperID: original.library[0].id,
                scalingMode: .center,
                playbackRate: 0.5,
                isMuted: false
            )
        )

        let data = try JSONEncoder().encode(original)
        let decoded = try JSONDecoder().decode(AppConfiguration.self, from: data)

        #expect(decoded == original)
    }

    /// A settings file written by an earlier build must not wipe the library.
    @Test func decodingToleratesMissingFields() throws {
        let json = Data(#"{"library":[],"isWallpaperEnabled":true}"#.utf8)
        let decoded = try JSONDecoder().decode(AppConfiguration.self, from: json)

        #expect(decoded.isWallpaperEnabled)
        #expect(decoded.version == AppConfiguration.currentVersion)
        #expect(decoded.energyPreferences == EnergyPreferences())
        #expect(decoded.previousDesktopPictures.isEmpty)
    }

    @Test func decodingADisplayConfigurationAppliesDefaults() throws {
        let json = Data(#"{"displayID":5}"#.utf8)
        let decoded = try JSONDecoder().decode(DisplayWallpaperConfiguration.self, from: json)

        #expect(decoded.displayID == 5)
        #expect(decoded.scalingMode == .fill)
        #expect(decoded.playbackRate == 1.0)
        #expect(decoded.isMuted)
        // Off by default: the app must not touch the system wallpaper unasked.
        #expect(!decoded.setsDesktopPicture)
    }

    @Test func desktopPictureSettingsSurviveARoundTrip() throws {
        var configuration = AppConfiguration()
        configuration.setConfiguration(
            DisplayWallpaperConfiguration(displayID: 3, setsDesktopPicture: true)
        )
        configuration.previousDesktopPictures = ["3": "file:///Library/Desktop%20Pictures/Original.heic"]

        let data = try JSONEncoder().encode(configuration)
        let decoded = try JSONDecoder().decode(AppConfiguration.self, from: data)

        #expect(decoded.configuration(forDisplay: 3)?.setsDesktopPicture == true)
        #expect(decoded.previousDesktopPictures["3"] == "file:///Library/Desktop%20Pictures/Original.heic")
    }

    @Test func settingsStoreRoundTripsThroughUserDefaults() throws {
        let suiteName = "WallpacheTests.\(UUID().uuidString)"
        let defaults = try #require(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }

        let store = SettingsStore(defaults: defaults)
        var configuration = AppConfiguration()
        configuration.library = [makeRecord(name: "Saved")]
        store.save(configuration)

        #expect(SettingsStore(defaults: defaults).load().library.first?.name == "Saved")
    }

    @Test func settingsStoreFallsBackWhenStoredDataIsCorrupt() throws {
        let suiteName = "WallpacheTests.\(UUID().uuidString)"
        let defaults = try #require(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }

        defaults.set(Data("not json".utf8), forKey: SettingsStore.defaultKey)

        #expect(SettingsStore(defaults: defaults).load().library.isEmpty)
    }
}
