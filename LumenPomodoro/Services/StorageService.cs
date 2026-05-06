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

    public StorageService()
    {
        _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenPomodoro");
        Directory.CreateDirectory(_appDataPath);
        
        _settingsFile = Path.Combine(_appDataPath, "settings.json");
        _tasksFile = Path.Combine(_appDataPath, "tasks.json");
        _sessionsFile = Path.Combine(_appDataPath, "sessions.json");
    }

    public Settings LoadSettings()
    {
        if (File.Exists(_settingsFile))
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
        return new Settings();
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
        if (File.Exists(_tasksFile))
        {
            try
            {
                var content = File.ReadAllText(_tasksFile);
                return JsonConvert.DeserializeObject<List<TaskItem>>(content) ?? GetDefaultTasks();
            }
            catch
            {
                return GetDefaultTasks();
            }
        }
        return GetDefaultTasks();
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
                    Color = GetCategoryColor(category.Key),
                    CreatedAt = DateTime.Now
                });
            }
        }
        return tasks;
    }

    private string GetCategoryColor(string category)
    {
        return category switch
        {
            "数学" => "#3B82F6",
            "英语" => "#10B981",
            "政治" => "#EF4444",
            "专业课" => "#8B5CF6",
            _ => "#6B7280"
        };
    }

    public List<FocusSession> LoadSessions()
    {
        if (File.Exists(_sessionsFile))
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
        return new List<FocusSession>();
    }

    public void SaveSessions(List<FocusSession> sessions)
    {
        var content = JsonConvert.SerializeObject(sessions, Formatting.Indented);
        File.WriteAllText(_sessionsFile, content);
        InvalidateStatsCache();
    }

    public void AddSession(FocusSession session)
    {
        lock (_fileLock)
        {
            var sessions = LoadSessions();
            sessions.Add(session);
            SaveSessionsWithTransaction(sessions);
        }
        InvalidateStatsCache();
    }

    private void InvalidateStatsCache()
    {
        _cachedTodayStats = null;
    }

    public void SaveSessionsWithTransaction(List<FocusSession> sessions)
    {
        var backupFile = _sessionsFile + ".bak";
        
        try
        {
            var content = JsonConvert.SerializeObject(sessions, Formatting.Indented);
            
            // Create backup if original exists
            if (File.Exists(_sessionsFile))
            {
                File.Copy(_sessionsFile, backupFile, true);
            }
            
            // Write to temp file first
            var tempFile = _sessionsFile + ".tmp";
            File.WriteAllText(tempFile, content);
            
            // Replace original file with temp file
            if (File.Exists(_sessionsFile))
            {
                File.Replace(tempFile, _sessionsFile, backupFile);
            }
            else
            {
                // First time save - just move temp to original
                File.Move(tempFile, _sessionsFile);
            }
        }
        catch
        {
            // Restore from backup if exists
            if (File.Exists(backupFile))
            {
                File.Copy(backupFile, _sessionsFile, true);
            }
            throw;
        }
    }

    public DailyStats GetTodayStats()
    {
        if (_cachedTodayStats != null && _cacheDate == DateTime.Today)
        {
            return _cachedTodayStats;
        }

        var sessions = LoadSessions();
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