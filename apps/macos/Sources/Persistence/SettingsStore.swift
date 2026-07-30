import Foundation
import os

/// Loads and saves `AppConfiguration`. `UserDefaults` is enough for the amount
/// of state the MVP keeps; the JSON blob keeps migrations in one place.
nonisolated final class SettingsStore {
    static let defaultKey = "com.kimlongly.Wallpache.configuration"

    private let defaults: UserDefaults
    private let key: String
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    init(defaults: UserDefaults = .standard, key: String = SettingsStore.defaultKey) {
        self.defaults = defaults
        self.key = key
    }

    /// Returns an empty configuration when nothing is stored yet or when the
    /// stored payload cannot be decoded, so a corrupt blob never blocks launch.
    func load() -> AppConfiguration {
        guard let data = defaults.data(forKey: key) else { return AppConfiguration() }
        do {
            return try decoder.decode(AppConfiguration.self, from: data)
        } catch {
            Log.lifecycle.error("Discarding unreadable configuration: \(error.localizedDescription, privacy: .public)")
            return AppConfiguration()
        }
    }

    func save(_ configuration: AppConfiguration) {
        do {
            defaults.set(try encoder.encode(configuration), forKey: key)
        } catch {
            Log.lifecycle.error("Failed to save configuration: \(error.localizedDescription, privacy: .public)")
        }
    }
}
