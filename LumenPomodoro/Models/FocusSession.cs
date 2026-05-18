namespace LumenPomodoro.Models;

public class FocusSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public int FocusMinutes { get; set; } = 25;
    public bool Completed { get; set; } = false;
    public string? Notes { get; set; }
    public int QualityScore { get; set; } = 0; // 1-3 星
}

public class DailyStats
{
    public int CompletedPomodoros { get; set; } = 0;
    public int TotalFocusMinutes { get; set; } = 0;
    public Dictionary<string, int> TaskStats { get; set; } = new Dictionary<string, int>();
    public int CurrentStreak { get; set; } = 0;
}

public class DailyReport
{
    public DateTime Date { get; set; }
    public int CompletedPomodoros { get; set; }
    public int TotalMinutes { get; set; }
    public string MainTask { get; set; } = string.Empty;
    public int StreakDays { get; set; }
    public double AvgQualityScore { get; set; }
    public int UniqueTasksCount { get; set; }
}