using System.ComponentModel.DataAnnotations;

namespace LumenPomodoro.Models;

public class Settings
{
    public int SchemaVersion { get; set; } = 1;

    [Range(1, 120)]
    public int WorkMinutes { get; set; } = 25;

    [Range(1, 60)]
    public int ShortBreakMinutes { get; set; } = 5;

    [Range(1, 120)]
    public int LongBreakMinutes { get; set; } = 15;

    [Range(1, 20)]
    public int LongBreakInterval { get; set; } = 4;

    public bool CameraAlertEnabled { get; set; } = true;
    public CameraAlertMode CameraAlertMode { get; set; } = CameraAlertMode.UntilConfirm;

    [Range(10, 3600)]
    public int CameraFixedOnSeconds { get; set; } = 180;

    public bool CameraFollowBreakEnabled { get; set; } = true;

    [Range(0, 99)]
    public int CameraIndex { get; set; } = 0;

    public bool CameraAlertCanManualClose { get; set; } = true;
    public CameraAlertLevel CameraAlertLevel { get; set; } = CameraAlertLevel.Medium;
    public bool HasShownCameraPrivacyNotice { get; set; } = false;

    public bool PresenceDetectionEnabled { get; set; } = true;

    [Range(1, 300)]
    public int PresenceDetectionSeconds { get; set; } = 5;

    [Range(0, 1440)]
    public int DailyGoalMinutes { get; set; } = 120;

    [Range(0, 10080)]
    public int WeeklyGoalMinutes { get; set; } = 600;

    [Range(1, 100)]
    public int DailyTargetPomodoros { get; set; } = 8;

    public bool SoundEnabled { get; set; } = true;
    public bool PopupEnabled { get; set; } = true;
    public bool SystemNotificationEnabled { get; set; } = true;

    public bool TrayEnabled { get; set; } = false;
    public bool CloseToTray { get; set; } = false;
    public bool AutoStartEnabled { get; set; } = false;

    /// <summary>主题：system / light / dark</summary>
    public string Theme { get; set; } = "system";

    public bool AnimationEnabled { get; set; } = true;
    public string? LastSelectedTaskId { get; set; }

    public DateTime? ExamDate { get; set; }
    public string ExamName { get; set; } = "考研";
    public DateTime? LastReportShownDate { get; set; }

    // 功能开关（非核心功能）
    public bool InsightsEnabled { get; set; } = true;
    public bool DailyReportEnabled { get; set; } = true;
    public bool ExamCountdownEnabled { get; set; } = true;
    public bool DynamicIslandEnabled { get; set; } = true;
}

public enum CameraAlertMode
{
    FixedDuration,
    UntilConfirm,
    FollowBreak
}

public enum CameraAlertLevel
{
    Light,
    Medium,
    Severe
}
