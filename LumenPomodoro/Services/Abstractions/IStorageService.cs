using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface IStorageService
{
    Settings LoadSettings();
    void SaveSettings(Settings settings);
    Task SaveSettingsAsync(Settings settings, CancellationToken cancellationToken = default);
    
    List<TaskItem> LoadTasks();
    void SaveTasks(List<TaskItem> tasks);
    Task SaveTasksAsync(List<TaskItem> tasks, CancellationToken cancellationToken = default);
    
    List<FocusSession> LoadSessions();
    void AddSession(FocusSession session);
    Task AddSessionAsync(FocusSession session, CancellationToken cancellationToken = default);
    
    DailyStats GetTodayStats();
    List<FocusSession> GetSessionsInRange(DateTime start, DateTime end);
}
