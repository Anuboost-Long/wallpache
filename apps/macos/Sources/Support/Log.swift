import Foundation
import os

/// Centralised loggers so subsystem/category strings are declared once.
nonisolated enum Log {
    private static let subsystem = Bundle.main.bundleIdentifier ?? "com.kimlongly.Wallpache"

    static let lifecycle = Logger(subsystem: subsystem, category: "lifecycle")
    static let playback = Logger(subsystem: subsystem, category: "playback")
    static let displays = Logger(subsystem: subsystem, category: "displays")
    static let library = Logger(subsystem: subsystem, category: "library")
    static let policy = Logger(subsystem: subsystem, category: "policy")
}
