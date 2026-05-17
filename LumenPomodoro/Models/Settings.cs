namespace LumenPomodoro.Models;

public class Settings
{
    public int WorkMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int LongBreakInterval { get; set; } = 4;
    
    public bool CameraAlertEnabled { get; set; } = true;
    public CameraAlertMode CameraAlertMode { get; set; } = CameraAlertMode.UntilConfirm;
    public int CameraFixedOnSeconds { get; set; } = 180;
    public bool CameraFollowBreakEnabled { get; set; } = true;
    public int CameraIndex { get; set; } = 0;
    public bool CameraAlertCanManualClose { get; set; } = true;
    public CameraAlertLevel CameraAlertLevel { get; set; } = CameraAlertLevel.Medium;
    public bool HasShownCameraPrivacyNotice { get; set; } = false;

    public bool PresenceDetectionEnabled { get; set; } = true;
    public int PresenceDetectionSeconds { get; set; } = 5;
    public int DailyGoalMinutes { get; set; } = 120;
    public int WeeklyGoalMinutes { get; set; } = 600;
    
    public bool SoundEnabled { get; set; } = true;
    public bool PopupEnabled { get; set; } = true;
    public bool SystemNotificationEnabled { get; set; } = true;
    
    public bool TrayEnabled { get; set; } = false;
    public bool CloseToTray { get; set; } = false;
    public bool AutoStartEnabled { get; set; } = false;
    
    public string Theme { get; set; } = "system";
    public bool AnimationEnabled { get; set; } = true;
    public string? LastSelectedTaskId { get; set; }
    public DateTime? ExamDate { get; set; }
    public string ExamName { get; set; } = "考研";
    public DateTime? LastReportShownDate { get; set; }
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