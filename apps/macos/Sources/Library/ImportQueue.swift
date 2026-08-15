import AppKit
import Combine
import CoreGraphics
import Foundation
import os

/// Videos dropped on the library, held for review before anything is copied in.
///
/// Nothing enters storage until the user confirms, so an accidental drop costs
/// no disk space. Inspection and importing both run off the main actor, which
/// keeps the rest of the app usable while a batch is on its way in.
final class ImportQueue: ObservableObject {
    /// One dropped file and how far it has got.
    struct Item: Identifiable {
        enum State: Equatable {
            case inspecting
            case ready
            /// The file cannot be imported at all, e.g. a wrong format.
            case rejected(String)
            case importing
            case imported
            case failed(String)
        }

        let id = UUID()
        let url: URL
        var state: State = .inspecting
        var preview: NSImage?
        /// Duration and resolution, once inspection has read them.
        var detail: String?

        var name: String { url.deletingPathExtension().lastPathComponent }
        var isReady: Bool { state == .ready }
        var isImporting: Bool { state == .importing }

        var problem: String? {
            switch state {
            case .rejected(let reason), .failed(let reason): return reason
            default: return nil
            }
        }
    }

    @Published private(set) var items: [Item] = []
    @Published private(set) var importedCount = 0
    @Published private(set) var importTotal = 0

    private let importVideo: @MainActor (URL) async throws -> Void
    private var importTask: Task<Void, Never>?

    init(importVideo: @escaping @MainActor (URL) async throws -> Void) {
        self.importVideo = importVideo
    }

    var isImporting: Bool { importedCount < importTotal }

    var readyItems: [Item] { items.filter(\.isReady) }

    /// Adds dropped files, skipping ones already waiting. Each is inspected on
    /// its own task so a slow file does not hold up the previews behind it.
    func stage(_ urls: [URL]) {
        for url in urls where !items.contains(where: { $0.url == url }) {
            let item = Item(url: url)
            items.append(item)
            inspect(item.id, url: url)
        }
    }

    func remove(_ item: Item) {
        guard !item.isImporting else { return }
        items.removeAll { $0.id == item.id }
    }

    /// Clears everything the user can still take back. An item already being
    /// copied is left to finish.
    func removeAll() {
        items.removeAll { !$0.isImporting }
    }

    /// Imports every ready item, one at a time so a large batch cannot saturate
    /// the machine. The task is not awaited by the caller: the tray reports its
    /// own progress and the app stays free for anything else.
    func importReadyItems() {
        guard importTask == nil else { return }

        let queued = readyItems
        guard !queued.isEmpty else { return }

        importedCount = 0
        importTotal = queued.count

        importTask = Task {
            for item in queued {
                await importQueued(item)
                importedCount += 1
            }

            items.removeAll { $0.state == .imported }
            importedCount = 0
            importTotal = 0
            importTask = nil
        }
    }

    // MARK: - Private

    /// A file removed from the tray while it waited is simply skipped.
    private func importQueued(_ item: Item) async {
        guard items.contains(where: { $0.id == item.id }) else { return }

        update(item.id) { $0.state = .importing }

        do {
            try await importVideo(item.url)
            update(item.id) { $0.state = .imported }
        } catch {
            Log.library.error("Import failed: \(error.localizedDescription, privacy: .public)")
            update(item.id) { $0.state = .failed(error.localizedDescription) }
        }
    }

    private func inspect(_ id: Item.ID, url: URL) {
        Task {
            let inspection = await Self.inspect(url: url)
            update(id) { item in
                item.detail = inspection.detail
                item.preview = inspection.preview.map { NSImage(cgImage: $0, size: .zero) }
                item.state = inspection.failure.map { .rejected($0) } ?? .ready
            }
        }
    }

    private func update(_ id: Item.ID, _ mutate: (inout Item) -> Void) {
        guard let index = items.firstIndex(where: { $0.id == id }) else { return }
        mutate(&items[index])
    }

    private nonisolated struct Inspection: Sendable {
        var detail: String?
        var preview: CGImage?
        var failure: String?
    }

    /// Validates the file and pulls a preview frame, without copying anything.
    /// The same validation runs again at import time, so a file that becomes
    /// unusable in between is still caught.
    private nonisolated static func inspect(url: URL) async -> Inspection {
        guard WallpaperLibraryService.supportedExtensions.contains(url.pathExtension.lowercased()) else {
            let error = WallpaperError.notPlayable(name: url.lastPathComponent)
            return Inspection(failure: error.errorDescription)
        }

        do {
            // A GIF cannot be opened by AVFoundation, and it is only converted
            // once the user confirms, so it is measured and previewed as an image.
            if GIFTranscoder.isGIF(url) {
                let summary = try GIFTranscoder.inspect(url: url)
                return Inspection(
                    detail: describe(VideoMetadata(duration: summary.duration, pixelSize: summary.pixelSize)),
                    preview: GIFTranscoder.previewFrame(
                        url: url,
                        maximumPixelWidth: ThumbnailGenerator.thumbnailPixelWidth
                    )
                )
            }

            let metadata = try await VideoMetadataReader.read(url: url)
            return Inspection(
                detail: describe(metadata),
                preview: await ThumbnailGenerator.frame(from: url, duration: metadata.duration)
            )
        } catch {
            return Inspection(failure: error.localizedDescription)
        }
    }

    private nonisolated static func describe(_ metadata: VideoMetadata) -> String {
        let total = Int(metadata.duration.rounded())
        let duration = String(format: "%d:%02d", total / 60, total % 60)

        guard let size = metadata.pixelSize else { return duration }
        return "\(duration) · \(Int(size.width)) × \(Int(size.height))"
    }
}
