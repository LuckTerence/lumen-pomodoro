using System.Collections.Generic;
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

    public bool CameraAlertEnabled { get; set; } = false;
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

    // 防走神（前台窗口 + 键鼠空闲检测，仅专注阶段生效）
    public bool FocusGuardEnabled { get; set; } = true;

    /// <summary>分心 App 黑名单：匹配前台进程名或窗口标题（不区分大小写子串）。</summary>
    public List<string> FocusGuardBlocklist { get; set; } = new()
    {
        "bilibili", "youtube", "抖音", "douyin", "微博", "weibo",
        "知乎", "zhihu", "WeChat", "Weixin", "微信", "QQ", "TikTok",
        "Steam", "网易云音乐", "爱奇艺", "腾讯视频", "优酷"
    };

    [Range(10, 3600)]
    public int FocusGuardIdleSeconds { get; set; } = 180;

    [Range(1, 60)]
    public int FocusGuardPollSeconds { get; set; } = 5;

    public CameraAlertLevel FocusGuardAlertLevel { get; set; } = CameraAlertLevel.Severe;

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

    /// <summary>界面语言: "system" / "zh" / "en"</summary>
    public string Language { get; set; } = "system";
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
