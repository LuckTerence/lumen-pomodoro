import AppKit
import SwiftUI

enum DynamicIslandMode: Equatable {
    case compact
    case expanded
    case transient
}

/// 顶部灵动岛：Compact / Expanded / Transient
@MainActor
final class DynamicIslandService: ObservableObject {
    @Published private(set) var isVisible = false
    @Published private(set) var title = ""
    @Published private(set) var countdownText = ""
    @Published private(set) var mode: DynamicIslandMode = .compact
    @Published private(set) var sessionMode: TimerMode = .idle
    @Published var mainWindowFocused = false
    /// keep | minimize | hide
    @Published var whenFocused = "minimize"

    var onPause: (() -> Void)?
    var onResume: (() -> Void)?
    var onSkipBreak: (() -> Void)?
    var onStartFocus: (() -> Void)?
    var onOpenMain: (() -> Void)?

    private var panel: NSPanel?
    private var hostingView: NSHostingView<DynamicIslandContentView>?
    private var autoHideTask: Task<Void, Never>?
    private var expandIdleTask: Task<Void, Never>?
    private var hasActiveCountdown = false

    func showNotification(title: String, message: String) {
        mode = .transient
        self.title = title
        countdownText = message
        showPanel(width: 340, height: 64)
        scheduleAutoHide(seconds: 2.5) { [weak self] in
            guard let self else { return }
            if self.hasActiveCountdown {
                self.mode = .compact
                self.refreshSize()
                self.applyFocusVisuals()
            } else {
                self.hide()
            }
        }
    }

    func startCountdown(title: String, remaining: String, session: TimerMode) {
        hasActiveCountdown = true
        mode = .compact
        sessionMode = session
        self.title = title
        countdownText = remaining
        cancelAutoHide()
        showPanel(width: 220, height: 52)
        applyFocusVisuals()
    }

    func updateCountdown(_ remaining: String, session: TimerMode) {
        guard hasActiveCountdown else { return }
        sessionMode = session
        if mode != .transient {
            countdownText = remaining
            if mode == .compact {
                title = Self.title(for: session)
            }
        }
        reposition()
        applyFocusVisuals()
    }

    func hide() {
        cancelAutoHide()
        cancelExpandIdle()
        panel?.orderOut(nil)
        isVisible = false
        hasActiveCountdown = false
        mode = .compact
    }

    func toggleExpanded() {
        guard mode != .transient else { return }
        if mode == .expanded {
            mode = .compact
            cancelExpandIdle()
            refreshSize()
        } else {
            mode = .expanded
            refreshSize()
            scheduleExpandIdle()
        }
        objectWillChange.send()
    }

    func collapseExpanded() {
        guard mode == .expanded else { return }
        mode = .compact
        cancelExpandIdle()
        refreshSize()
    }

    private func scheduleExpandIdle() {
        cancelExpandIdle()
        expandIdleTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(4))
            guard !Task.isCancelled else { return }
            await MainActor.run { self?.collapseExpanded() }
        }
    }

    private func cancelExpandIdle() {
        expandIdleTask?.cancel()
        expandIdleTask = nil
    }

    private func scheduleAutoHide(seconds: Double, onDone: @escaping () -> Void) {
        cancelAutoHide()
        autoHideTask = Task {
            try? await Task.sleep(for: .seconds(seconds))
            guard !Task.isCancelled else { return }
            onDone()
        }
    }

    private func cancelAutoHide() {
        autoHideTask?.cancel()
        autoHideTask = nil
    }

    private func showPanel(width: CGFloat, height: CGFloat) {
        let content = DynamicIslandContentView(service: self)
        if hostingView == nil {
            hostingView = NSHostingView(rootView: content)
        } else {
            hostingView?.rootView = content
        }
        guard let hostingView else { return }

        if panel == nil {
            let panel = NSPanel(
                contentRect: NSRect(x: 0, y: 0, width: width, height: height),
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

        panel?.setContentSize(NSSize(width: width, height: height))
        panel?.contentView = hostingView
        reposition()
        panel?.orderFrontRegardless()
        isVisible = true
        applyFocusVisuals()
    }

    private func refreshSize() {
        let size: NSSize
        switch mode {
        case .expanded:
            size = NSSize(width: 360, height: 88)
        case .transient:
            size = NSSize(width: 340, height: 64)
        case .compact:
            size = NSSize(width: 220, height: 52)
        }
        panel?.setContentSize(size)
        if let hostingView {
            hostingView.rootView = DynamicIslandContentView(service: self)
        }
        reposition()
    }

    private func applyFocusVisuals() {
        guard let panel else { return }
        let policy = whenFocused.lowercased()
        if mainWindowFocused && policy == "hide" && mode != .transient {
            panel.orderOut(nil)
            isVisible = false
            return
        }
        if !panel.isVisible {
            panel.orderFrontRegardless()
            isVisible = true
        }
        if mainWindowFocused && policy == "minimize" && mode == .compact {
            panel.alphaValue = 0.55
        } else {
            panel.alphaValue = 1.0
        }
    }

    private func reposition() {
        guard let panel, let screen = NSScreen.main else { return }
        let frame = panel.frame
        let x = screen.frame.midX - frame.width / 2
        let y = screen.visibleFrame.maxY - frame.height - 12
        panel.setFrameOrigin(NSPoint(x: x, y: y))
    }

    static func title(for session: TimerMode) -> String {
        switch session {
        case .focus: return "专注中"
        case .break: return "休息中"
        case .paused: return "已暂停"
        case .idle: return "Lumen"
        }
    }
}

struct DynamicIslandContentView: View {
    @ObservedObject var service: DynamicIslandService

    var body: some View {
        VStack(spacing: service.mode == .expanded ? 10 : 4) {
            HStack(spacing: 12) {
                if !service.title.isEmpty {
                    Text(service.title)
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.white.opacity(0.9))
                }
                Text(service.countdownText)
                    .font(service.mode == .compact
                          ? .title3.monospacedDigit().weight(.semibold)
                          : .caption)
                    .foregroundStyle(.white)
                    .lineLimit(2)
            }

            if service.mode == .expanded {
                HStack(spacing: 8) {
                    actions
                }
            }
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 12)
        .background(
            Capsule()
                .fill(Color.black.opacity(0.88))
                .shadow(color: .black.opacity(0.28), radius: 12, y: 4)
        )
        .contentShape(Capsule())
        .onTapGesture {
            service.toggleExpanded()
        }
    }

    @ViewBuilder
    private var actions: some View {
        switch service.sessionMode {
        case .focus:
            islandButton("暂停") { service.onPause?(); service.collapseExpanded() }
        case .paused:
            islandButton("继续") { service.onResume?(); service.collapseExpanded() }
        case .break:
            islandButton("结束休息") { service.onSkipBreak?(); service.collapseExpanded() }
        case .idle:
            islandButton("开始") { service.onStartFocus?(); service.collapseExpanded() }
        }
        islandButton("主窗口") { service.onOpenMain?(); service.collapseExpanded() }
    }

    private func islandButton(_ title: String, action: @escaping () -> Void) -> some View {
        Button(title, action: action)
            .buttonStyle(.plain)
            .font(.caption.weight(.semibold))
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(Color.white.opacity(0.15), in: Capsule())
            .foregroundStyle(.white)
    }
}
