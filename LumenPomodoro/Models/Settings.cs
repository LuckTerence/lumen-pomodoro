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

    /// <summary>连续多少次 poll 命中后才告警（防抖）。默认 2。</summary>
    [Range(1, 10)]
    public int FocusGuardDebounceHits { get; set; } = 2;

    /// <summary>单次专注会话最多发出几次走神通知。默认 3。</summary>
    [Range(1, 20)]
    public int FocusGuardMaxAlertsPerSession { get; set; } = 3;

    /// <summary>系统勿扰开启时降级通知（预留；实现按端推进）。</summary>
    public bool FocusGuardRespectDoNotDisturb { get; set; } = true;

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

    /// <summary>专注/休息计时进行中关闭应用时弹出确认。默认 true。</summary>
    public bool ConfirmExitWhileFocusing { get; set; } = true;

    /// <summary>
    /// 计时结束前多少秒发一次预告通知；0 = 关闭。建议 15–120。
    /// </summary>
    [Range(0, 300)]
    public int SessionEndPreNotifySeconds { get; set; } = 30;

    /// <summary>休息时显示全屏遮罩倒计时。默认 false。</summary>
    public bool FullscreenBreakEnabled { get; set; } = false;

    /// <summary>
    /// 严格模式：禁止手动关摄像头灯、禁止提前结束休息；
    /// 完成专注时按 Severe 强度置顶。默认 false。
    /// </summary>
    public bool StrictModeEnabled { get; set; } = false;

    /// <summary>界面语言: "system" / "zh" / "en"</summary>
    public string Language { get; set; } = "system";

    /// <summary>是否允许用户手动关闭摄像头提醒（严格模式强制否）。</summary>
    public bool EffectiveCameraAlertCanManualClose =>
        !StrictModeEnabled && CameraAlertCanManualClose;

    /// <summary>休息中是否允许提前结束（严格模式强制否）。</summary>
    public bool EffectiveAllowEndBreakEarly => !StrictModeEnabled;

    /// <summary>
    /// 严格专注一键预设：严格模式 + 全屏休息 + 摄像头灯（Severe / 不可手关 / 跟随休息）。
    /// 不覆盖时长、任务、黑名单等个人配置。
    /// </summary>
    public void ApplyStrictFocusPreset()
    {
        StrictModeEnabled = true;
        FullscreenBreakEnabled = true;
        CameraAlertEnabled = true;
        CameraAlertMode = CameraAlertMode.UntilConfirm;
        CameraAlertLevel = CameraAlertLevel.Severe;
        CameraAlertCanManualClose = false;
        CameraFollowBreakEnabled = true;
        ConfirmExitWhileFocusing = true;
        if (SessionEndPreNotifySeconds <= 0)
            SessionEndPreNotifySeconds = 30;
        SoundEnabled = true;
        PopupEnabled = true;
        SystemNotificationEnabled = true;
    }
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
