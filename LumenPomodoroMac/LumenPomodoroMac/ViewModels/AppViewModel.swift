import AppKit
import Foundation
import SwiftUI

@MainActor
final class AppViewModel: ObservableObject {
    @Published var settings: Settings
    @Published var tasks: [TaskItem] = []
    @Published var selectedTask: TaskItem?
    @Published var todayStats = DailyStats()
    @Published var remainingTime = "25:00"
    @Published var progress: Double = 1.0
    @Published var isFocusCompleted = false
    @Published var currentNotes = ""
    @Published var userRating = 0
    @Published var showPrivacySheet = false
    @Published var cameraErrorMessage: String?
    @Published var lastCompletedSummary = ""
    @Published var dailyReport: DailyReport?
    @Published var showDailyReport = false
    @Published var isWindowActive = true
    @Published var showFullscreenBreak = false
    @Published var fullscreenBreakTitle = "休息时间"

    let timerService = TimerService()
    let cameraService = CameraService()
    let dynamicIsland = DynamicIslandService()
    let focusGuard = FocusGuardService()

    private var currentSession: FocusSession?
    private var completedSessionsToday = 0
    private var sessionEndPreNotifySent = false
    private let storage = StorageService.shared

    /// 专注 / 休息 / 暂停中，退出前宜确认
    var isSessionActive: Bool {
        switch timerService.mode {
        case .focus, .break, .paused: return true
        case .idle: return false
        }
    }

    /// 若需确认则弹窗；返回 true 表示允许退出
    @discardableResult
    func confirmExitIfNeeded() -> Bool {
        guard settings.confirmExitWhileFocusing, isSessionActive else { return true }

        let alert = NSAlert()
        alert.messageText = "确认退出"
        alert.informativeText = "计时仍在进行中，确定要退出吗？未完成的本轮进度将不会保存为完成番茄。"
        alert.alertStyle = .warning
        alert.addButton(withTitle: "退出")
        alert.addButton(withTitle: "取消")
        return alert.runModal() == .alertFirstButtonReturn
    }

    var currentStatus: TimerMode { timerService.mode }
    var isCameraAlertActive: Bool { cameraService.isRunning }
    var cameraStatus: String { cameraService.statusMessage }

    var suggestLongBreak: Bool {
        completedSessionsToday > 0 && completedSessionsToday % settings.longBreakInterval == 0
    }

    var todayPomodoroProgress: Double {
        guard settings.dailyTargetPomodoros > 0 else { return 0 }
        return min(1, Double(todayStats.completedPomodoros) / Double(settings.dailyTargetPomodoros))
    }

    var examCountdownText: String? {
        guard settings.examCountdownEnabled, let examDate = settings.examDate else { return nil }
        let days = Calendar.current.dateComponents([.day], from: Calendar.current.startOfDay(for: Date()), to: Calendar.current.startOfDay(for: examDate)).day ?? 0
        guard days >= 0 else { return nil }
        return "\(settings.examName) 倒计时 \(days) 天"
    }

    init() {
        settings = storage.loadSettings()
        bindTimer()
        bindFocusGuard()
        reloadData()
        NotificationService.shared.requestAuthorizationIfNeeded()
        syncLaunchAtLoginFromSettings()
        prepareDailyReportIfNeeded()

        if settings.cameraAlertEnabled && !settings.hasShownCameraPrivacyNotice {
            showPrivacySheet = true
        }
    }

    func onAppBecameActive() {
        isWindowActive = true
        dynamicIsland.hide()
    }

    func onAppResignedActive() {
        isWindowActive = false
        updateDynamicIslandIfNeeded(forceStart: true)
    }

    func reloadData() {
        tasks = storage.loadTasks()
        todayStats = storage.todayStats()
        completedSessionsToday = todayStats.completedPomodoros

        if let lastId = settings.lastSelectedTaskId,
           let task = tasks.first(where: { $0.id == lastId }) {
            selectedTask = task
        } else {
            selectedTask = tasks.first
        }

        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
    }

    func saveSettings() {
        // 整值写回，确保 @Published 与磁盘一致
        let snapshot = settings
        storage.saveSettings(snapshot)
        settings = snapshot
        syncLaunchAtLoginFromSettings()
        timerService.configureIdle(minutes: settings.workMinutes)
        objectWillChange.send()
    }

    /// 一键应用严格专注预设并落盘
    func applyStrictFocusPreset() {
        var next = settings
        next.applyStrictFocusPreset()
        settings = next
        saveSettings()

        let alert = NSAlert()
        alert.messageText = "严格专注预设"
        alert.informativeText = """
        已开启：
        • 严格模式
        • 全屏休息
        • 摄像头指示灯（Severe，不可手关，跟随休息）
        • 结束前预告与退出确认

        时长与任务配置未改动。
        """
        alert.alertStyle = .informational
        alert.addButton(withTitle: "好")
        alert.runModal()
    }

    func syncLaunchAtLoginFromSettings() {
        do {
            try LaunchAtLoginService.setEnabled(settings.launchAtLogin)
        } catch {
            // SMAppService 在未签名或开发环境下可能失败，忽略即可
        }
    }

    func selectTask(_ task: TaskItem) {
        selectedTask = task
        settings.lastSelectedTaskId = task.id
        saveSettings()
    }

    func applyPreset(_ preset: PomodoroPreset) {
        settings.workMinutes = preset.work
        settings.shortBreakMinutes = preset.shortBreak
        settings.longBreakMinutes = preset.longBreak
        saveSettings()
        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
    }

    func adjustWorkMinutes(by delta: Int) {
        let rounded = Int((Double(settings.workMinutes + delta) / 5.0).rounded() * 5)
        let newValue = min(max(rounded, 5), 120)
        guard newValue != settings.workMinutes else { return }
        settings.workMinutes = newValue
        saveSettings()
        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
    }

    func startFocus() {
        guard let task = selectedTask ?? tasks.first else { return }
        selectedTask = task

        if settings.cameraAlertEnabled && !settings.hasShownCameraPrivacyNotice {
            showPrivacySheet = true
            return
        }

        isFocusCompleted = false
        currentNotes = ""
        userRating = 0

        currentSession = FocusSession(
            taskId: task.id,
            taskName: task.name,
            startTime: Date(),
            focusMinutes: settings.workMinutes
        )

        settings.lastSelectedTaskId = task.id
        saveSettings()
        sessionEndPreNotifySent = false
        timerService.startFocus(minutes: settings.workMinutes)
        focusGuard.start(settings: settings, resetSessionCounters: true)
        updateDynamicIslandIfNeeded(forceStart: true)
    }

    func togglePause() {
        switch timerService.mode {
        case .focus, .break:
            timerService.pause()
            focusGuard.stop()
        case .paused:
            timerService.resume()
            if timerService.mode == .focus {
                focusGuard.start(settings: settings, resetSessionCounters: false)
            }
        case .idle:
            startFocus()
        }
        updateDynamicIslandIfNeeded(forceStart: false)
    }

    func resetTimer() {
        timerService.reset()
        currentSession = nil
        isFocusCompleted = false
        sessionEndPreNotifySent = false
        showFullscreenBreak = false
        focusGuard.stop()
        dynamicIsland.hide()
        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
        Task { await stopCameraIfNeeded() }
    }

    func confirmPrivacyNotice() {
        settings.hasShownCameraPrivacyNotice = true
        saveSettings()
        showPrivacySheet = false
    }

    func dismissDailyReport() {
        showDailyReport = false
        settings.lastReportShownDate = Calendar.current.startOfDay(for: Date())
        saveSettings()
    }

    func startBreak(long: Bool = false) {
        isFocusCompleted = false
        sessionEndPreNotifySent = false
        focusGuard.stop()
        let minutes = long ? settings.longBreakMinutes : settings.shortBreakMinutes
        fullscreenBreakTitle = long ? "长休息" : "短休息"
        timerService.startBreak(minutes: minutes)
        remainingTime = formatTime(minutes * 60)
        progress = 1
        updateDynamicIslandIfNeeded(forceStart: true)

        if settings.fullscreenBreakEnabled {
            showFullscreenBreak = true
        }

        if settings.cameraAlertEnabled && settings.cameraFollowBreakEnabled {
            Task {
                await startCameraForBreak()
            }
        }
    }

    func skipBreak() {
        guard tryAllowEndBreakEarly() else { return }

        isFocusCompleted = false
        focusGuard.stop()
        showFullscreenBreak = false
        dynamicIsland.hide()
        timerService.reset()
        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
        Task {
            await stopCameraIfNeeded()
        }
    }

    func stopCameraAlert() {
        if !settings.effectiveCameraAlertCanManualClose {
            let alert = NSAlert()
            alert.messageText = "提示"
            alert.informativeText = settings.strictModeEnabled
                ? "严格模式已开启，不能手动关闭摄像头提醒。"
                : "当前设置不允许手动关闭摄像头提醒。"
            alert.alertStyle = .informational
            alert.addButton(withTitle: "好")
            alert.runModal()
            return
        }
        Task {
            await stopCameraIfNeeded()
        }
    }

    @discardableResult
    func tryAllowEndBreakEarly() -> Bool {
        guard !settings.effectiveAllowEndBreakEarly, timerService.mode == .break else {
            return true
        }
        let alert = NSAlert()
        alert.messageText = "严格模式"
        alert.informativeText = "严格模式已开启，请等待休息自然结束。"
        alert.alertStyle = .informational
        alert.addButton(withTitle: "好")
        alert.runModal()
        return false
    }

    func setRating(_ rating: Int) {
        userRating = rating
    }

    func addTask(name: String, category: String, color: String) {
        let task = TaskItem(name: name, category: category, color: color)
        tasks.append(task)
        storage.saveTasks(tasks)
        selectedTask = task
    }

    func deleteTask(_ task: TaskItem) {
        tasks.removeAll { $0.id == task.id }
        storage.saveTasks(tasks)
        if selectedTask?.id == task.id {
            selectedTask = tasks.first
        }
    }

    func updateTask(_ task: TaskItem) {
        guard let index = tasks.firstIndex(where: { $0.id == task.id }) else { return }
        tasks[index] = task
        storage.saveTasks(tasks)
    }

    private func bindTimer() {
        timerService.onTick = { [weak self] remaining, total in
            guard let self else { return }
            self.remainingTime = self.formatTime(remaining)
            self.progress = total > 0 ? Double(remaining) / Double(total) : 0
            self.updateDynamicIslandIfNeeded(forceStart: false)
            self.maybeSendSessionEndPreNotify(remainingSeconds: remaining)
        }

        timerService.onFocusCompleted = { [weak self] in
            self?.handleFocusCompleted()
        }

        timerService.onBreakCompleted = { [weak self] in
            self?.handleBreakCompleted()
        }

        timerService.onModeChanged = { [weak self] mode in
            guard let self else { return }
            if mode != .focus { self.focusGuard.stop() }
            self.objectWillChange.send()
        }
    }

    private func bindFocusGuard() {
        focusGuard.onDistraction = { [weak self] reason in
            self?.handleFocusGuardDistraction(reason)
        }
        // 恢复专注不重置会话告警计数（防刷通知）；计数在 FocusGuardService 内管理
        focusGuard.onFocusRegained = { }
    }

    private func maybeSendSessionEndPreNotify(remainingSeconds: Int) {
        let threshold = settings.sessionEndPreNotifySeconds
        guard threshold > 0, !sessionEndPreNotifySent else { return }
        guard timerService.mode == .focus || timerService.mode == .break else { return }
        guard remainingSeconds > 0, remainingSeconds <= threshold else { return }

        sessionEndPreNotifySent = true
        NotificationService.shared.show(
            title: "即将结束",
            body: "还剩 \(remainingSeconds) 秒",
            enabled: settings.systemNotificationEnabled
        )
        if settings.soundEnabled {
            SoundService.shared.playCompletion(enabled: true)
        }
    }

    private func handleFocusGuardDistraction(_ reason: String) {
        // 次数上限已在 FocusGuardService 内执行
        NotificationService.shared.show(title: "走神提醒", body: reason, enabled: true)
        if settings.focusGuardAlertLevel != .light {
            SoundService.shared.playCompletion(enabled: true)
        }
        if settings.focusGuardAlertLevel == .severe {
            NSApplication.shared.activate(ignoringOtherApps: true)
            NSApplication.shared.windows.first?.makeKeyAndOrderFront(nil)
        }
    }

    private func handleFocusCompleted() {
        focusGuard.stop()

        if var session = currentSession {
            session.endTime = Date()
            session.completed = true
            session.notes = currentNotes.isEmpty ? nil : currentNotes
            session.qualityScore = userRating
            storage.appendSession(session)
            currentSession = nil
        }

        isFocusCompleted = true
        lastCompletedSummary = "完成 \(settings.workMinutes) 分钟专注 · \(selectedTask?.name ?? "未知任务")"

        SoundService.shared.playCompletion(enabled: settings.soundEnabled)
        NotificationService.shared.show(
            title: "专注完成",
            body: lastCompletedSummary,
            enabled: settings.systemNotificationEnabled
        )

        if settings.popupEnabled && !isWindowActive {
            dynamicIsland.showNotification(title: "专注完成", message: lastCompletedSummary)
        }

        if settings.strictModeEnabled || settings.cameraAlertLevel == .severe {
            NSApplication.shared.activate(ignoringOtherApps: true)
            NSApplication.shared.windows.first?.makeKeyAndOrderFront(nil)
        }

        Task {
            await triggerCameraAlertAfterFocus()
        }
        reloadData()
        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
        dynamicIsland.hide()
    }

    private func handleBreakCompleted() {
        focusGuard.stop()
        showFullscreenBreak = false
        SoundService.shared.playCompletion(enabled: settings.soundEnabled)
        NotificationService.shared.show(
            title: "休息结束",
            body: "可以开始下一轮专注了。",
            enabled: settings.systemNotificationEnabled
        )
        Task {
            await stopCameraIfNeeded()
        }
        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
        dynamicIsland.hide()
    }

    private func triggerCameraAlertAfterFocus() async {
        guard settings.cameraAlertEnabled else { return }
        switch settings.cameraAlertMode {
        case .fixedDuration:
            await cameraService.startForDuration(seconds: settings.cameraFixedOnSeconds)
        case .untilConfirm, .followBreak:
            do { try await cameraService.start() }
            catch { cameraErrorMessage = error.localizedDescription }
        }
    }

    private func startCameraForBreak() async {
        guard settings.cameraAlertEnabled else { return }
        do { try await cameraService.start() }
        catch { cameraErrorMessage = error.localizedDescription }
    }

    private func stopCameraIfNeeded() async {
        cameraService.stop()
    }

    private func prepareDailyReportIfNeeded() {
        guard settings.dailyReportEnabled else { return }
        let today = Calendar.current.startOfDay(for: Date())
        if let last = settings.lastReportShownDate, Calendar.current.isDate(last, inSameDayAs: today) {
            return
        }
        guard let report = DailyReportService.yesterdayReport() else { return }
        dailyReport = report
        showDailyReport = true
    }

    private func updateDynamicIslandIfNeeded(forceStart: Bool) {
        guard settings.dynamicIslandEnabled else {
            dynamicIsland.hide()
            return
        }

        let activeModes: Set<TimerMode> = [.focus, .break, .paused]
        guard activeModes.contains(timerService.mode) else {
            dynamicIsland.hide()
            return
        }

        if isWindowActive && !forceStart {
            dynamicIsland.hide()
            return
        }

        let title: String
        switch timerService.mode {
        case .focus: title = "专注中"
        case .break: title = "休息中"
        case .paused: title = "已暂停"
        default: title = ""
        }

        if forceStart || !dynamicIsland.isVisible {
            dynamicIsland.startCountdown(title: title, remaining: remainingTime)
        } else {
            dynamicIsland.updateCountdown(remainingTime)
        }
    }

    private func syncTimerDisplay() {
        remainingTime = timerService.formattedTime
        progress = timerService.progress
    }

    private func formatTime(_ seconds: Int) -> String {
        String(format: "%02d:%02d", seconds / 60, seconds % 60)
    }
}
