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
    
    public bool SoundEnabled { get; set; } = true;
    public bool PopupEnabled { get; set; } = true;
    public bool SystemNotificationEnabled { get; set; } = true;
    
    public bool TrayEnabled { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool AutoStartEnabled { get; set; } = false;
    
    public string Theme { get; set; } = "system";
    public bool AnimationEnabled { get; set; } = true;
}

public enum CameraAlertMode
{
    FixedDuration,
    UntilConfirm,
    FollowBreak
}