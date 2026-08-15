import SwiftUI

/// Videos waiting to be imported, each with its own preview and progress, so
/// unwanted files can be dropped from the batch before anything is copied in.
struct ImportTray: View {
    @ObservedObject var queue: ImportQueue

    var body: some View {
        if !queue.items.isEmpty {
            VStack(spacing: 10) {
                Divider()
                header
                cards
            }
            .padding(.bottom, 12)
        }
    }

    // MARK: - Sections

    private var header: some View {
        HStack(spacing: 10) {
            Text(title)
                .font(.callout.weight(.medium))

            if queue.isImporting {
                ProgressView(value: Double(queue.importedCount), total: Double(queue.importTotal))
                    .frame(width: 120)
            }

            Spacer()

            Button("Clear") { queue.removeAll() }

            Button(importTitle) { queue.importReadyItems() }
                .buttonStyle(.borderedProminent)
                .disabled(queue.readyItems.isEmpty || queue.isImporting)
        }
        .padding(.horizontal, 12)
    }

    /// A fixed height keeps the tray a strip along the bottom: left to size
    /// itself, the scroll view would take half the window from the grid.
    private var cards: some View {
        ScrollView(.horizontal) {
            HStack(alignment: .top, spacing: 10) {
                ForEach(queue.items) { item in
                    ImportTrayCard(item: item) { queue.remove(item) }
                }
            }
            .padding(.horizontal, 12)
        }
        .frame(height: ImportTrayCard.height)
    }

    // MARK: - Labels

    private var title: String {
        if queue.isImporting {
            return "Importing \(min(queue.importedCount + 1, queue.importTotal)) of \(queue.importTotal)…"
        }

        return switch queue.readyItems.count {
        case 0: "Nothing ready to import"
        case 1: "1 video ready to import"
        case let count: "\(count) videos ready to import"
        }
    }

    private var importTitle: String {
        queue.readyItems.count == 1 ? "Import 1 Video" : "Import \(queue.readyItems.count) Videos"
    }
}

/// One staged file: its preview frame, what it is, and how far it has got.
private struct ImportTrayCard: View {
    static let height: CGFloat = 148

    let item: ImportQueue.Item
    let onRemove: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            preview

            Text(item.name)
                .font(.caption.weight(.medium))
                .lineLimit(1)

            Text(subtitle)
                .font(.caption2)
                .foregroundStyle(item.problem == nil ? AnyShapeStyle(.secondary) : AnyShapeStyle(Color.orange))
                .lineLimit(2)
                .fixedSize(horizontal: false, vertical: true)

            Spacer(minLength: 0)
        }
        .frame(width: 148, height: Self.height - 16, alignment: .topLeading)
        .padding(8)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 10))
        .help(subtitle)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("\(item.name), \(subtitle)")
    }

    private var preview: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 6, style: .continuous)
                .fill(Color(nsColor: .quaternaryLabelColor))

            if let image = item.preview {
                Image(nsImage: image)
                    .resizable()
                    .aspectRatio(contentMode: .fill)
            } else {
                Image(systemName: "film")
                    .font(.title3)
                    .foregroundStyle(.secondary)
            }

            statusOverlay
        }
        .frame(height: 76)
        .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
        .overlay(alignment: .topTrailing) { removeButton }
    }

    @ViewBuilder
    private var statusOverlay: some View {
        switch item.state {
        case .inspecting, .importing:
            scrim { ProgressView().controlSize(.small) }
        case .imported:
            scrim {
                Image(systemName: "checkmark.circle.fill")
                    .font(.title2)
                    .foregroundStyle(.white)
            }
        case .rejected, .failed:
            scrim {
                Image(systemName: "exclamationmark.triangle.fill")
                    .font(.title3)
                    .foregroundStyle(.orange)
            }
        case .ready:
            EmptyView()
        }
    }

    /// Removing is offered until the copy starts; after that the item has to
    /// finish, and it leaves the tray on its own.
    @ViewBuilder
    private var removeButton: some View {
        if !item.isImporting, item.state != .imported {
            Button(action: onRemove) {
                Image(systemName: "xmark.circle.fill")
                    .symbolRenderingMode(.palette)
                    .foregroundStyle(.white, .black.opacity(0.55))
            }
            .buttonStyle(.plain)
            .padding(4)
            .accessibilityLabel("Remove \(item.name)")
        }
    }

    private func scrim<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        ZStack {
            Color.black.opacity(0.35)
            content()
        }
    }

    private var subtitle: String {
        switch item.state {
        case .inspecting: "Checking…"
        case .ready: item.detail ?? ""
        case .importing: "Importing…"
        case .imported: "Imported"
        case .rejected(let reason), .failed(let reason): reason
        }
    }
}
