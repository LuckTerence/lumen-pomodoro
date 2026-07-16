import Foundation

enum TimerMode: String, Codable {
    case idle
    case focus
    case `break`
    case paused
}

enum CameraAlertMode: String, Codable {
    case fixedDuration = "FixedDuration"
    case untilConfirm = "UntilConfirm"
    case followBreak = "FollowBreak"
}

enum CameraAlertLevel: String, Codable {
    case light = "Light"
    case medium = "Medium"
    case severe = "Severe"
}

enum InsightType: String, Codable {
    case peakHour = "PeakHour"
    case bestDay = "BestDay"
    case trend = "Trend"
    case streak = "Streak"
    case taskCompletion = "TaskCompletion"
    case motivation = "Motivation"
}

struct Settings: Codable, Equatable {
    var schemaVersion: Int = 1
    var workMinutes: Int = 25
    var shortBreakMinutes: Int = 5
    var longBreakMinutes: Int = 15
    var longBreakInterval: Int = 4
    var cameraAlertEnabled: Bool = false
    var cameraAlertMode: CameraAlertMode = .untilConfirm
    var cameraFixedOnSeconds: Int = 180
    var cameraFollowBreakEnabled: Bool = true
    var cameraIndex: Int = 0
    var cameraAlertCanManualClose: Bool = true
    var cameraAlertLevel: CameraAlertLevel = .medium
    var hasShownCameraPrivacyNotice: Bool = false
    var focusGuardEnabled: Bool = true
    var focusGuardBlocklist: [String] = Settings.defaultBlocklist
    var focusGuardIdleSeconds: Int = 180
    var focusGuardPollSeconds: Int = 5
    /// 连续 poll 命中次数达到后才告警（防抖），默认 2
    var focusGuardDebounceHits: Int = 2
    /// 单次专注最多走神通知次数，默认 3
    var focusGuardMaxAlertsPerSession: Int = 3
    /// 系统勿扰时降级通知（预留）
    var focusGuardRespectDoNotDisturb: Bool = true
    var focusGuardAlertLevel: CameraAlertLevel = .severe
    var dailyGoalMinutes: Int = 120
    var weeklyGoalMinutes: Int = 600
    var dailyTargetPomodoros: Int = 8
    var soundEnabled: Bool = true
    var popupEnabled: Bool = true
    var systemNotificationEnabled: Bool = true
    var menuBarEnabled: Bool = true
    var launchAtLogin: Bool = false
    var theme: String = "system"
    var lastSelectedTaskId: String?
    var examDate: Date?
    var examName: String = "考研"
    var examCountdownEnabled: Bool = true
    var insightsEnabled: Bool = true
    var dailyReportEnabled: Bool = true
    var dynamicIslandEnabled: Bool = true
    var lastReportShownDate: Date?
    /// 计时进行中退出需确认
    var confirmExitWhileFocusing: Bool = true
    /// 结束前预告秒数；0 = 关闭
    var sessionEndPreNotifySeconds: Int = 30
    /// 休息时全屏遮罩
    var fullscreenBreakEnabled: Bool = false
    /// 严格模式：禁止手动关灯、禁止提前结束休息
    var strictModeEnabled: Bool = false
    var language: String = "system"

    var effectiveCameraAlertCanManualClose: Bool {
        !strictModeEnabled && cameraAlertCanManualClose
    }

    var effectiveAllowEndBreakEarly: Bool {
        !strictModeEnabled
    }

    /// 严格专注一键预设：严格 + 全屏休息 + 摄像头灯（不改时长/任务）
    mutating func applyStrictFocusPreset() {
        strictModeEnabled = true
        fullscreenBreakEnabled = true
        cameraAlertEnabled = true
        cameraAlertMode = .untilConfirm
        cameraAlertLevel = .severe
        cameraAlertCanManualClose = false
        cameraFollowBreakEnabled = true
        confirmExitWhileFocusing = true
        if sessionEndPreNotifySeconds <= 0 {
            sessionEndPreNotifySeconds = 30
        }
        soundEnabled = true
        popupEnabled = true
        systemNotificationEnabled = true
    }

    static let defaultBlocklist = [
        "bilibili", "youtube", "抖音", "douyin", "微博", "weibo",
        "知乎", "zhihu", "WeChat", "Weixin", "微信", "QQ", "TikTok",
        "Steam", "网易云音乐", "爱奇艺", "腾讯视频", "优酷", "Safari"
    ]

    enum CodingKeys: String, CodingKey {
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
        case lastReportShownDate = "LastReportShownDate"
        case confirmExitWhileFocusing = "ConfirmExitWhileFocusing"
        case sessionEndPreNotifySeconds = "SessionEndPreNotifySeconds"
        case fullscreenBreakEnabled = "FullscreenBreakEnabled"
        case strictModeEnabled = "StrictModeEnabled"
        case language = "Language"
    }

    init() {}

    init(from decoder: Decoder) throws {
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
        lastReportShownDate = try c.decodeIfPresent(Date.self, forKey: .lastReportShownDate)
        confirmExitWhileFocusing = try c.decodeIfPresent(Bool.self, forKey: .confirmExitWhileFocusing) ?? true
        sessionEndPreNotifySeconds = try c.decodeIfPresent(Int.self, forKey: .sessionEndPreNotifySeconds) ?? 30
        fullscreenBreakEnabled = try c.decodeIfPresent(Bool.self, forKey: .fullscreenBreakEnabled) ?? false
        strictModeEnabled = try c.decodeIfPresent(Bool.self, forKey: .strictModeEnabled) ?? false
        language = try c.decodeIfPresent(String.self, forKey: .language) ?? "system"
    }

    func encode(to encoder: Encoder) throws {
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
        try c.encodeIfPresent(lastReportShownDate, forKey: .lastReportShownDate)
        try c.encode(confirmExitWhileFocusing, forKey: .confirmExitWhileFocusing)
        try c.encode(sessionEndPreNotifySeconds, forKey: .sessionEndPreNotifySeconds)
        try c.encode(fullscreenBreakEnabled, forKey: .fullscreenBreakEnabled)
        try c.encode(strictModeEnabled, forKey: .strictModeEnabled)
        try c.encode(language, forKey: .language)
    }
}

struct TaskItem: Codable, Identifiable, Equatable {
    var id: String = UUID().uuidString
    var name: String = ""
    var category: String = ""
    var color: String = "#3B82F6"
    var createdAt: Date = Date()

    enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
        case category = "Category"
        case color = "Color"
        case createdAt = "CreatedAt"
    }
}

struct FocusSession: Codable, Identifiable {
    var id: String = UUID().uuidString
    var taskId: String = ""
    var taskName: String = ""
    var startTime: Date = Date()
    var endTime: Date?
    var focusMinutes: Int = 25
    var completed: Bool = false
    var notes: String?
    var qualityScore: Int = 0

    enum CodingKeys: String, CodingKey {
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

struct DailyStats {
    var completedPomodoros: Int = 0
    var totalFocusMinutes: Int = 0
    var currentStreak: Int = 0
}

struct DailyReport: Identifiable {
    let id = UUID()
    var date: Date
    var completedPomodoros: Int
    var totalMinutes: Int
    var mainTask: String
    var streakDays: Int
    var avgQualityScore: Double
    var uniqueTasksCount: Int
    var categorySuggestion: String
}

struct Insight: Identifiable {
    let id = UUID()
    var title: String
    var description: String
    var actionHint: String
    var type: InsightType
}

struct HeatmapDay: Identifiable {
    var id: Date { date }
    var date: Date
    var focusMinutes: Int
    var intensityLevel: Int
}

struct HourlyDataPoint: Identifiable {
    var id: Int { hour }
    var hour: Int
    var totalMinutes: Int
    var sessionCount: Int
}

struct WeeklyDataPoint: Identifiable {
    var id: Date { weekStart }
    var weekStart: Date
    var totalMinutes: Int
    var completedPomodoros: Int
}

struct TaskSlice: Identifiable {
    var id: String { taskName }
    var taskName: String
    var taskColor: String
    var pomodoroCount: Int
    var percentage: Double
}

struct GoalProgress: Identifiable {
    var id: String { label }
    var label: String
    var currentMinutes: Int
    var targetMinutes: Int
    var progressPercent: Double
    var isCompleted: Bool
}

struct PomodoroPreset: Identifiable {
    let id = UUID()
    let name: String
    let work: Int
    let shortBreak: Int
    let longBreak: Int
}

extension PomodoroPreset {
    static let standard = PomodoroPreset(name: "标准", work: 25, shortBreak: 5, longBreak: 15)
    static let deep = PomodoroPreset(name: "深度", work: 50, shortBreak: 10, longBreak: 20)
    static let sprint = PomodoroPreset(name: "冲刺", work: 15, shortBreak: 3, longBreak: 10)
    static let all: [PomodoroPreset] = [.standard, .deep, .sprint]
}
