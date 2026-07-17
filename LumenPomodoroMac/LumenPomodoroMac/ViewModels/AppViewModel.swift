import LumenPomodoroMacCore
import AppKit
import Foundation
import SwiftUI

@MainActor
final class AppViewModel: ObservableObject {
    @Published var settings: LumenPomodoroMacCore.Settings
    @Published var tasks: [TaskItem] = []
    @Published var selectedTask: TaskItem?
    @Published var todayStats = DailyStats()
    @Published var remainingTime = "25:00"
    @Published var progress: Double = 1.0
    @Published var isFocusCompleted = false
    @Published var currentNotes = ""
    @Published var userRating = 0
    @Published var showPrivacySheet = false
    @Published var showOnboarding = false
    @Published var cameraErrorMessage: String?
    @Published var showCameraErrorOfferSettings = false
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
    /// 最近完成会话 id，用于评分/笔记事后写入
    private var lastCompletedSessionId: String?
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
        bindDynamicIslandActions()
        reloadData()
        NotificationService.shared.requestAuthorizationIfNeeded()
        syncLaunchAtLoginFromSettings()
        prepareDailyReportIfNeeded()

        if !settings.hasCompletedOnboarding {
            showOnboarding = true
        } else if settings.cameraAlertEnabled && !settings.hasShownCameraPrivacyNotice {
            showPrivacySheet = true
        }
    }

    private func bindDynamicIslandActions() {
        dynamicIsland.onPause = { [weak self] in self?.togglePause() }
        dynamicIsland.onResume = { [weak self] in self?.togglePause() }
        dynamicIsland.onSkipBreak = { [weak self] in self?.skipBreak() }
        dynamicIsland.onStartFocus = { [weak self] in self?.startFocus() }
        dynamicIsland.onSelectTask = { [weak self] id in
            guard let self, let task = self.tasks.first(where: { $0.id == id }) else { return }
            self.selectTask(task)
            self.syncIslandTasks()
        }
        dynamicIsland.onOpenMain = {
            NSApplication.shared.activate(ignoringOtherApps: true)
            NSApplication.shared.windows.first { $0.canBecomeMain }?.makeKeyAndOrderFront(nil)
        }
    }

    private func syncIslandTasks() {
        let chips = tasks.prefix(8).map {
            IslandTaskItem(id: $0.id, name: $0.name, color: $0.color.isEmpty ? "#3B82F6" : $0.color)
        }
        dynamicIsland.setTasks(Array(chips), selectedId: selectedTask?.id)
    }

    var cameraStatusDisplay: String {
        settings.cameraStatusDisplay(isActive: isCameraAlertActive, raw: cameraStatus)
    }

    func completeOnboarding(scene: String) {
        var next = settings
        next.applyFocusScenePreset(scene)
        next.dynamicIslandEnabled = true
        if next.cameraAlertEnabled {
            next.hasShownCameraPrivacyNotice = true
        }
        next.hasCompletedOnboarding = true
        settings = next
        saveSettings()
        showOnboarding = false
    }

    func applyFocusScenePreset(_ scene: String, announce: Bool = true) {
        var next = settings
        next.applyFocusScenePreset(scene)
        if next.cameraAlertEnabled {
            next.hasShownCameraPrivacyNotice = true
        }
        settings = next
        saveSettings()

        guard announce else { return }
        let name: String
        switch scene.lowercased() {
        case "light", "轻松": name = "轻松"
        case "strict", "严格", "严格专注": name = "严格专注"
        default: name = "标准"
        }
        let alert = NSAlert()
        alert.messageText = "场景预设"
        alert.informativeText = "已应用「\(name)」场景。时长与任务未改动。"
        alert.alertStyle = .informational
        alert.addButton(withTitle: "好")
        alert.runModal()
    }

    func onAppBecameActive() {
        isWindowActive = true
        dynamicIsland.mainWindowFocused = true
        dynamicIsland.whenFocused = settings.dynamicIslandWhenFocused
        syncIslandTasks()
        updateDynamicIslandIfNeeded(forceStart: false)
    }

    func onAppResignedActive() {
        isWindowActive = false
        dynamicIsland.mainWindowFocused = false
        dynamicIsland.whenFocused = settings.dynamicIslandWhenFocused
        syncIslandTasks()
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
        dynamicIsland.whenFocused = settings.dynamicIslandWhenFocused
        updateDynamicIslandIfNeeded(forceStart: false)
        objectWillChange.send()
    }

    func applyStrictFocusPreset() {
        applyFocusScenePreset("strict")
    }

    func replayOnboarding() {
        settings.hasCompletedOnboarding = false
        saveSettings()
        showOnboarding = true
    }

    func openCameraPrivacySettings() {
        SystemSettingsOpener.openCameraPrivacy()
    }

    func presentCameraError(_ message: String) {
        cameraErrorMessage = message
        showCameraErrorOfferSettings = true
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
        persistReviewToLastSession()
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

        persistReviewToLastSession()
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
        guard let id = lastCompletedSessionId else { return }
        var sessions = storage.loadSessions()
        guard let index = sessions.firstIndex(where: { $0.id == id }) else { return }
        sessions[index].qualityScore = rating
        storage.saveSessions(sessions)
    }

    /// 将完成面板上的评分/笔记写回最近会话（开始/跳过休息时调用）
    func persistReviewToLastSession() {
        guard let id = lastCompletedSessionId else { return }
        var sessions = storage.loadSessions()
        guard let index = sessions.firstIndex(where: { $0.id == id }) else { return }
        if userRating > 0 {
            sessions[index].qualityScore = userRating
        }
        let notes = currentNotes.trimmingCharacters(in: .whitespacesAndNewlines)
        if !notes.isEmpty {
            sessions[index].notes = notes
        }
        storage.saveSessions(sessions)
        lastCompletedSessionId = nil
        currentNotes = ""
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
        dynamicIsland.showNotification(title: "即将结束", message: "还剩 \(remainingSeconds) 秒")
        NotificationService.shared.show(
            title: "即将结束",
            body: "还剩 \(remainingSeconds) 秒",
            enabled: settings.systemNotificationEnabled && !isWindowActive
        )
        if settings.soundEnabled {
            SoundService.shared.playCompletion(enabled: true)
        }
    }

    private func handleFocusGuardDistraction(_ reason: String) {
        // 次数上限已在 FocusGuardService 内执行；优先 Transient 岛
        dynamicIsland.showNotification(title: "走神提醒", message: reason)
        NotificationService.shared.show(title: "走神提醒", body: reason, enabled: !isWindowActive)
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
            // 笔记/评分通常在完成后填写，此处先落盘会话，事后由 setRating / persistReview 更新
            session.notes = currentNotes.isEmpty ? nil : currentNotes
            session.qualityScore = userRating
            storage.appendSession(session)
            lastCompletedSessionId = session.id
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

        // 完成事件走 Transient（岛优先）
        dynamicIsland.showNotification(title: "专注完成", message: lastCompletedSummary)

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
        // 完成态不立刻 hide：Transient 由岛自行回收
    }

    private func handleBreakCompleted() {
        focusGuard.stop()
        showFullscreenBreak = false
        SoundService.shared.playCompletion(enabled: settings.soundEnabled)
        dynamicIsland.showNotification(title: "休息结束", message: "可以开始下一轮专注了。")
        NotificationService.shared.show(
            title: "休息结束",
            body: "可以开始下一轮专注了。",
            enabled: settings.systemNotificationEnabled && !isWindowActive
        )
        Task {
            await stopCameraIfNeeded()
        }
        timerService.configureIdle(minutes: settings.workMinutes)
        syncTimerDisplay()
    }

    private func triggerCameraAlertAfterFocus() async {
        guard settings.cameraAlertEnabled else { return }
        switch settings.cameraAlertMode {
        case .fixedDuration:
            do {
                try await cameraService.startForDuration(seconds: settings.cameraFixedOnSeconds)
            } catch {
                presentCameraError(error.localizedDescription)
            }
        case .untilConfirm, .followBreak:
            do { try await cameraService.start() }
            catch { presentCameraError(error.localizedDescription) }
        }
    }

    private func startCameraForBreak() async {
        guard settings.cameraAlertEnabled else { return }
        do { try await cameraService.start() }
        catch { presentCameraError(error.localizedDescription) }
    }

    private func stopCameraIfNeeded() async {
        cameraService.stop()
    }

    private func prepareDailyReportIfNeeded() {
        guard settings.hasCompletedOnboarding else { return }
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
        dynamicIsland.whenFocused = settings.dynamicIslandWhenFocused
        dynamicIsland.mainWindowFocused = isWindowActive
        syncIslandTasks()

        guard settings.dynamicIslandEnabled else {
            dynamicIsland.hide()
            return
        }

        // hide 策略：前台时隐藏（Transient 仍由 showNotification 强制显示）
        if isWindowActive && settings.dynamicIslandWhenFocused.lowercased() == "hide" && !forceStart {
            if dynamicIsland.mode != .transient {
                dynamicIsland.hide()
            }
            return
        }

        let mode = timerService.mode
        let activeModes: Set<TimerMode> = [.focus, .break, .paused]

        // Idle：失焦时显示待命岛（选任务 + 开始）
        if mode == .idle {
            if !isWindowActive || forceStart {
                let label = selectedTask?.name ?? "选择任务"
                let ready = String(format: "%02d:00", max(1, settings.workMinutes))
                dynamicIsland.showIdleReady(taskLabel: label, readyTime: ready)
            } else if settings.dynamicIslandWhenFocused.lowercased() == "hide" {
                dynamicIsland.hide()
            }
            return
        }

        guard activeModes.contains(mode) else {
            dynamicIsland.hide()
            return
        }

        let title = DynamicIslandService.title(for: mode)
        if forceStart || !dynamicIsland.isVisible {
            dynamicIsland.startCountdown(title: title, remaining: remainingTime, session: mode)
        } else {
            dynamicIsland.updateCountdown(remainingTime, session: mode)
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
