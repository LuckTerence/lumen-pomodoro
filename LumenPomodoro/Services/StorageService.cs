using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

public class StorageService : IStorageService
{
    private readonly string _appDataPath;
    private readonly string _settingsFile;
    private readonly string _tasksFile;
    private readonly string _sessionsFile;

    private DailyStats? _cachedTodayStats;
    private DateTime _cacheDate;
    private readonly object _fileLock = new object();

    // Sessions 内存缓存 — 避免重复 JSON 反序列化
    private List<FocusSession>? _sessionsCache;
    private DateTime _sessionsCacheFileTime;

    // Tasks 内存缓存
    private List<TaskItem>? _tasksCache;
    private DateTime _tasksCacheFileTime;


    public StorageService(string? appDataPath = null)
    {
        _appDataPath = appDataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenPomodoro");
        Directory.CreateDirectory(_appDataPath);

        _settingsFile = Path.Combine(_appDataPath, "settings.json");
        _tasksFile = Path.Combine(_appDataPath, "tasks.json");
        _sessionsFile = Path.Combine(_appDataPath, "sessions.json");

        Log.Debug("StorageService 初始化，数据路径: {Path}", _appDataPath);
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
            // 检查 tasks 缓存
            if (_tasksCache != null && File.Exists(_tasksFile))
            {
                var writeTime = File.GetLastWriteTime(_tasksFile);
                if (writeTime == _tasksCacheFileTime)
                    return new List<TaskItem>(_tasksCache);
            }

            try
            {
                var content = File.ReadAllText(_tasksFile);
                var tasks = JsonConvert.DeserializeObject<List<TaskItem>>(content);
                if (tasks != null && tasks.Count > 0)
                {
                    UpdateTasksCache(tasks);
                    return new List<TaskItem>(tasks);
                }
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
            UpdateTasksCache(tasks);
        }
    }

    private static readonly (string Name, string Color)[] DefaultTaskData =
    [
        ("专注", "#3B82F6"),
        ("学习", "#10B981"),
        ("阅读", "#8B5CF6"),
        ("复习", "#EF4444"),
    ];

    private List<TaskItem> GetDefaultTasks()
    {
        return DefaultTaskData.Select((t, i) => new TaskItem
        {
            Id = $"default_{i}",
            Name = t.Name,
            Category = string.Empty,
            Color = t.Color,
            CreatedAt = DateTime.Now
        }).ToList();
    }

    public List<FocusSession> LoadSessions()
    {
        lock (_fileLock)
        {
            return new List<FocusSession>(GetOrLoadSessions());
        }
    }

    /// <summary>
    /// 返回缓存引用（不拷贝），仅供内部在同一锁内使用。
    /// </summary>
    private List<FocusSession> GetOrLoadSessions()
    {
        if (_sessionsCache != null && File.Exists(_sessionsFile))
        {
            var writeTime = File.GetLastWriteTime(_sessionsFile);
            if (writeTime == _sessionsCacheFileTime)
                return _sessionsCache;
        }

        try
        {
            var content = File.ReadAllText(_sessionsFile);
            var sessions = JsonConvert.DeserializeObject<List<FocusSession>>(content) ?? new List<FocusSession>();
            UpdateSessionsCache(sessions);
            return sessions;
        }
        catch
        {
            var empty = new List<FocusSession>();
            UpdateSessionsCache(empty);
            return empty;
        }
    }

    private void UpdateSessionsCache(List<FocusSession> sessions)
    {
        _sessionsCache = sessions;
        _sessionsCacheFileTime = File.Exists(_sessionsFile)
            ? File.GetLastWriteTime(_sessionsFile)
            : DateTime.MinValue;
    }

    /// <summary>内部使用，保留兼容</summary>
    private List<FocusSession> LoadSessionsCore() => GetOrLoadSessions();

    public void SaveSessions(List<FocusSession> sessions)
    {
        lock (_fileLock)
        {
            var content = JsonConvert.SerializeObject(sessions, Formatting.Indented);
            File.WriteAllText(_sessionsFile, content);
            UpdateSessionsCache(sessions);
        }
        InvalidateStatsCache();
    }

    public void AddSession(FocusSession session)
    {
        lock (_fileLock)
        {
            var sessions = GetOrLoadSessions();
            sessions.Add(session);
            SaveSessionsWithTransaction(sessions);
        }
        Log.Information("保存专注会话: {TaskName}, {FocusMinutes} 分钟", session.TaskName, session.FocusMinutes);
    }

    public void UpdateSession(string sessionId, Action<FocusSession> updater)
    {
        lock (_fileLock)
        {
            var sessions = GetOrLoadSessions();
            var session = sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                updater(session);
                SaveSessionsWithTransaction(sessions);
            }
        }
    }

    private void InvalidateStatsCache()
    {
        _cachedTodayStats = null;
    }

    private void UpdateTasksCache(List<TaskItem> tasks)
    {
        _tasksCache = tasks;
        _tasksCacheFileTime = File.Exists(_tasksFile)
            ? File.GetLastWriteTime(_tasksFile)
            : DateTime.MinValue;
    }

    private void SaveSessionsWithTransaction(List<FocusSession> sessions)
    {
        var backupFile = _sessionsFile + ".bak";
        var tempFile = _sessionsFile + ".tmp";

        try
        {
            var content = JsonConvert.SerializeObject(sessions, Formatting.Indented);

            if (File.Exists(_sessionsFile))
            {
                File.Copy(_sessionsFile, backupFile, true);
            }

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

            // 写入成功后更新缓存（避免下次读磁盘）
            UpdateSessionsCache(sessions);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存会话数据失败");
            // 写入失败，缓存可能过时
            _sessionsCache = null;
            if (File.Exists(backupFile))
            {
                File.Copy(backupFile, _sessionsFile, true);
            }
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            throw;
        }
    }

    public DailyStats GetTodayStats()
    {
        lock (_fileLock)
        {
            if (_cachedTodayStats != null && _cacheDate == DateTime.Today)
            {
                return new DailyStats
                {
                    CompletedPomodoros = _cachedTodayStats.CompletedPomodoros,
                    TotalFocusMinutes = _cachedTodayStats.TotalFocusMinutes,
                    TaskStats = new Dictionary<string, int>(_cachedTodayStats.TaskStats),
                    CurrentStreak = _cachedTodayStats.CurrentStreak
                };
            }

            var sessions = GetOrLoadSessions();
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

    private static int CalculateStreak(List<FocusSession> sessions)
    {
        var completed = sessions.Where(s => s.Completed && s.EndTime.HasValue).ToList();
        return InsightEngine.CalculateStreak(completed);
    }
}
