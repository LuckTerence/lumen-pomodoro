namespace LumenPomodoro.Models;

/// <summary>
/// 洞察可触发的结构化动作类型。UI 据此渲染真实按钮，形成「洞察→行动」闭环，
/// 而非仅展示 ActionHint 文案。
/// </summary>
public enum SuggestedActionKind
{
    /// <summary>立即以指定科目开始一次专注</summary>
    StartFocus,
    /// <summary>把科目排到今日的某个时段（配合 DailyPlan，A2 使用）</summary>
    ScheduleBlock,
    /// <summary>建议调整单次专注时长（分钟）</summary>
    AdjustDuration,
    /// <summary>跳转到某个设置项</summary>
    OpenSettings
}

/// <summary>
/// 洞察附带的可执行动作。替代纯文本的 ActionHint，使 UI 能渲染真实按钮。
/// 该对象为运行时计算产物，不入 JSON，故不触发 schema 迁移。
/// </summary>
public class SuggestedAction
{
    public SuggestedActionKind Kind { get; set; }
    /// <summary>按钮文案，如「现在专注「数学」」</summary>
    public string ActionLabel { get; set; } = string.Empty;
    /// <summary>关联科目名（StartFocus / ScheduleBlock 使用）</summary>
    public string TaskName { get; set; } = string.Empty;
    /// <summary>建议时段（0-23），无则 -1（ScheduleBlock 使用）</summary>
    public int PreferredHour { get; set; } = -1;
    /// <summary>建议时长（分钟），无则 0（AdjustDuration 使用）</summary>
    public int TargetMinutes { get; set; }
    /// <summary>目标设置键（OpenSettings 使用）</summary>
    public string SettingKey { get; set; } = string.Empty;

    public SuggestedAction() { }

    public SuggestedAction(SuggestedActionKind kind, string actionLabel,
        string taskName = "", int preferredHour = -1, int targetMinutes = 0, string settingKey = "")
    {
        Kind = kind;
        ActionLabel = actionLabel;
        TaskName = taskName;
        PreferredHour = preferredHour;
        TargetMinutes = targetMinutes;
        SettingKey = settingKey;
    }
}

/// <summary>
/// 今日计划中的一个时段块。配合「峰值时段排程」（A2）：
/// 洞察建议把某科目排到某个时段，落盘到 dailyplan.json，形成可执行的今日计划。
/// </summary>
public class PlannedBlock
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>关联科目名</summary>
    public string TaskName { get; set; } = string.Empty;
    /// <summary>计划时段（0-23）</summary>
    public int Hour { get; set; }
    /// <summary>计划专注分钟（默认取单次工作分钟）</summary>
    public int DurationMinutes { get; set; }
}

/// <summary>
/// 某一天的专注计划；按日期存储，跨天后自动重置。
/// 该对象会落盘（dailyplan.json），故触发 schema 迁移（V2）。
/// </summary>
public class DailyPlan
{
    public DateTime Date { get; set; } = DateTime.Today;
    public List<PlannedBlock> Blocks { get; set; } = [];
}

public class Insight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionHint { get; set; } = string.Empty;
    public InsightType Type { get; set; }
    /// <summary>结构化可执行动作；为 null 时 UI 仅展示说明</summary>
    public SuggestedAction? Action { get; set; }
}

public enum InsightType
{
    PeakHour,
    BestDay,
    Trend,
    Streak,
    TaskCompletion,
    Motivation
}

public class HeatmapDay
{
    public DateTime Date { get; set; }
    public int FocusMinutes { get; set; }
    public int IntensityLevel { get; set; } // 0-4
}

public class HourlyDataPoint
{
    public int Hour { get; set; } // 0-23
    public int TotalMinutes { get; set; }
    public int SessionCount { get; set; }
}

public class WeeklyDataPoint
{
    public DateTime WeekStart { get; set; }
    public int TotalMinutes { get; set; }
    public int CompletedPomodoros { get; set; }
}

public class TaskSlice
{
    public string TaskName { get; set; } = string.Empty;
    public string TaskColor { get; set; } = "#3B82F6";
    public int PomodoroCount { get; set; }
    public double Percentage { get; set; }
}

public class ComparisonData
{
    public string Label { get; set; } = string.Empty;
    public int CurrentValue { get; set; }
    public int PreviousValue { get; set; }
    public double ChangePercent { get; set; }
    public bool IsPositive { get; set; }
}

public class GoalProgress
{
    public string Label { get; set; } = string.Empty;
    public int CurrentMinutes { get; set; }
    public int TargetMinutes { get; set; }
    public double ProgressPercent { get; set; }
    public bool IsCompleted { get; set; }
}

public class CategoryStats
{
    public string Category { get; set; } = string.Empty;
    public int TotalMinutes { get; set; }
    public int PomodoroCount { get; set; }
    public string Color { get; set; } = "#3B82F6";
}

public class EfficiencyDataPoint
{
    public DateTime WeekStart { get; set; }
    public double CompletionRate { get; set; } // 0-1
    public double AvgFocusMinutes { get; set; }
    public double AvgQualityScore { get; set; } // 1-3
}
