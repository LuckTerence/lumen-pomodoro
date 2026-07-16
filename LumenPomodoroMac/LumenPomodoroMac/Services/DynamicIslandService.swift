import AppKit
import SwiftUI

@MainActor
final class DynamicIslandService: ObservableObject {
    @Published private(set) var isVisible = false
    @Published private(set) var title = ""
    @Published private(set) var countdownText = ""
    @Published private(set) var isCountdownMode = false

    private var panel: NSPanel?
    private var hostingView: NSHostingView<DynamicIslandContentView>?
    private var autoHideTask: Task<Void, Never>?

    func showNotification(title: String, message: String) {
        isCountdownMode = false
        self.title = title
        countdownText = message
        showPanel(width: 320)
        autoHideTask?.cancel()
        autoHideTask = Task {
            try? await Task.sleep(for: .seconds(2.5))
            guard !Task.isCancelled else { return }
            hide()
        }
    }

    func startCountdown(title: String, remaining: String) {
        isCountdownMode = true
        self.title = title
        countdownText = remaining
        showPanel(width: 220)
        autoHideTask?.cancel()
    }

    func updateCountdown(_ remaining: String) {
        guard isCountdownMode else { return }
        countdownText = remaining
        reposition()
    }

    func hide() {
        autoHideTask?.cancel()
        autoHideTask = nil
        panel?.orderOut(nil)
        isVisible = false
        isCountdownMode = false
    }

    private func showPanel(width: CGFloat) {
        let content = DynamicIslandContentView(service: self)
        if hostingView == nil {
            hostingView = NSHostingView(rootView: content)
        } else {
            hostingView?.rootView = content
        }

        guard let hostingView else { return }

        if panel == nil {
            let panel = NSPanel(
                contentRect: NSRect(x: 0, y: 0, width: width, height: 56),
                styleMask: [.nonactivatingPanel, .borderless],
                backing: .buffered,
                defer: false
            )
            panel.isOpaque = false
            panel.backgroundColor = .clear
            panel.hasShadow = true
            panel.level = .floating
            panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
            panel.hidesOnDeactivate = false
            panel.contentView = hostingView
            self.panel = panel
        }

        panel?.setContentSize(NSSize(width: width, height: isCountdownMode ? 56 : 72))
        panel?.contentView = hostingView
        reposition()
        panel?.orderFrontRegardless()
        isVisible = true
    }

    private func reposition() {
        guard let panel, let screen = NSScreen.main else { return }
        let frame = panel.frame
        let x = screen.frame.midX - frame.width / 2
        let y = screen.visibleFrame.maxY - frame.height - 12
        panel.setFrameOrigin(NSPoint(x: x, y: y))
    }
}

struct DynamicIslandContentView: View {
    @ObservedObject var service: DynamicIslandService

    var body: some View {
        VStack(spacing: service.isCountdownMode ? 4 : 6) {
            if !service.title.isEmpty {
                Text(service.title)
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.white.opacity(0.9))
            }
            Text(service.countdownText)
                .font(service.isCountdownMode ? .title3.monospacedDigit().weight(.semibold) : .caption)
                .foregroundStyle(.white)
                .lineLimit(2)
                .multilineTextAlignment(.center)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 12)
        .background(
            Capsule()
                .fill(Color.black.opacity(0.85))
                .shadow(color: .black.opacity(0.25), radius: 12, y: 4)
        )
    }
}
