using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using LumenPomodoro.Models;

namespace LumenPomodoro.Services;

public class StorageService
{
    private readonly string _appDataPath;
    private readonly string _settingsFile;
    private readonly string _tasksFile;
    private readonly string _sessionsFile;

    private DailyStats? _cachedTodayStats;
    private DateTime _cacheDate;
    private readonly object _fileLock = new object();

    public StorageService(string? appDataPath = null)
    {
        _appDataPath = appDataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenPomodoro");
        Directory.CreateDirectory(_appDataPath);

        _settingsFile = Path.Combine(_appDataPath, "settings.json");
        _tasksFile = Path.Combine(_appDataPath, "tasks.json");
        _sessionsFile = Path.Combine(_appDataPath, "sessions.json");
    }

    public Settings LoadSettings()
    {
        lock (_fileLock)
        {
            try
            {
                var content = File.ReadAllText(_settingsFile);
                return JsonConvert.DeserializeObject<Settings>(content) ?? new Settings();
            }
            catch
            {
                return new Settings();
            }
        }
    }

    public void SaveSettings(Settings settings)
    {
        lock (_fileLock)
        {
            var content = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_settingsFile, content);
        }
    }

    public List<TaskItem> LoadTasks()
    {
        lock (_fileLock)
        {
            try
            {
                var content = File.ReadAllText(_tasksFile);
                var tasks = JsonConvert.DeserializeObject<List<TaskItem>>(content);
                if (tasks != null && tasks.Count > 0) return tasks;
            }
            catch
            {
            }

            var defaults = GetDefaultTasks();
            SaveTasks(defaults);
            return defaults;
        }
    }

    public void SaveTasks(List<TaskItem> tasks)
    {
        lock (_fileLock)
        {
            var content = JsonConvert.SerializeObject(tasks, Formatting.Indented);
            File.WriteAllText(_tasksFile, content);
        }
    }

    private List<TaskItem> GetDefaultTasks()
    {
        var tasks = new List<TaskItem>();
        int index = 0;
        foreach (var category in TaskCategories.DefaultTasks)
        {
            foreach (var taskName in category.Value)
            {
                tasks.Add(new TaskItem
                {
                    Id = $"default_{index++}",
                    Name = taskName,
                    Category = category.Key,
                    Color = TaskCategories.GetCategoryColor(category.Key),
                    CreatedAt = DateTime.Now
                });
            }
        }
        return tasks;
    }

    public List<FocusSession> LoadSessions()
    {
        lock (_fileLock)
        {
            return LoadSessionsCore();
        }
    }

    private List<FocusSession> LoadSessionsCore()
    {
        try
        {
            var content = File.ReadAllText(_sessionsFile);
            return JsonConvert.DeserializeObject<List<FocusSession>>(content) ?? new List<FocusSession>();
        }
        catch
        {
            return new List<FocusSession>();
        }
    }

    public void SaveSessions(List<FocusSession> sessions)
    {
        lock (_fileLock)
        {
            var content = JsonConvert.SerializeObject(sessions, Formatting.Indented);
            File.WriteAllText(_sessionsFile, content);
        }
        InvalidateStatsCache();
    }

    public void AddSession(FocusSession session)
    {
        lock (_fileLock)
        {
            var sessions = LoadSessionsCore();
            sessions.Add(session);
            SaveSessionsWithTransaction(sessions);
        }
    }

    private void InvalidateStatsCache()
    {
        _cachedTodayStats = null;
    }

    private void SaveSessionsWithTransaction(List<FocusSession> sessions)
    {
        var backupFile = _sessionsFile + ".bak";

        try
        {
            var content = JsonConvert.SerializeObject(sessions, Formatting.Indented);

            if (File.Exists(_sessionsFile))
            {
                File.Copy(_sessionsFile, backupFile, true);
            }

            var tempFile = _sessionsFile + ".tmp";
            File.WriteAllText(tempFile, content);

            if (File.Exists(_sessionsFile))
            {
                File.Replace(tempFile, _sessionsFile, backupFile);
            }
            else
            {
                File.Move(tempFile, _sessionsFile);
            }

            InvalidateStatsCache();
        }
        catch
        {
            if (File.Exists(backupFile))
            {
                File.Copy(backupFile, _sessionsFile, true);
            }
            throw;
        }
    }

    public DailyStats GetTodayStats()
    {
        lock (_fileLock)
        {
            if (_cachedTodayStats != null && _cacheDate == DateTime.Today)
            {
                return _cachedTodayStats;
            }

            var sessions = LoadSessionsCore();
            var today = DateTime.Today;
            var todaySessions = sessions.Where(s => s.Completed && s.EndTime.HasValue && s.EndTime.Value.Date == today).ToList();

            var stats = new DailyStats();
            stats.CompletedPomodoros = todaySessions.Count;
            stats.TotalFocusMinutes = todaySessions.Sum(s => s.FocusMinutes);

            foreach (var session in todaySessions)
            {
                if (!stats.TaskStats.ContainsKey(session.TaskName))
                {
                    stats.TaskStats[session.TaskName] = 0;
                }
                stats.TaskStats[session.TaskName]++;
            }

            stats.CurrentStreak = CalculateStreak(sessions);

            _cachedTodayStats = stats;
            _cacheDate = DateTime.Today;

            return stats;
        }
    }

    private int CalculateStreak(List<FocusSession> sessions)
    {
        var completedSessions = sessions.Where(s => s.Completed && s.EndTime.HasValue)
                                       .Select(s => s.EndTime!.Value.Date)
                                       .Distinct()
                                       .OrderByDescending(d => d)
                                       .ToList();

        if (completedSessions.Count == 0) return 0;

        var startDate = completedSessions[0];
        if (startDate != DateTime.Today && startDate != DateTime.Today.AddDays(-1))
        {
            return 0;
        }

        int streak = 1;
        for (int i = 1; i < completedSessions.Count; i++)
        {
            if (completedSessions[i] == completedSessions[i - 1].AddDays(-1))
            {
                streak++;
            }
            else
            {
                break;
            }
        }

        return streak;
    }
}
