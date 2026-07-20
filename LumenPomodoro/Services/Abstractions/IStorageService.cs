using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface IStorageService
{
    Settings LoadSettings();
    void SaveSettings(Settings settings);
    List<TaskItem> LoadTasks();
    void SaveTasks(List<TaskItem> tasks);
    void RestoreDefaultTasks();
    List<FocusSession> LoadSessions();
    void AddSession(FocusSession session);
    void UpdateSession(string sessionId, Action<FocusSession> updater);
    DailyStats GetTodayStats();
    /// <summary>读取今日计划（峰值时段排程 A2）。跨天自动返回今日空计划。</summary>
    DailyPlan LoadDailyPlan();
    /// <summary>写入今日计划（日期归正为今天）。</summary>
    void SaveDailyPlan(DailyPlan plan);
}
