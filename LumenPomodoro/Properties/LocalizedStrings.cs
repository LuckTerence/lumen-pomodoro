using System.Globalization;
using System.Resources;

namespace LumenPomodoro.Properties;

/// <summary>
/// 提供对 Resources.resx 和 Resources.{culture}.resx 的强类型访问。
/// 运行时根据 Thread.CurrentUICulture 自动选择对应语言。
/// </summary>
public static class LocalizedStrings
{
    private static readonly ResourceManager _resources =
        new("LumenPomodoro.Properties.Resources", typeof(LocalizedStrings).Assembly);

    public static string App_Title => _resources.GetString(nameof(App_Title), CultureInfo.CurrentUICulture) ?? "Lumen Pomodoro";
    public static string Focus_Complete => _resources.GetString(nameof(Focus_Complete), CultureInfo.CurrentUICulture) ?? "Focus Complete";
    public static string Break_Time => _resources.GetString(nameof(Break_Time), CultureInfo.CurrentUICulture) ?? "Break Time!";
    public static string Focus_Start => _resources.GetString(nameof(Focus_Start), CultureInfo.CurrentUICulture) ?? "Focus · {0}";
    public static string Break_Complete => _resources.GetString(nameof(Break_Complete), CultureInfo.CurrentUICulture) ?? "Break Complete!";
    public static string Break_Ready => _resources.GetString(nameof(Break_Ready), CultureInfo.CurrentUICulture) ?? "Ready to start next round?";
    public static string Milestone_First => _resources.GetString(nameof(Milestone_First), CultureInfo.CurrentUICulture) ?? "First Pomodoro Done!";
    public static string Milestone_Daily => _resources.GetString(nameof(Milestone_Daily), CultureInfo.CurrentUICulture) ?? "Daily Goal Reached!";
    public static string Milestone_Target => _resources.GetString(nameof(Milestone_Target), CultureInfo.CurrentUICulture) ?? "Daily Pomodoro Target Reached!";
    public static string Short_Break => _resources.GetString(nameof(Short_Break), CultureInfo.CurrentUICulture) ?? "Short Break";
    public static string Long_Break => _resources.GetString(nameof(Long_Break), CultureInfo.CurrentUICulture) ?? "Long Break";
    public static string NoTaskSelected => _resources.GetString(nameof(NoTaskSelected), CultureInfo.CurrentUICulture) ?? "Please select a task first";
    public static string DistractionAlert => _resources.GetString(nameof(DistractionAlert), CultureInfo.CurrentUICulture) ?? "Distraction Alert";
    public static string DistractionMessage => _resources.GetString(nameof(DistractionMessage), CultureInfo.CurrentUICulture) ?? "You seem to have left. Please return to focus.";
    public static string CameraError => _resources.GetString(nameof(CameraError), CultureInfo.CurrentUICulture) ?? "Camera Alert Failed";
    public static string CameraErrorTitle => _resources.GetString(nameof(CameraErrorTitle), CultureInfo.CurrentUICulture) ?? "Camera Error";
    public static string CameraProtectedRelease => _resources.GetString(nameof(CameraProtectedRelease), CultureInfo.CurrentUICulture) ?? "protection release";
    public static string CameraManualCloseNotAllowed => _resources.GetString(nameof(CameraManualCloseNotAllowed), CultureInfo.CurrentUICulture) ?? "Manual camera close is not allowed. Enable it in settings.";
    public static string AllAlertsDisabled => _resources.GetString(nameof(AllAlertsDisabled), CultureInfo.CurrentUICulture) ?? "Warning: All notification methods are disabled. You may miss alerts!";
    public static string RegistryAccessDenied => _resources.GetString(nameof(RegistryAccessDenied), CultureInfo.CurrentUICulture) ?? "Cannot access system startup registry. Run as administrator.";
    public static string RegistryWriteDenied => _resources.GetString(nameof(RegistryWriteDenied), CultureInfo.CurrentUICulture) ?? "Cannot modify registry. Run as administrator.";
    public static string AutoStartFailed => _resources.GetString(nameof(AutoStartFailed), CultureInfo.CurrentUICulture) ?? "Failed to set auto-start";
    public static string ConfirmExitWhileFocusing_Title => _resources.GetString(nameof(ConfirmExitWhileFocusing_Title), CultureInfo.CurrentUICulture) ?? "确认退出";
    public static string ConfirmExitWhileFocusing_Message => _resources.GetString(nameof(ConfirmExitWhileFocusing_Message), CultureInfo.CurrentUICulture) ?? "计时仍在进行中，确定要退出吗？未完成的本轮进度将不会保存为完成番茄。";
    public static string SessionEndSoon_Title => _resources.GetString(nameof(SessionEndSoon_Title), CultureInfo.CurrentUICulture) ?? "即将结束";
    public static string SessionEndSoon_Message => _resources.GetString(nameof(SessionEndSoon_Message), CultureInfo.CurrentUICulture) ?? "还剩 {0} 秒";
    public static string StrictMode_CameraCloseBlocked => _resources.GetString(nameof(StrictMode_CameraCloseBlocked), CultureInfo.CurrentUICulture) ?? "严格模式已开启，不能手动关闭摄像头提醒。";
    public static string StrictMode_EndBreakBlocked => _resources.GetString(nameof(StrictMode_EndBreakBlocked), CultureInfo.CurrentUICulture) ?? "严格模式已开启，请等待休息自然结束。";
    public static string FullscreenBreak_Short => _resources.GetString(nameof(FullscreenBreak_Short), CultureInfo.CurrentUICulture) ?? "短休息";
    public static string FullscreenBreak_Long => _resources.GetString(nameof(FullscreenBreak_Long), CultureInfo.CurrentUICulture) ?? "长休息";
    public static string FullscreenBreak_Hint => _resources.GetString(nameof(FullscreenBreak_Hint), CultureInfo.CurrentUICulture) ?? "站起来走走，看看远处";
}
