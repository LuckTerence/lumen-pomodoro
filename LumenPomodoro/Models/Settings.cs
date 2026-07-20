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

    /// <summary>是否完成首次产品引导（灯 / 隐私 / 场景预设）。</summary>
    public bool HasCompletedOnboarding { get; set; } = false;

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

    /// <summary>顶部灵动岛——产品主卖点与默认主交互。默认开启。</summary>
    public bool DynamicIslandEnabled { get; set; } = true;

    /// <summary>
    /// 主窗口在前台时岛的行为：keep=保持；minimize=淡化缩小；hide=隐藏。
    /// 默认 minimize：岛仍可见但不抢戏。
    /// </summary>
    public string DynamicIslandWhenFocused { get; set; } = "minimize";

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
    /// 严格模式：禁止提前结束休息；若已开摄像头灯则禁止手动关灯；
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

    /// <summary>轻松：岛 + 声音/通知，少打扰（方案 B：灯始终默认关）。</summary>
    public void ApplyLightFocusPreset()
    {
        StrictModeEnabled = false;
        FullscreenBreakEnabled = false;
        DynamicIslandEnabled = true;
        DynamicIslandWhenFocused = "minimize";
        CameraAlertEnabled = false;
        CameraAlertCanManualClose = true;
        FocusGuardEnabled = false;
        ConfirmExitWhileFocusing = true;
        SessionEndPreNotifySeconds = 30;
        SoundEnabled = true;
        PopupEnabled = true;
        SystemNotificationEnabled = true;
    }

    /// <summary>标准：灵动岛主交互 + 防走神；摄像头灯保持关闭（高级选项）。</summary>
    public void ApplyStandardFocusPreset()
    {
        StrictModeEnabled = false;
        FullscreenBreakEnabled = false;
        DynamicIslandEnabled = true;
        DynamicIslandWhenFocused = "minimize";
        CameraAlertEnabled = false;
        CameraAlertCanManualClose = true;
        FocusGuardEnabled = true;
        FocusGuardAlertLevel = CameraAlertLevel.Medium;
        ConfirmExitWhileFocusing = true;
        if (SessionEndPreNotifySeconds <= 0)
            SessionEndPreNotifySeconds = 30;
        SoundEnabled = true;
        PopupEnabled = true;
        SystemNotificationEnabled = true;
    }

    /// <summary>
    /// 严格专注：严格模式 + 全屏休息 + 岛 keep；摄像头灯仍默认关（高级可选）。
    /// 不覆盖时长、任务、黑名单等个人配置。
    /// </summary>
    public void ApplyStrictFocusPreset()
    {
        StrictModeEnabled = true;
        FullscreenBreakEnabled = true;
        DynamicIslandEnabled = true;
        DynamicIslandWhenFocused = "keep";
        CameraAlertEnabled = false;
        CameraAlertCanManualClose = false;
        FocusGuardEnabled = true;
        FocusGuardAlertLevel = CameraAlertLevel.Severe;
        ConfirmExitWhileFocusing = true;
        if (SessionEndPreNotifySeconds <= 0)
            SessionEndPreNotifySeconds = 30;
        SoundEnabled = true;
        PopupEnabled = true;
        SystemNotificationEnabled = true;
    }

    /// <summary>应用命名场景预设：light / standard / strict。</summary>
    public void ApplyFocusScenePreset(string scene)
    {
        switch ((scene ?? "").Trim().ToLowerInvariant())
        {
            case "light":
            case "轻松":
                ApplyLightFocusPreset();
                break;
            case "strict":
            case "严格":
            case "严格专注":
                ApplyStrictFocusPreset();
                break;
            default:
                ApplyStandardFocusPreset();
                break;
        }
    }
}

/// <summary>摄像头灯状态文案（高级可选；非主卖点）。</summary>
public static class CameraAlertStatusText
{
    public static string Describe(bool cameraEnabled, bool isActive, string? rawStatus, bool canManualClose)
    {
        if (!cameraEnabled)
            return "摄像头灯：关闭（高级选项，默认不用）";
        if (isActive)
        {
            var baseText = string.IsNullOrWhiteSpace(rawStatus)
                ? "摄像头灯：亮着 — 该休息了"
                : $"摄像头灯：亮着 — {rawStatus}";
            return canManualClose
                ? baseText + "（可手动关闭）"
                : baseText + "（严格模式不可手关）";
        }

        if (!string.IsNullOrWhiteSpace(rawStatus) &&
            (rawStatus.Contains("失败", StringComparison.Ordinal) ||
             rawStatus.Contains("错误", StringComparison.Ordinal) ||
             rawStatus.Contains("error", StringComparison.OrdinalIgnoreCase) ||
             rawStatus.Contains("权限", StringComparison.Ordinal)))
        {
            return $"摄像头灯：异常 — {rawStatus}。请在系统设置中允许摄像头权限后重试。";
        }

        return "摄像头灯：待命（专注结束后点亮）";
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
