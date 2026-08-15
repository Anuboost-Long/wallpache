import SwiftUI

/// Grid of imported wallpapers with import, apply, and delete actions.
struct LibraryView: View {
    @EnvironmentObject private var coordinator: WallpaperCoordinator
    @State private var isDropTargeted = false
    @State private var isZoneTargeted = false

    private let columns = [GridItem(.adaptive(minimum: 200, maximum: 280), spacing: 16)]

    var body: some View {
        VStack(spacing: 0) {
            content
            ImportTray(queue: coordinator.importQueue)
            Divider()
            toolbar
        }
        .dropDestination(for: URL.self) { urls, _ in
            coordinator.importQueue.stage(urls)
            return true
        } isTargeted: { isDropTargeted = $0 }
        .overlay {
            // The zone draws its own highlight, so the window-wide one would
            // only be a second signal for the same drop.
            if isDropTargeted, !isZoneTargeted {
                RoundedRectangle(cornerRadius: 12)
                    .strokeBorder(Color.accentColor, lineWidth: 3)
                    .padding(8)
            }
        }
    }

    // MARK: - Sections

    @ViewBuilder
    private var content: some View {
        if coordinator.library.isEmpty {
            emptyState
        } else {
            ScrollView {
                LazyVGrid(columns: columns, spacing: 16) {
                    ForEach(coordinator.library) { record in
                        WallpaperCell(record: record)
                    }
                }
                .padding(16)
            }
        }
    }

    private var emptyState: some View {
        VStack(spacing: 10) {
            BrandMarkView(size: 76)
                .opacity(0.9)
            Text("No wallpapers yet")
                .font(.title3.weight(.medium))
            Text("Drag an .mp4, .mov, .m4v, or .gif here, or use Import Video.")
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding()
    }

    private var toolbar: some View {
        HStack {
            Text(coordinator.statusMessage)
                .font(.callout)
                .foregroundStyle(.secondary)

            Spacer()

            if coordinator.isImporting {
                ProgressView()
                    .controlSize(.small)
            }

            ImportDropZone(queue: coordinator.importQueue, isTargeted: $isZoneTargeted)

            Button("Import Video…") {
                Task { await coordinator.presentImportPanel() }
            }
            .disabled(coordinator.isImporting)
        }
        .padding(12)
    }
}

/// One wallpaper in the grid.
private struct WallpaperCell: View {
    @EnvironmentObject private var coordinator: WallpaperCoordinator
    let record: WallpaperRecord

    @State private var isPreviewing = false
    @State private var isRenaming = false
    @State private var isConfirmingDelete = false
    @State private var draftName = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            WallpaperThumbnail(url: coordinator.storage.thumbnailURL(for: record))
                .aspectRatio(16 / 9, contentMode: .fit)

            Text(record.name)
                .lineLimit(1)
                .font(.callout.weight(.medium))

            Text(subtitle)
                .font(.caption)
                .foregroundStyle(.secondary)

            HStack(spacing: 8) {
                Button("Apply") { coordinator.applyToAllDisplays(record) }

                Button("Preview") { isPreviewing = true }

                Menu("Display") {
                    ForEach(coordinator.displays) { display in
                        Button(display.name) { coordinator.apply(record, to: display.id) }
                    }
                }
                .menuStyle(.borderlessButton)
                .fixedSize()
                .disabled(coordinator.displays.count < 2)

                Spacer()

                // Rename and delete are also in the context menu, but a
                // right-click-only action is easy to miss.
                Menu {
                    Button("Rename…") { beginRenaming() }
                    Divider()
                    Button("Delete…", role: .destructive) { isConfirmingDelete = true }
                } label: {
                    Image(systemName: "ellipsis.circle")
                }
                .menuStyle(.borderlessButton)
                .menuIndicator(.hidden)
                .fixedSize()
                .accessibilityLabel("More actions for \(record.name)")
            }
        }
        .padding(10)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 12))
        .sheet(isPresented: $isPreviewing) {
            WallpaperPreviewView(
                record: record,
                videoURL: coordinator.storage.videoURL(for: record),
                // Shown at 640 points, so the grid thumbnail would be upscaled
                // on a Retina display.
                thumbnailURL: coordinator.storage.stillURL(for: record)
                    ?? coordinator.storage.thumbnailURL(for: record),
                onApply: { coordinator.applyToAllDisplays(record) }
            )
        }
        .contextMenu {
            Button("Preview…") { isPreviewing = true }
            Button("Apply to All Displays") { coordinator.applyToAllDisplays(record) }
            ForEach(coordinator.displays) { display in
                Button("Apply to \(display.name)") { coordinator.apply(record, to: display.id) }
            }
            Divider()
            Button("Rename…") { beginRenaming() }
            Button("Delete…", role: .destructive) { isConfirmingDelete = true }
        }
        .alert("Rename Wallpaper", isPresented: $isRenaming) {
            TextField("Name", text: $draftName)
            Button("Rename") { coordinator.rename(record, to: draftName) }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Choose a new name for this wallpaper. The video file itself is not renamed.")
        }
        .confirmationDialog(
            "Delete “\(record.name)”?",
            isPresented: $isConfirmingDelete,
            titleVisibility: .visible
        ) {
            Button("Delete", role: .destructive) { coordinator.delete(record) }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("The imported copy is removed from your library and deleted from disk. Any display using it falls back to the normal macOS wallpaper.")
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("\(record.name), \(subtitle)")
    }

    /// Seeds the field with the current name so the dialog opens ready to edit.
    private func beginRenaming() {
        draftName = record.name
        isRenaming = true
    }

    private var subtitle: String {
        [record.durationDescription, record.resolutionDescription]
            .compactMap { $0 }
            .joined(separator: " · ")
    }
}
