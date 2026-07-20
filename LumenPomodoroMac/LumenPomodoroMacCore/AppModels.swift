import Foundation

public enum TimerMode: String, Codable {
    case idle
    case focus
    case `break`
    case paused
}

public enum CameraAlertMode: String, Codable {
    case fixedDuration = "FixedDuration"
    case untilConfirm = "UntilConfirm"
    case followBreak = "FollowBreak"
}

public enum CameraAlertLevel: String, Codable {
    case light = "Light"
    case medium = "Medium"
    case severe = "Severe"
}

public enum InsightType: String, Codable {
    case peakHour = "PeakHour"
    case bestDay = "BestDay"
    case trend = "Trend"
    case streak = "Streak"
    case taskCompletion = "TaskCompletion"
    case motivation = "Motivation"
}

/// 洞察可触发的结构化动作类型。UI 据此渲染真实按钮，形成「洞察→行动」闭环，
/// 而非仅展示 actionHint 文案。
public enum SuggestedActionKind: String, Codable {
    /// 立即以指定科目开始一次专注
    case startFocus = "StartFocus"
    /// 把科目排到今日的某个时段（配合 DailyPlan，A2 使用）
    case scheduleBlock = "ScheduleBlock"
    /// 建议调整单次专注时长（分钟）
    case adjustDuration = "AdjustDuration"
    /// 跳转到某个设置项
    case openSettings = "OpenSettings"
}

/// 洞察附带的可执行动作。替代纯文本的 actionHint，使 UI 能渲染真实按钮。
/// 该对象为运行时计算产物，不入 JSON，故不触发 schema 迁移。
public struct SuggestedAction: Codable, Identifiable {
    public let id = UUID()
    public var kind: SuggestedActionKind
    /// 按钮文案，如「现在专注「数学」」
    public var actionLabel: String
    /// 关联科目名（startFocus / scheduleBlock 使用）
    public var taskName: String
    /// 建议时段（0-23），无则 -1（scheduleBlock 使用）
    public var preferredHour: Int
    /// 建议时长（分钟），无则 0（adjustDuration 使用）
    public var targetMinutes: Int
    /// 目标设置键（openSettings 使用）
    public var settingKey: String

    public init(kind: SuggestedActionKind, actionLabel: String,
                taskName: String = "", preferredHour: Int = -1, targetMinutes: Int = 0, settingKey: String = "") {
        self.kind = kind
        self.actionLabel = actionLabel
        self.taskName = taskName
        self.preferredHour = preferredHour
        self.targetMinutes = targetMinutes
        self.settingKey = settingKey
    }
}

public struct Settings: Codable, Equatable {
    public var schemaVersion: Int = 1
    public var workMinutes: Int = 25
    public var shortBreakMinutes: Int = 5
    public var longBreakMinutes: Int = 15
    public var longBreakInterval: Int = 4
    public var cameraAlertEnabled: Bool = false
    public var cameraAlertMode: CameraAlertMode = .untilConfirm
    public var cameraFixedOnSeconds: Int = 180
    public var cameraFollowBreakEnabled: Bool = true
    public var cameraIndex: Int = 0
    public var cameraAlertCanManualClose: Bool = true
    public var cameraAlertLevel: CameraAlertLevel = .medium
    public var hasShownCameraPrivacyNotice: Bool = false
    /// 是否完成首次产品引导
    public var hasCompletedOnboarding: Bool = false
    public var focusGuardEnabled: Bool = true
    public var focusGuardBlocklist: [String] = Settings.defaultBlocklist
    public var focusGuardIdleSeconds: Int = 180
    public var focusGuardPollSeconds: Int = 5
    /// 连续 poll 命中次数达到后才告警（防抖），默认 2
    public var focusGuardDebounceHits: Int = 2
    /// 单次专注最多走神通知次数，默认 3
    public var focusGuardMaxAlertsPerSession: Int = 3
    /// 系统勿扰时降级通知（预留）
    public var focusGuardRespectDoNotDisturb: Bool = true
    public var focusGuardAlertLevel: CameraAlertLevel = .severe
    public var dailyGoalMinutes: Int = 120
    public var weeklyGoalMinutes: Int = 600
    public var dailyTargetPomodoros: Int = 8
    public var soundEnabled: Bool = true
    public var popupEnabled: Bool = true
    public var systemNotificationEnabled: Bool = true
    public var menuBarEnabled: Bool = true
    public var launchAtLogin: Bool = false
    public var theme: String = "system"
    public var lastSelectedTaskId: String?
    public var examDate: Date?
    public var examName: String = "考研"
    public var examCountdownEnabled: Bool = true
    public var insightsEnabled: Bool = true
    public var dailyReportEnabled: Bool = true
    public var dynamicIslandEnabled: Bool = true
    /// keep | minimize | hide — 主窗口在前台时岛的行为
    public var dynamicIslandWhenFocused: String = "minimize"
    public var lastReportShownDate: Date?
    /// 计时进行中退出需确认
    public var confirmExitWhileFocusing: Bool = true
    /// 结束前预告秒数；0 = 关闭
    public var sessionEndPreNotifySeconds: Int = 30
    /// 休息时全屏遮罩
    public var fullscreenBreakEnabled: Bool = false
    /// 严格模式：禁止手动关灯、禁止提前结束休息
    public var strictModeEnabled: Bool = false
    public var language: String = "system"

    public var effectiveCameraAlertCanManualClose: Bool {
        !strictModeEnabled && cameraAlertCanManualClose
    }

    public var effectiveAllowEndBreakEarly: Bool {
        !strictModeEnabled
    }

    public init() {}

    public mutating func applyLightFocusPreset() {
        strictModeEnabled = false
        fullscreenBreakEnabled = false
        dynamicIslandEnabled = true
        dynamicIslandWhenFocused = "minimize"
        cameraAlertEnabled = false
        cameraAlertCanManualClose = true
        focusGuardEnabled = false
        confirmExitWhileFocusing = true
        sessionEndPreNotifySeconds = 30
        soundEnabled = true
        popupEnabled = true
        systemNotificationEnabled = true
    }

    public mutating func applyStandardFocusPreset() {
        strictModeEnabled = false
        fullscreenBreakEnabled = false
        dynamicIslandEnabled = true
        dynamicIslandWhenFocused = "minimize"
        cameraAlertEnabled = false
        cameraAlertCanManualClose = true
        focusGuardEnabled = true
        focusGuardAlertLevel = .medium
        confirmExitWhileFocusing = true
        if sessionEndPreNotifySeconds <= 0 {
            sessionEndPreNotifySeconds = 30
        }
        soundEnabled = true
        popupEnabled = true
        systemNotificationEnabled = true
    }

    /// 严格专注：严格 + 全屏休息 + 岛 keep（灯可选，默认关）
    public mutating func applyStrictFocusPreset() {
        strictModeEnabled = true
        fullscreenBreakEnabled = true
        dynamicIslandEnabled = true
        dynamicIslandWhenFocused = "keep"
        cameraAlertEnabled = false
        cameraAlertCanManualClose = false
        focusGuardEnabled = true
        focusGuardAlertLevel = .severe
        confirmExitWhileFocusing = true
        if sessionEndPreNotifySeconds <= 0 {
            sessionEndPreNotifySeconds = 30
        }
        soundEnabled = true
        popupEnabled = true
        systemNotificationEnabled = true
    }

    public mutating func applyFocusScenePreset(_ scene: String) {
        switch scene.lowercased() {
        case "light", "轻松":
            applyLightFocusPreset()
        case "strict", "严格", "严格专注":
            applyStrictFocusPreset()
        default:
            applyStandardFocusPreset()
        }
    }

    /// 摄像头灯可读状态
    public func cameraStatusDisplay(isActive: Bool, raw: String) -> String {
        if !cameraAlertEnabled {
            return "摄像头灯：关闭（设置中可开启，仅作硬件提醒）"
        }
        if isActive {
            let base = raw.isEmpty ? "摄像头灯：亮着 — 该休息了" : "摄像头灯：亮着 — \(raw)"
            return effectiveCameraAlertCanManualClose ? "\(base)（可手动关闭）" : "\(base)（严格模式不可手关）"
        }
        if raw.contains("失败") || raw.contains("错误") || raw.lowercased().contains("error") || raw.contains("权限") {
            return "摄像头灯：异常 — \(raw)。请在系统设置中允许摄像头后重试。"
        }
        return "摄像头灯：待命（专注结束后点亮）"
    }

    public static let defaultBlocklist = [
        "bilibili", "youtube", "抖音", "douyin", "微博", "weibo",
        "知乎", "zhihu", "WeChat", "Weixin", "微信", "QQ", "TikTok",
        "Steam", "网易云音乐", "爱奇艺", "腾讯视频", "优酷", "Safari"
    ]

    public enum CodingKeys: String, CodingKey {
        case schemaVersion = "SchemaVersion"
        case workMinutes = "WorkMinutes"
        case shortBreakMinutes = "ShortBreakMinutes"
        case longBreakMinutes = "LongBreakMinutes"
        case longBreakInterval = "LongBreakInterval"
        case cameraAlertEnabled = "CameraAlertEnabled"
        case cameraAlertMode = "CameraAlertMode"
        case cameraFixedOnSeconds = "CameraFixedOnSeconds"
        case cameraFollowBreakEnabled = "CameraFollowBreakEnabled"
        case cameraIndex = "CameraIndex"
        case cameraAlertCanManualClose = "CameraAlertCanManualClose"
        case cameraAlertLevel = "CameraAlertLevel"
        case hasShownCameraPrivacyNotice = "HasShownCameraPrivacyNotice"
        case hasCompletedOnboarding = "HasCompletedOnboarding"
        case focusGuardEnabled = "FocusGuardEnabled"
        case focusGuardBlocklist = "FocusGuardBlocklist"
        case focusGuardIdleSeconds = "FocusGuardIdleSeconds"
        case focusGuardPollSeconds = "FocusGuardPollSeconds"
        case focusGuardDebounceHits = "FocusGuardDebounceHits"
        case focusGuardMaxAlertsPerSession = "FocusGuardMaxAlertsPerSession"
        case focusGuardRespectDoNotDisturb = "FocusGuardRespectDoNotDisturb"
        case focusGuardAlertLevel = "FocusGuardAlertLevel"
        case dailyGoalMinutes = "DailyGoalMinutes"
        case weeklyGoalMinutes = "WeeklyGoalMinutes"
        case dailyTargetPomodoros = "DailyTargetPomodoros"
        case soundEnabled = "SoundEnabled"
        case popupEnabled = "PopupEnabled"
        case systemNotificationEnabled = "SystemNotificationEnabled"
        case menuBarEnabled = "MenuBarEnabled"
        case launchAtLogin = "LaunchAtLogin"
        case autoStartEnabled = "AutoStartEnabled"
        case theme = "Theme"
        case lastSelectedTaskId = "LastSelectedTaskId"
        case examDate = "ExamDate"
        case examName = "ExamName"
        case examCountdownEnabled = "ExamCountdownEnabled"
        case insightsEnabled = "InsightsEnabled"
        case dailyReportEnabled = "DailyReportEnabled"
        case dynamicIslandEnabled = "DynamicIslandEnabled"
        case dynamicIslandWhenFocused = "DynamicIslandWhenFocused"
        case lastReportShownDate = "LastReportShownDate"
        case confirmExitWhileFocusing = "ConfirmExitWhileFocusing"
        case sessionEndPreNotifySeconds = "SessionEndPreNotifySeconds"
        case fullscreenBreakEnabled = "FullscreenBreakEnabled"
        case strictModeEnabled = "StrictModeEnabled"
        case language = "Language"
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        schemaVersion = try c.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? 1
        workMinutes = try c.decodeIfPresent(Int.self, forKey: .workMinutes) ?? 25
        shortBreakMinutes = try c.decodeIfPresent(Int.self, forKey: .shortBreakMinutes) ?? 5
        longBreakMinutes = try c.decodeIfPresent(Int.self, forKey: .longBreakMinutes) ?? 15
        longBreakInterval = try c.decodeIfPresent(Int.self, forKey: .longBreakInterval) ?? 4
        cameraAlertEnabled = try c.decodeIfPresent(Bool.self, forKey: .cameraAlertEnabled) ?? false
        cameraAlertMode = try c.decodeIfPresent(CameraAlertMode.self, forKey: .cameraAlertMode) ?? .untilConfirm
        cameraFixedOnSeconds = try c.decodeIfPresent(Int.self, forKey: .cameraFixedOnSeconds) ?? 180
        cameraFollowBreakEnabled = try c.decodeIfPresent(Bool.self, forKey: .cameraFollowBreakEnabled) ?? true
        cameraIndex = try c.decodeIfPresent(Int.self, forKey: .cameraIndex) ?? 0
        cameraAlertCanManualClose = try c.decodeIfPresent(Bool.self, forKey: .cameraAlertCanManualClose) ?? true
        cameraAlertLevel = try c.decodeIfPresent(CameraAlertLevel.self, forKey: .cameraAlertLevel) ?? .medium
        hasShownCameraPrivacyNotice = try c.decodeIfPresent(Bool.self, forKey: .hasShownCameraPrivacyNotice) ?? false
        hasCompletedOnboarding = try c.decodeIfPresent(Bool.self, forKey: .hasCompletedOnboarding) ?? false
        focusGuardEnabled = try c.decodeIfPresent(Bool.self, forKey: .focusGuardEnabled) ?? true
        focusGuardBlocklist = try c.decodeIfPresent([String].self, forKey: .focusGuardBlocklist) ?? Settings.defaultBlocklist
        focusGuardIdleSeconds = try c.decodeIfPresent(Int.self, forKey: .focusGuardIdleSeconds) ?? 180
        focusGuardPollSeconds = try c.decodeIfPresent(Int.self, forKey: .focusGuardPollSeconds) ?? 5
        focusGuardDebounceHits = try c.decodeIfPresent(Int.self, forKey: .focusGuardDebounceHits) ?? 2
        focusGuardMaxAlertsPerSession = try c.decodeIfPresent(Int.self, forKey: .focusGuardMaxAlertsPerSession) ?? 3
        focusGuardRespectDoNotDisturb = try c.decodeIfPresent(Bool.self, forKey: .focusGuardRespectDoNotDisturb) ?? true
        focusGuardAlertLevel = try c.decodeIfPresent(CameraAlertLevel.self, forKey: .focusGuardAlertLevel) ?? .severe
        dailyGoalMinutes = try c.decodeIfPresent(Int.self, forKey: .dailyGoalMinutes) ?? 120
        weeklyGoalMinutes = try c.decodeIfPresent(Int.self, forKey: .weeklyGoalMinutes) ?? 600
        dailyTargetPomodoros = try c.decodeIfPresent(Int.self, forKey: .dailyTargetPomodoros) ?? 8
        soundEnabled = try c.decodeIfPresent(Bool.self, forKey: .soundEnabled) ?? true
        popupEnabled = try c.decodeIfPresent(Bool.self, forKey: .popupEnabled) ?? true
        systemNotificationEnabled = try c.decodeIfPresent(Bool.self, forKey: .systemNotificationEnabled) ?? true
        menuBarEnabled = try c.decodeIfPresent(Bool.self, forKey: .menuBarEnabled) ?? true
        let autoStart = try c.decodeIfPresent(Bool.self, forKey: .autoStartEnabled)
        launchAtLogin = try c.decodeIfPresent(Bool.self, forKey: .launchAtLogin) ?? autoStart ?? false
        theme = try c.decodeIfPresent(String.self, forKey: .theme) ?? "system"
        lastSelectedTaskId = try c.decodeIfPresent(String.self, forKey: .lastSelectedTaskId)
        examDate = try c.decodeIfPresent(Date.self, forKey: .examDate)
        examName = try c.decodeIfPresent(String.self, forKey: .examName) ?? "考研"
        examCountdownEnabled = try c.decodeIfPresent(Bool.self, forKey: .examCountdownEnabled) ?? true
        insightsEnabled = try c.decodeIfPresent(Bool.self, forKey: .insightsEnabled) ?? true
        dailyReportEnabled = try c.decodeIfPresent(Bool.self, forKey: .dailyReportEnabled) ?? true
        dynamicIslandEnabled = try c.decodeIfPresent(Bool.self, forKey: .dynamicIslandEnabled) ?? true
        dynamicIslandWhenFocused = try c.decodeIfPresent(String.self, forKey: .dynamicIslandWhenFocused) ?? "minimize"
        lastReportShownDate = try c.decodeIfPresent(Date.self, forKey: .lastReportShownDate)
        confirmExitWhileFocusing = try c.decodeIfPresent(Bool.self, forKey: .confirmExitWhileFocusing) ?? true
        sessionEndPreNotifySeconds = try c.decodeIfPresent(Int.self, forKey: .sessionEndPreNotifySeconds) ?? 30
        fullscreenBreakEnabled = try c.decodeIfPresent(Bool.self, forKey: .fullscreenBreakEnabled) ?? false
        strictModeEnabled = try c.decodeIfPresent(Bool.self, forKey: .strictModeEnabled) ?? false
        language = try c.decodeIfPresent(String.self, forKey: .language) ?? "system"
    }

    public func encode(to encoder: Encoder) throws {
        var c = encoder.container(keyedBy: CodingKeys.self)
        try c.encode(schemaVersion, forKey: .schemaVersion)
        try c.encode(workMinutes, forKey: .workMinutes)
        try c.encode(shortBreakMinutes, forKey: .shortBreakMinutes)
        try c.encode(longBreakMinutes, forKey: .longBreakMinutes)
        try c.encode(longBreakInterval, forKey: .longBreakInterval)
        try c.encode(cameraAlertEnabled, forKey: .cameraAlertEnabled)
        try c.encode(cameraAlertMode, forKey: .cameraAlertMode)
        try c.encode(cameraFixedOnSeconds, forKey: .cameraFixedOnSeconds)
        try c.encode(cameraFollowBreakEnabled, forKey: .cameraFollowBreakEnabled)
        try c.encode(cameraIndex, forKey: .cameraIndex)
        try c.encode(cameraAlertCanManualClose, forKey: .cameraAlertCanManualClose)
        try c.encode(cameraAlertLevel, forKey: .cameraAlertLevel)
        try c.encode(hasShownCameraPrivacyNotice, forKey: .hasShownCameraPrivacyNotice)
        try c.encode(hasCompletedOnboarding, forKey: .hasCompletedOnboarding)
        try c.encode(focusGuardEnabled, forKey: .focusGuardEnabled)
        try c.encode(focusGuardBlocklist, forKey: .focusGuardBlocklist)
        try c.encode(focusGuardIdleSeconds, forKey: .focusGuardIdleSeconds)
        try c.encode(focusGuardPollSeconds, forKey: .focusGuardPollSeconds)
        try c.encode(focusGuardDebounceHits, forKey: .focusGuardDebounceHits)
        try c.encode(focusGuardMaxAlertsPerSession, forKey: .focusGuardMaxAlertsPerSession)
        try c.encode(focusGuardRespectDoNotDisturb, forKey: .focusGuardRespectDoNotDisturb)
        try c.encode(focusGuardAlertLevel, forKey: .focusGuardAlertLevel)
        try c.encode(dailyGoalMinutes, forKey: .dailyGoalMinutes)
        try c.encode(weeklyGoalMinutes, forKey: .weeklyGoalMinutes)
        try c.encode(dailyTargetPomodoros, forKey: .dailyTargetPomodoros)
        try c.encode(soundEnabled, forKey: .soundEnabled)
        try c.encode(popupEnabled, forKey: .popupEnabled)
        try c.encode(systemNotificationEnabled, forKey: .systemNotificationEnabled)
        try c.encode(menuBarEnabled, forKey: .menuBarEnabled)
        try c.encode(launchAtLogin, forKey: .launchAtLogin)
        try c.encode(launchAtLogin, forKey: .autoStartEnabled)
        try c.encode(theme, forKey: .theme)
        try c.encodeIfPresent(lastSelectedTaskId, forKey: .lastSelectedTaskId)
        try c.encodeIfPresent(examDate, forKey: .examDate)
        try c.encode(examName, forKey: .examName)
        try c.encode(examCountdownEnabled, forKey: .examCountdownEnabled)
        try c.encode(insightsEnabled, forKey: .insightsEnabled)
        try c.encode(dailyReportEnabled, forKey: .dailyReportEnabled)
        try c.encode(dynamicIslandEnabled, forKey: .dynamicIslandEnabled)
        try c.encode(dynamicIslandWhenFocused, forKey: .dynamicIslandWhenFocused)
        try c.encodeIfPresent(lastReportShownDate, forKey: .lastReportShownDate)
        try c.encode(confirmExitWhileFocusing, forKey: .confirmExitWhileFocusing)
        try c.encode(sessionEndPreNotifySeconds, forKey: .sessionEndPreNotifySeconds)
        try c.encode(fullscreenBreakEnabled, forKey: .fullscreenBreakEnabled)
        try c.encode(strictModeEnabled, forKey: .strictModeEnabled)
        try c.encode(language, forKey: .language)
    }
}

public struct TaskItem: Codable, Identifiable, Equatable {
    public var id: String = UUID().uuidString
    public var name: String = ""
    public var category: String = ""
    public var color: String = "#3B82F6"
    public var createdAt: Date = Date()

    public init(id: String = UUID().uuidString, name: String = "", category: String = "", color: String = "#3B82F6", createdAt: Date = Date()) {
        self.id = id
        self.name = name
        self.category = category
        self.color = color
        self.createdAt = createdAt
    }

    public enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
        case category = "Category"
        case color = "Color"
        case createdAt = "CreatedAt"
    }
}

public struct FocusSession: Codable, Identifiable {
    public var id: String = UUID().uuidString
    public var taskId: String = ""
    public var taskName: String = ""
    public var startTime: Date = Date()
    public var endTime: Date?
    public var focusMinutes: Int = 25
    public var completed: Bool = false
    public var notes: String?
    public var qualityScore: Int = 0

    public init(id: String = UUID().uuidString, taskId: String = "", taskName: String = "", startTime: Date = Date(), endTime: Date? = nil, focusMinutes: Int = 25, completed: Bool = false, notes: String? = nil, qualityScore: Int = 0) {
        self.id = id
        self.taskId = taskId
        self.taskName = taskName
        self.startTime = startTime
        self.endTime = endTime
        self.focusMinutes = focusMinutes
        self.completed = completed
        self.notes = notes
        self.qualityScore = qualityScore
    }

    public enum CodingKeys: String, CodingKey {
        case id = "Id"
        case taskId = "TaskId"
        case taskName = "TaskName"
        case startTime = "StartTime"
        case endTime = "EndTime"
        case focusMinutes = "FocusMinutes"
        case completed = "Completed"
        case notes = "Notes"
        case qualityScore = "QualityScore"
    }
}

/// 今日计划中的一个时段块。配合「峰值时段排程」（A2）：
/// 洞察建议把某科目排到某个时段，落盘到 dailyplan.json，形成可执行的今日计划。
public struct PlannedBlock: Codable, Identifiable {
    public var id: String
    /// 关联科目名
    public var taskName: String
    /// 计划时段（0-23）
    public var hour: Int
    /// 计划专注分钟（默认取单次工作分钟）
    public var durationMinutes: Int

    public init(id: String = UUID().uuidString, taskName: String = "", hour: Int = 0, durationMinutes: Int = 25) {
        self.id = id
        self.taskName = taskName
        self.hour = hour
        self.durationMinutes = durationMinutes
    }

    enum CodingKeys: String, CodingKey {
        case id = "Id"
        case taskName = "TaskName"
        case hour = "Hour"
        case durationMinutes = "DurationMinutes"
    }
}

/// 某一天的专注计划；按日期存储，跨天后自动重置。
/// 该对象会落盘（dailyplan.json），故触发 schema 迁移（V2）。
public struct DailyPlan: Codable {
    public var date: Date
    public var blocks: [PlannedBlock]

    public init(date: Date = Date(), blocks: [PlannedBlock] = []) {
        self.date = date
        self.blocks = blocks
    }

    enum CodingKeys: String, CodingKey {
        case date = "Date"
        case blocks = "Blocks"
    }
}

public struct DailyStats {
    public var completedPomodoros: Int = 0
    public var totalFocusMinutes: Int = 0
    public var currentStreak: Int = 0

    public init(completedPomodoros: Int = 0, totalFocusMinutes: Int = 0, currentStreak: Int = 0) {
        self.completedPomodoros = completedPomodoros
        self.totalFocusMinutes = totalFocusMinutes
        self.currentStreak = currentStreak
    }
}

public struct DailyReport: Identifiable {
    public let id = UUID()
    public var date: Date
    public var completedPomodoros: Int
    public var totalMinutes: Int
    public var mainTask: String
    public var streakDays: Int
    public var avgQualityScore: Double
    public var uniqueTasksCount: Int
    public var categorySuggestion: String

    public init(date: Date, completedPomodoros: Int, totalMinutes: Int, mainTask: String, streakDays: Int, avgQualityScore: Double, uniqueTasksCount: Int, categorySuggestion: String) {
        self.date = date
        self.completedPomodoros = completedPomodoros
        self.totalMinutes = totalMinutes
        self.mainTask = mainTask
        self.streakDays = streakDays
        self.avgQualityScore = avgQualityScore
        self.uniqueTasksCount = uniqueTasksCount
        self.categorySuggestion = categorySuggestion
    }
}

public struct Insight: Identifiable {
    public let id = UUID()
    public var title: String
    public var description: String
    public var actionHint: String
    public var type: InsightType
    /// 结构化可执行动作；为 nil 时 UI 仅展示说明
    public var action: SuggestedAction?

    public init(title: String, description: String, actionHint: String, type: InsightType, action: SuggestedAction? = nil) {
        self.title = title
        self.description = description
        self.actionHint = actionHint
        self.type = type
        self.action = action
    }
}

public struct HeatmapDay: Identifiable {
    public var id: Date { date }
    public var date: Date
    public var focusMinutes: Int
    public var intensityLevel: Int

    public init(date: Date, focusMinutes: Int, intensityLevel: Int) {
        self.date = date
        self.focusMinutes = focusMinutes
        self.intensityLevel = intensityLevel
    }
}

public struct HourlyDataPoint: Identifiable {
    public var id: Int { hour }
    public var hour: Int
    public var totalMinutes: Int
    public var sessionCount: Int

    public init(hour: Int, totalMinutes: Int, sessionCount: Int) {
        self.hour = hour
        self.totalMinutes = totalMinutes
        self.sessionCount = sessionCount
    }
}

public struct WeeklyDataPoint: Identifiable {
    public var id: Date { weekStart }
    public var weekStart: Date
    public var totalMinutes: Int
    public var completedPomodoros: Int

    public init(weekStart: Date, totalMinutes: Int, completedPomodoros: Int) {
        self.weekStart = weekStart
        self.totalMinutes = totalMinutes
        self.completedPomodoros = completedPomodoros
    }
}

public struct TaskSlice: Identifiable {
    public var id: String { taskName }
    public var taskName: String
    public var taskColor: String
    public var pomodoroCount: Int
    public var percentage: Double

    public init(taskName: String, taskColor: String, pomodoroCount: Int, percentage: Double) {
        self.taskName = taskName
        self.taskColor = taskColor
        self.pomodoroCount = pomodoroCount
        self.percentage = percentage
    }
}

public struct GoalProgress: Identifiable {
    public var id: String { label }
    public var label: String
    public var currentMinutes: Int
    public var targetMinutes: Int
    public var progressPercent: Double
    public var isCompleted: Bool

    public init(label: String, currentMinutes: Int, targetMinutes: Int, progressPercent: Double, isCompleted: Bool) {
        self.label = label
        self.currentMinutes = currentMinutes
        self.targetMinutes = targetMinutes
        self.progressPercent = progressPercent
        self.isCompleted = isCompleted
    }
}

public struct PomodoroPreset: Identifiable {
    public let id = UUID()
    public let name: String
    public let work: Int
    public let shortBreak: Int
    public let longBreak: Int

    public init(name: String, work: Int, shortBreak: Int, longBreak: Int) {
        self.name = name
        self.work = work
        self.shortBreak = shortBreak
        self.longBreak = longBreak
    }
}

public extension PomodoroPreset {
    static let standard = PomodoroPreset(name: "标准", work: 25, shortBreak: 5, longBreak: 15)
    static let deep = PomodoroPreset(name: "深度", work: 50, shortBreak: 10, longBreak: 20)
    static let sprint = PomodoroPreset(name: "冲刺", work: 15, shortBreak: 3, longBreak: 10)
    static let all: [PomodoroPreset] = [.standard, .deep, .sprint]
}
