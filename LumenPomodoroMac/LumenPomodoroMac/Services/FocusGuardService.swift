import AppKit
import ApplicationServices
import Foundation

@MainActor
final class FocusGuardService {
    var onDistraction: ((String) -> Void)?
    var onFocusRegained: (() -> Void)?

    /// 测试用：覆盖勿扰检测
    var doNotDisturbOverride: (() -> Bool)?

    private var timer: Timer?
    private var blocklist: [String] = []
    private var idleThresholdSeconds: Double = 180
    private var debounceHits = 2
    private var maxAlertsPerSession = 3
    private var respectDoNotDisturb = true
    private var ownProcessName = ""
    private var ownBundleId = ""
    private var consecutiveHits = 0
    private var alertCount = 0
    private var isDistracted = false
    private(set) var isRunning = false

    /// 当前会话已发出的告警次数
    var sessionAlertCount: Int { alertCount }

    /// - Parameter resetSessionCounters: true 新专注会话；false 暂停后恢复（保留告警计数）
    func start(settings: Settings, resetSessionCounters: Bool = true) {
        guard settings.focusGuardEnabled else { return }
        stop(clearSessionCounters: false)

        blocklist = settings.focusGuardBlocklist
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }

        idleThresholdSeconds = Double(max(1, settings.focusGuardIdleSeconds))
        let pollSeconds = max(1, settings.focusGuardPollSeconds)
        debounceHits = min(10, max(1, settings.focusGuardDebounceHits))
        maxAlertsPerSession = min(20, max(1, settings.focusGuardMaxAlertsPerSession))
        respectDoNotDisturb = settings.focusGuardRespectDoNotDisturb

        let app = NSRunningApplication.current
        ownProcessName = app.localizedName ?? ""
        ownBundleId = app.bundleIdentifier ?? ""

        if resetSessionCounters {
            consecutiveHits = 0
            alertCount = 0
            isDistracted = false
        } else {
            consecutiveHits = 0
            isDistracted = false
        }
        isRunning = true

        if !AXIsProcessTrusted() {
            NSLog("[FocusGuard] Accessibility not trusted; window title matching degraded")
        }

        timer = Timer.scheduledTimer(withTimeInterval: TimeInterval(pollSeconds), repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.tick()
            }
        }
    }

    /// 停止轮询。默认保留会话告警计数（暂停场景）；新会话由 start(resetSessionCounters: true) 清零。
    func stop(clearSessionCounters: Bool = false) {
        timer?.invalidate()
        timer = nil
        isRunning = false
        consecutiveHits = 0
        isDistracted = false
        if clearSessionCounters {
            alertCount = 0
        }
    }

    private func tick() {
        guard isRunning else { return }

        let suppress = respectDoNotDisturb && isDoNotDisturbActive()

        if let reason = evaluate() {
            consecutiveHits += 1
            if consecutiveHits >= debounceHits && !isDistracted {
                isDistracted = true
                if suppress {
                    // 勿扰：标记分心但不消耗配额、不通知
                    return
                }
                if alertCount < maxAlertsPerSession {
                    alertCount += 1
                    onDistraction?(reason)
                }
            }
        } else {
            if isDistracted {
                isDistracted = false
                onFocusRegained?()
            }
            consecutiveHits = 0
        }
    }

    private func isDoNotDisturbActive() -> Bool {
        if let override = doNotDisturbOverride {
            return override()
        }
        return SystemAttentionState.isDoNotDisturbActive()
    }

    private func evaluate() -> String? {
        let idle = Self.idleSeconds()
        if idle >= idleThresholdSeconds {
            let minutes = (idle / 60.0 * 10).rounded() / 10
            return "检测到你已 \(minutes) 分钟无操作，可能已离开。"
        }

        let (appName, bundleId, windowTitle) = Self.frontmostInfo()

        if !ownBundleId.isEmpty && bundleId == ownBundleId { return nil }
        if !ownProcessName.isEmpty && appName.compare(ownProcessName, options: .caseInsensitive) == .orderedSame {
            return nil
        }

        for entry in blocklist {
            if appName.localizedCaseInsensitiveContains(entry) ||
                bundleId.localizedCaseInsensitiveContains(entry) ||
                windowTitle.localizedCaseInsensitiveContains(entry) {
                let label = windowTitle.isEmpty ? appName : windowTitle
                return "检测到你正在使用「\(label)」，注意力可能已分散。"
            }
        }

        return nil
    }

    static func idleSeconds() -> Double {
        let idle = CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: .null)
        return idle.isFinite ? idle : 0
    }

    static func frontmostInfo() -> (appName: String, bundleId: String, windowTitle: String) {
        guard let app = NSWorkspace.shared.frontmostApplication else {
            return ("", "", "")
        }
        let appName = app.localizedName ?? ""
        let bundleId = app.bundleIdentifier ?? ""
        let windowTitle = frontmostWindowTitle(for: app.processIdentifier)
        return (appName, bundleId, windowTitle)
    }

    private static func frontmostWindowTitle(for pid: pid_t) -> String {
        guard AXIsProcessTrusted() else { return "" }

        let appElement = AXUIElementCreateApplication(pid)
        var focusedWindow: CFTypeRef?
        let result = AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute as CFString, &focusedWindow)
        guard result == .success, let window = focusedWindow else { return "" }

        var titleValue: CFTypeRef?
        let titleResult = AXUIElementCopyAttributeValue(window as! AXUIElement, kAXTitleAttribute as CFString, &titleValue)
        guard titleResult == .success, let title = titleValue as? String else { return "" }
        return title
    }
}
