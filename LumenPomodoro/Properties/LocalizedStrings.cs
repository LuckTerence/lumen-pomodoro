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

    public static string App_Title => _resources.GetString(nameof(App_Title), CultureInfo.CurrentUICulture) ?? "Lumen 番茄钟";
    public static string Focus_Complete => _resources.GetString(nameof(Focus_Complete), CultureInfo.CurrentUICulture) ?? "专注完成";
    public static string Break_Time => _resources.GetString(nameof(Break_Time), CultureInfo.CurrentUICulture) ?? "该休息了！";
    public static string Focus_Start => _resources.GetString(nameof(Focus_Start), CultureInfo.CurrentUICulture) ?? "专注 · {0}";
    public static string Break_Complete => _resources.GetString(nameof(Break_Complete), CultureInfo.CurrentUICulture) ?? "休息完成！";
    public static string Break_Ready => _resources.GetString(nameof(Break_Ready), CultureInfo.CurrentUICulture) ?? "准备好开始下一轮了吗？";
    public static string Milestone_First => _resources.GetString(nameof(Milestone_First), CultureInfo.CurrentUICulture) ?? "第一个番茄完成！";
    public static string Milestone_Daily => _resources.GetString(nameof(Milestone_Daily), CultureInfo.CurrentUICulture) ?? "今日目标达成！";
    public static string Milestone_Target => _resources.GetString(nameof(Milestone_Target), CultureInfo.CurrentUICulture) ?? "今日番茄目标达成！";
    public static string Short_Break => _resources.GetString(nameof(Short_Break), CultureInfo.CurrentUICulture) ?? "短休息";
    public static string Long_Break => _resources.GetString(nameof(Long_Break), CultureInfo.CurrentUICulture) ?? "长休息";
}
