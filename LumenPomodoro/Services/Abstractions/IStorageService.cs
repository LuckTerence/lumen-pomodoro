using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface IStorageService
{
    Settings LoadSettings();
    void SaveSettings(Settings settings);
    List<TaskItem> LoadTasks();
    void SaveTasks(List<TaskItem> tasks);
    List<FocusSession> LoadSessions();
    void AddSession(FocusSession session);
    void UpdateSession(string sessionId, Action<FocusSession> updater);
    DailyStats GetTodayStats();
}
