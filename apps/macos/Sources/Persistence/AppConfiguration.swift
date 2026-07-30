import CoreGraphics
import Foundation

/// Energy rules the user can opt out of. Nothing here changes behaviour
/// silently: every rule maps to one switch in Settings.
nonisolated struct EnergyPreferences: Codable, Hashable, Sendable {
    var pauseInLowPowerMode: Bool = true
    var pauseOnHighThermalState: Bool = true
    var pauseWhenScreenLocked: Bool = true

    init(
        pauseInLowPowerMode: Bool = true,
        pauseOnHighThermalState: Bool = true,
        pauseWhenScreenLocked: Bool = true
    ) {
        self.pauseInLowPowerMode = pauseInLowPowerMode
        self.pauseOnHighThermalState = pauseOnHighThermalState
        self.pauseWhenScreenLocked = pauseWhenScreenLocked
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        pauseInLowPowerMode = try container.decodeIfPresent(Bool.self, forKey: .pauseInLowPowerMode) ?? true
        pauseOnHighThermalState = try container.decodeIfPresent(Bool.self, forKey: .pauseOnHighThermalState) ?? true
        pauseWhenScreenLocked = try container.decodeIfPresent(Bool.self, forKey: .pauseWhenScreenLocked) ?? true
    }
}

/// Everything the app restores after a relaunch. Transient objects
/// (`AVPlayer`, `NSScreen`, window references) are deliberately absent.
nonisolated struct AppConfiguration: Codable, Hashable, Sendable {
    static let currentVersion = 1

    var version: Int = AppConfiguration.currentVersion
    var library: [WallpaperRecord] = []
    var displayConfigurations: [DisplayWallpaperConfiguration] = []
    /// Whether wallpapers were running when the app last quit.
    var isWallpaperEnabled: Bool = false
    var energyPreferences: EnergyPreferences = EnergyPreferences()
    /// The desktop picture each display had before the app replaced it, keyed by
    /// display ID. Kept so switching the option off restores the user's own
    /// wallpaper instead of stranding them with a video frame.
    var previousDesktopPictures: [String: String] = [:]

    init() {}

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        version = try container.decodeIfPresent(Int.self, forKey: .version) ?? Self.currentVersion
        library = try container.decodeIfPresent([WallpaperRecord].self, forKey: .library) ?? []
        displayConfigurations = try container.decodeIfPresent(
            [DisplayWallpaperConfiguration].self,
            forKey: .displayConfigurations
        ) ?? []
        isWallpaperEnabled = try container.decodeIfPresent(Bool.self, forKey: .isWallpaperEnabled) ?? false
        energyPreferences = try container.decodeIfPresent(
            EnergyPreferences.self,
            forKey: .energyPreferences
        ) ?? EnergyPreferences()
        previousDesktopPictures = try container.decodeIfPresent(
            [String: String].self,
            forKey: .previousDesktopPictures
        ) ?? [:]
    }

    func wallpaper(withID id: UUID?) -> WallpaperRecord? {
        guard let id else { return nil }
        return library.first { $0.id == id }
    }

    func configuration(forDisplay displayID: CGDirectDisplayID) -> DisplayWallpaperConfiguration? {
        displayConfigurations.first { $0.displayID == displayID }
    }

    /// Inserts or replaces one display assignment, keeping the collection
    /// free of duplicates for the same display.
    mutating func setConfiguration(_ configuration: DisplayWallpaperConfiguration) {
        if let index = displayConfigurations.firstIndex(where: { $0.displayID == configuration.displayID }) {
            displayConfigurations[index] = configuration
        } else {
            displayConfigurations.append(configuration)
        }
    }

    /// Drops assignments that point at a wallpaper that is no longer in the
    /// library, so a deleted video cannot resurrect a broken session.
    mutating func removeAssignments(ofWallpaper id: UUID) {
        for index in displayConfigurations.indices where displayConfigurations[index].wallpaperID == id {
            displayConfigurations[index].wallpaperID = nil
        }
    }
}
