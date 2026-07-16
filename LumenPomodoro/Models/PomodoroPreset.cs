namespace LumenPomodoro.Models;

/// <summary>
/// 番茄钟预设定义。
/// </summary>
public class PomodoroPreset
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int WorkMinutes { get; init; }
    public int ShortBreakMinutes { get; init; }
    public int LongBreakMinutes { get; init; }
    public int LongBreakInterval { get; init; } = 4;

    public static readonly PomodoroPreset Standard = new()
    {
        Name = "standard",
        DisplayName = "标准",
        Description = "25 分钟专注 · 5 分钟短休 · 15 分钟长休",
        WorkMinutes = 25,
        ShortBreakMinutes = 5,
        LongBreakMinutes = 15,
        LongBreakInterval = 4
    };

    public static readonly PomodoroPreset DeepWork = new()
    {
        Name = "deep",
        DisplayName = "深度工作",
        Description = "50 分钟专注 · 10 分钟短休 · 20 分钟长休",
        WorkMinutes = 50,
        ShortBreakMinutes = 10,
        LongBreakMinutes = 20,
        LongBreakInterval = 4
    };

    public static readonly PomodoroPreset Sprint = new()
    {
        Name = "sprint",
        DisplayName = "冲刺",
        Description = "15 分钟专注 · 3 分钟短休 · 10 分钟长休",
        WorkMinutes = 15,
        ShortBreakMinutes = 3,
        LongBreakMinutes = 10,
        LongBreakInterval = 6
    };

    public static readonly PomodoroPreset Custom = new()
    {
        Name = "custom",
        DisplayName = "自定义",
        Description = "手动调节时间",
        WorkMinutes = 0,
        ShortBreakMinutes = 0,
        LongBreakMinutes = 0,
        LongBreakInterval = 0
    };

    public static readonly PomodoroPreset[] All = [Standard, DeepWork, Sprint];
}
