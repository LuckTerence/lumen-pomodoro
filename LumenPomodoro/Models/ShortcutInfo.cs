using System.Collections.Generic;

namespace LumenPomodoro.Models;

/// <summary>
/// 快捷键信息，集中维护。
/// </summary>
public static class ShortcutInfo
{
    public record Entry(string Key, string Description, string Context);

    public static readonly List<Entry> All = new()
    {
        new("Space", "开始 / 暂停 / 恢复", "计时页"),
        new("Esc", "重置计时", "计时页"),
        new("1 - 9", "切换任务（按列表顺序）", "计时页"),
        new("Tab", "切换页面（下一项）", "全局"),
        new("Shift + Tab", "切换页面（上一项）", "全局"),
    };
}
