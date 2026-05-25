using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    private readonly string _schemaFile;

    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private DailyStats? _cachedTodayStats;
    private DateTime _cacheDate;
    private readonly object _fileLock = new object();

    // Sessions 内存缓存 — 避免重复 JSON 反序列化
    private List<FocusSession>? _sessionsCache;

    // Tasks 内存缓存
    private List<TaskItem>? _tasksCache;


    public StorageService(string? appDataPath = null)
    {
        _appDataPath = appDataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenPomodoro");
        Directory.CreateDirectory(_appDataPath);

        _settingsFile = Path.Combine(_appDataPath, "settings.json");
        _tasksFile = Path.Combine(_appDataPath, "tasks.json");
        _sessionsFile = Path.Combine(_appDataPath, "sessions.json");
        _schemaFile = Path.Combine(_appDataPath, "_schema.json");

        RunMigrations();

        Log.Debug("StorageService 初始化，数据路径: {Path}", _appDataPath);
    }

    private int GetStoredSchemaVersion()
    {
        try
        {
            if (File.Exists(_schemaFile))
            {
                var json = File.ReadAllText(_schemaFile);
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                if (doc.TryGetProperty("schema_version", out var version) && version.TryGetInt32(out var v))
                    return v;
            }
        }
        catch { }
        // 无 _schema.json 时，检查 settings.json 中的 SchemaVersion
        if (File.Exists(_settingsFile))
        {
            try
            {
                var json = File.ReadAllText(_settingsFile);
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                if (doc.TryGetProperty("SchemaVersion", out var version) && version.TryGetInt32(out var v))
                    return v;
            }
            catch { }
        }
        return 0;
    }

    private void SaveSchemaVersion(int version)
    {
        var meta = new { schema_version = version, updated_at = DateTime.Now.ToString("O") };
        File.WriteAllText(_schemaFile,
            JsonSerializer.Serialize(meta, IndentedOptions));
    }

    private void RunMigrations()
    {
        var current = GetStoredSchemaVersion();
        if (current >= CurrentSchemaVersion) return;

        Log.Information("执行数据迁移: V{From} → V{To}", current, CurrentSchemaVersion);

        // V0 → V1: 初始化 schema 版本，无数据结构变更
        if (current < 1)
        {
            MigrateV0ToV1();
        }

        SaveSchemaVersion(CurrentSchemaVersion);
        Log.Information("数据迁移完成");
    }

    private void MigrateV0ToV1()
    {
        // V0 数据无 SchemaVersion 字段，升级到 V1 只需写入版本号
        // 如果 settings.json 已存在，补写 SchemaVersion
        if (File.Exists(_settingsFile))
        {
            try
            {
                var json = File.ReadAllText(_settingsFile);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null && settings.SchemaVersion == 0)
                {
                    settings.SchemaVersion = 1;
                    SaveSettings(settings);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "V0→V1 迁移 settings.json 失败，将保留原文件");
            }
        }
    }

    public Settings LoadSettings()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_settingsFile))
            {
                Log.Debug("配置文件不存在，返回默认设置");
                return new Settings();
            }

            try
            {
                var content = File.ReadAllText(_settingsFile);
                var settings = JsonSerializer.Deserialize<Settings>(content);
                if (settings == null)
                {
                    Log.Warning("配置文件反序列化返回null，返回默认设置");
                    return TryRecoverSettingsFromBackup() ?? new Settings();
                }
                return settings;
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "配置文件JSON格式错误，尝试从备份恢复");
                return TryRecoverSettingsFromBackup() ?? new Settings();
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error(ex, "配置文件访问被拒绝");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载配置文件失败");
                return TryRecoverSettingsFromBackup() ?? new Settings();
            }
        }
    }

    private Settings? TryRecoverSettingsFromBackup()
    {
        var backupFile = _settingsFile + ".bak";
        if (File.Exists(backupFile))
        {
            try
            {
                var content = File.ReadAllText(backupFile);
                var settings = JsonSerializer.Deserialize<Settings>(content);
                if (settings != null)
                {
                    Log.Information("从备份文件恢复配置成功");
                    // 恢复主配置文件
                    File.WriteAllText(_settingsFile, content);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "从备份恢复配置失败");
            }
        }
        Log.Warning("无可用备份，返回默认设置");
        return null;
    }

    private void CreateBackup(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var backupFile = filePath + ".bak";
                File.Copy(filePath, backupFile, true);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "创建备份文件失败");
        }
    }

    /// <summary>
    /// 使用临时文件实现原子写入：先写 .tmp，再 File.Replace 到目标文件。
    /// 防止进程崩溃时 JSON 文件损坏。
    /// </summary>
    private static void AtomicWriteAllText(string filePath, string content)
    {
        var tempFile = filePath + ".tmp";
        File.WriteAllText(tempFile, content);

        if (File.Exists(filePath))
        {
            File.Replace(tempFile, filePath, filePath + ".bak");
        }
        else
        {
            File.Move(tempFile, filePath);
        }
    }

    public void SaveSettings(Settings settings)
    {
        lock (_fileLock)
        {
            CreateBackup(_settingsFile);
            var content = JsonSerializer.Serialize(settings, IndentedOptions);
            AtomicWriteAllText(_settingsFile, content);
        }
    }

    public List<TaskItem> LoadTasks()
    {
        lock (_fileLock)
        {
            if (_tasksCache != null)
                return new List<TaskItem>(_tasksCache);

            try
            {
                var content = File.ReadAllText(_tasksFile);
                var tasks = JsonSerializer.Deserialize<List<TaskItem>>(content);
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
            CreateBackup(_tasksFile);
            var content = JsonSerializer.Serialize(tasks, IndentedOptions);
            AtomicWriteAllText(_tasksFile, content);
            UpdateTasksCache(tasks);
        }
    }

    private static readonly (string Name, string Category, string Color)[] DefaultTaskData =
    [
        ("考研数学", "数学", "#3B82F6"),
        ("考研英语", "英语", "#10B981"),
        ("考研政治", "政治", "#EF4444"),
        ("专业课", "专业课", "#8B5CF6"),
        ("复盘与整理", "其他", "#6B7280"),
    ];

    private List<TaskItem> GetDefaultTasks()
    {
        return DefaultTaskData.Select((t, i) => new TaskItem
        {
            Id = $"default_{i}",
            Name = t.Name,
            Category = t.Category,
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
        if (_sessionsCache != null)
            return _sessionsCache;

        try
        {
            var content = File.ReadAllText(_sessionsFile);
            var sessions = JsonSerializer.Deserialize<List<FocusSession>>(content) ?? new List<FocusSession>();
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
    }

    /// <summary>内部使用，保留兼容</summary>
    private List<FocusSession> LoadSessionsCore() => GetOrLoadSessions();

    public void SaveSessions(List<FocusSession> sessions)
    {
        lock (_fileLock)
        {
            var content = JsonSerializer.Serialize(sessions, IndentedOptions);
            AtomicWriteAllText(_sessionsFile, content);
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
    }

    private void SaveSessionsWithTransaction(List<FocusSession> sessions)
    {
        var backupFile = _sessionsFile + ".bak";
        var tempFile = _sessionsFile + ".tmp";

        try
        {
            var content = JsonSerializer.Serialize(sessions, IndentedOptions);

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
