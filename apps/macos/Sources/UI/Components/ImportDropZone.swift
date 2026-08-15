import SwiftUI

/// Drop target beside the import button. Files land in the import tray for
/// review rather than being copied in straight away.
struct ImportDropZone: View {
    @ObservedObject var queue: ImportQueue
    @Binding var isTargeted: Bool

    var body: some View {
        HStack(spacing: 6) {
            Image(systemName: "arrow.down.doc")
            Text("Drop videos here")
        }
        .font(.callout)
        .foregroundStyle(isTargeted ? Brand.wavePurple : Color.secondary)
        .padding(.horizontal, 12)
        .padding(.vertical, 5)
        .background {
            RoundedRectangle(cornerRadius: 6, style: .continuous)
                .fill(Brand.wavePurple.opacity(isTargeted ? 0.12 : 0))
        }
        .overlay {
            RoundedRectangle(cornerRadius: 6, style: .continuous)
                .strokeBorder(
                    isTargeted ? Brand.wavePurple : Color.secondary.opacity(0.45),
                    style: StrokeStyle(lineWidth: 1, dash: [4, 3])
                )
        }
        .dropDestination(for: URL.self) { urls, _ in
            queue.stage(urls)
            return true
        } isTargeted: { isTargeted = $0 }
        .accessibilityLabel("Drop videos here to import")
    }
}
