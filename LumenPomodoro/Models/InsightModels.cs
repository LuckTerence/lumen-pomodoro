namespace LumenPomodoro.Models;

public class Insight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionHint { get; set; } = string.Empty;
    public InsightType Type { get; set; }
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
