import AppKit
import SwiftUI

enum DynamicIslandMode: Equatable {
    case compact
    case expanded
    case transient
}

struct IslandTaskItem: Identifiable, Equatable {
    let id: String
    let name: String
    let color: String
}

/// 顶部灵动岛：Compact / Expanded / Transient + 选任务 + 精修动效
@MainActor
final class DynamicIslandService: ObservableObject {
    @Published private(set) var isVisible = false
    @Published private(set) var title = ""
    @Published private(set) var countdownText = ""
    @Published private(set) var mode: DynamicIslandMode = .compact
    @Published private(set) var sessionMode: TimerMode = .idle
    @Published var mainWindowFocused = false
    @Published var whenFocused = "minimize"
    @Published var tasks: [IslandTaskItem] = []
    @Published var selectedTaskId: String?
    @Published var selectedTaskName: String?
    @Published var selectedTaskColor: String = "#3B82F6"
    /// 驱动 SwiftUI 动效
    @Published private(set) var appearToken = 0

    var onPause: (() -> Void)?
    var onResume: (() -> Void)?
    var onSkipBreak: (() -> Void)?
    var onStartFocus: (() -> Void)?
    var onOpenMain: (() -> Void)?
    var onSelectTask: ((String) -> Void)?

    private var panel: NSPanel?
    private var hostingView: NSHostingView<DynamicIslandRootView>?
    private var autoHideTask: Task<Void, Never>?
    private var expandIdleTask: Task<Void, Never>?
    private var hasActiveCountdown = false

    func setTasks(_ items: [IslandTaskItem], selectedId: String?) {
        tasks = Array(items.prefix(8))
        selectedTaskId = selectedId
        if let id = selectedId, let hit = tasks.first(where: { $0.id == id }) {
            selectedTaskName = hit.name
            selectedTaskColor = hit.color
        }
        refreshHosting()
    }

    func showNotification(title: String, message: String) {
        mode = .transient
        self.title = title
        countdownText = message
        appearToken &+= 1
        showPanel()
        scheduleAutoHide(seconds: 2.6) { [weak self] in
            guard let self else { return }
            if self.hasActiveCountdown {
                withAnimation(.spring(response: 0.38, dampingFraction: 0.82)) {
                    self.mode = .compact
                    self.applyCompactTitle()
                }
                self.refreshSize()
                self.applyFocusVisuals()
            } else {
                self.hide()
            }
        }
    }

    func showIdleReady(taskLabel: String, readyTime: String) {
        hasActiveCountdown = true
        mode = .compact
        sessionMode = .idle
        selectedTaskName = taskLabel == "选择任务" ? selectedTaskName : taskLabel
        applyCompactTitle()
        countdownText = readyTime
        cancelAutoHide()
        appearToken &+= 1
        showPanel()
        applyFocusVisuals()
    }

    func startCountdown(title: String, remaining: String, session: TimerMode) {
        hasActiveCountdown = true
        mode = .compact
        sessionMode = session
        countdownText = remaining
        applyCompactTitle(fallback: title)
        cancelAutoHide()
        appearToken &+= 1
        showPanel()
        applyFocusVisuals()
    }

    func updateCountdown(_ remaining: String, session: TimerMode) {
        guard hasActiveCountdown else { return }
        sessionMode = session
        if mode != .transient {
            countdownText = remaining
            if mode == .compact {
                applyCompactTitle()
            }
        }
        reposition()
        applyFocusVisuals()
    }

    func hide() {
        cancelAutoHide()
        cancelExpandIdle()
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.22
            ctx.timingFunction = CAMediaTimingFunction(name: .easeIn)
            panel?.animator().alphaValue = 0
        } completionHandler: { [weak self] in
            Task { @MainActor in
                guard let self else { return }
                self.panel?.orderOut(nil)
                self.panel?.alphaValue = 1
                self.isVisible = false
                self.hasActiveCountdown = false
                self.mode = .compact
            }
        }
    }

    func toggleExpanded() {
        guard mode != .transient else { return }
        withAnimation(.spring(response: 0.42, dampingFraction: 0.78)) {
            if mode == .expanded {
                mode = .compact
                cancelExpandIdle()
            } else {
                mode = .expanded
                scheduleExpandIdle()
            }
        }
        refreshSize()
        objectWillChange.send()
    }

    func collapseExpanded() {
        guard mode == .expanded else { return }
        withAnimation(.spring(response: 0.36, dampingFraction: 0.85)) {
            mode = .compact
        }
        cancelExpandIdle()
        refreshSize()
    }

    func selectTask(id: String) {
        guard sessionMode == .idle else { return }
        selectedTaskId = id
        if let hit = tasks.first(where: { $0.id == id }) {
            selectedTaskName = hit.name
            selectedTaskColor = hit.color
        }
        applyCompactTitle()
        onSelectTask?(id)
        scheduleExpandIdle()
        appearToken &+= 1
        refreshHosting()
    }

    private func applyCompactTitle(fallback: String? = nil) {
        let modeLabel: String
        switch sessionMode {
        case .focus: modeLabel = "专注中"
        case .break: modeLabel = "休息中"
        case .paused: modeLabel = "已暂停"
        case .idle: modeLabel = "准备"
        }
        if let name = selectedTaskName, !name.isEmpty, sessionMode == .idle || sessionMode == .focus || sessionMode == .paused {
            title = "\(modeLabel) · \(name)"
        } else {
            title = fallback ?? modeLabel
        }
    }

    private func scheduleExpandIdle() {
        cancelExpandIdle()
        expandIdleTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(5))
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

    private func showPanel() {
        let content = DynamicIslandRootView(service: self)
        if hostingView == nil {
            hostingView = NSHostingView(rootView: content)
        } else {
            hostingView?.rootView = content
        }
        guard let hostingView else { return }

        if panel == nil {
            let panel = NSPanel(
                contentRect: NSRect(x: 0, y: 0, width: 240, height: 56),
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

        refreshSize()
        panel?.contentView = hostingView
        panel?.alphaValue = 0
        reposition()
        panel?.orderFrontRegardless()
        isVisible = true
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.28
            ctx.timingFunction = CAMediaTimingFunction(name: .easeOut)
            panel?.animator().alphaValue = 1
        }
        applyFocusVisuals()
    }

    private func refreshHosting() {
        hostingView?.rootView = DynamicIslandRootView(service: self)
        refreshSize()
    }

    private func refreshSize() {
        let size: NSSize
        switch mode {
        case .expanded:
            let taskRows: CGFloat = tasks.isEmpty ? 0 : 36
            size = NSSize(width: min(420, 280 + CGFloat(min(tasks.count, 4)) * 20), height: 92 + taskRows)
        case .transient:
            size = NSSize(width: 340, height: 68)
        case .compact:
            size = NSSize(width: 248, height: 54)
        }
        panel?.setContentSize(size)
        hostingView?.rootView = DynamicIslandRootView(service: self)
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
        let target: CGFloat
        if mainWindowFocused && policy == "minimize" && mode == .compact {
            target = 0.55
        } else {
            target = 1.0
        }
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.22
            panel.animator().alphaValue = target
        }
    }

    private func reposition() {
        guard let panel, let screen = NSScreen.main else { return }
        let frame = panel.frame
        let x = screen.frame.midX - frame.width / 2
        let y = screen.visibleFrame.maxY - frame.height - 10
        panel.setFrameOrigin(NSPoint(x: x, y: y))
    }

    static func title(for session: TimerMode) -> String {
        switch session {
        case .focus: return "专注中"
        case .break: return "休息中"
        case .paused: return "已暂停"
        case .idle: return "准备"
        }
    }
}

// MARK: - SwiftUI

struct DynamicIslandRootView: View {
    @ObservedObject var service: DynamicIslandService

    var body: some View {
        DynamicIslandContentView(service: service)
            .id(service.appearToken)
            .transition(.asymmetric(
                insertion: .scale(scale: 0.82).combined(with: .opacity).combined(with: .offset(y: -6)),
                removal: .scale(scale: 0.9).combined(with: .opacity)
            ))
            .animation(.spring(response: 0.42, dampingFraction: 0.78), value: service.mode)
            .animation(.spring(response: 0.35, dampingFraction: 0.82), value: service.selectedTaskId)
            .animation(.easeOut(duration: 0.2), value: service.countdownText)
    }
}

struct DynamicIslandContentView: View {
    @ObservedObject var service: DynamicIslandService

    var body: some View {
        VStack(spacing: service.mode == .expanded ? 10 : 4) {
            HStack(spacing: 10) {
                if let color = service.selectedTaskColor.nilIfEmpty, service.mode != .transient {
                    Circle()
                        .fill(Color(hex: color))
                        .frame(width: 8, height: 8)
                        .transition(.scale.combined(with: .opacity))
                }
                if !service.title.isEmpty {
                    Text(service.title)
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.white.opacity(0.92))
                        .lineLimit(1)
                }
                Text(service.countdownText)
                    .font(service.mode == .compact
                          ? .title3.monospacedDigit().weight(.semibold)
                          : .caption.monospacedDigit())
                    .foregroundStyle(.white)
                    .lineLimit(2)
                    .minimumScaleFactor(0.85)
            }

            if service.mode == .expanded {
                if !service.tasks.isEmpty {
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 6) {
                            ForEach(service.tasks) { task in
                                taskChip(task)
                            }
                        }
                    }
                    .frame(maxWidth: 380)
                    .transition(.move(edge: .top).combined(with: .opacity))
                }

                HStack(spacing: 8) {
                    actions
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
        .padding(.horizontal, service.mode == .expanded ? 16 : 18)
        .padding(.vertical, service.mode == .expanded ? 12 : 11)
        .background(
            Capsule(style: .continuous)
                .fill(Color.black.opacity(service.mode == .transient ? 0.92 : 0.88))
                .shadow(color: .black.opacity(0.32), radius: service.mode == .expanded ? 16 : 12, y: 5)
        )
        .scaleEffect(service.mode == .expanded ? 1.0 : 1.0)
        .contentShape(Capsule())
        .onTapGesture {
            service.toggleExpanded()
        }
    }

    private func taskChip(_ task: IslandTaskItem) -> some View {
        let selected = task.id == service.selectedTaskId
        let canSelect = service.sessionMode == .idle
        return Button {
            service.selectTask(id: task.id)
        } label: {
            HStack(spacing: 5) {
                Circle().fill(Color(hex: task.color)).frame(width: 7, height: 7)
                Text(task.name)
                    .font(.caption2.weight(selected ? .semibold : .regular))
                    .lineLimit(1)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(
                Capsule().fill(selected ? Color.accentColor.opacity(0.35) : Color.white.opacity(0.14))
            )
            .foregroundStyle(.white)
            .opacity(canSelect || selected ? 1 : 0.5)
        }
        .buttonStyle(.plain)
        .disabled(!canSelect)
        .scaleEffect(selected ? 1.04 : 1.0)
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
            islandButton("开始", prominent: true) { service.onStartFocus?(); service.collapseExpanded() }
        }
        islandButton("主窗口") { service.onOpenMain?(); service.collapseExpanded() }
    }

    private func islandButton(_ title: String, prominent: Bool = false, action: @escaping () -> Void) -> some View {
        Button(title, action: action)
            .buttonStyle(.plain)
            .font(.caption.weight(.semibold))
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(
                Capsule().fill(prominent ? Color.accentColor.opacity(0.9) : Color.white.opacity(0.16))
            )
            .foregroundStyle(.white)
    }
}

private extension String {
    var nilIfEmpty: String? { isEmpty ? nil : self }
}
