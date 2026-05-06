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
}

public class DailyStats
{
    public int CompletedPomodoros { get; set; } = 0;
    public int TotalFocusMinutes { get; set; } = 0;
    public Dictionary<string, int> TaskStats { get; set; } = new Dictionary<string, int>();
    public int CurrentStreak { get; set; } = 0;
}