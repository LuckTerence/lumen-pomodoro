using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

/// <summary>
/// 会话数据持久化服务。
/// 内存缓存为真相来源，文件写盘在后台线程异步完成，避免计时完成回调阻塞 UI 线程；
/// 同时对 sessions.json 做数量裁剪以限制长期增长。
/// </summary>
public class StorageService : IStorageService, IDisposable
{
    private readonly string _appDataPath;
    private readonly string _settingsFile;
    private readonly string _tasksFile;
    private readonly string _sessionsFile;
    private readonly string _schemaFile;

    private const int CurrentSchemaVersion = 1;

    // sessions.json 保留的最大会话条数：超出后裁剪最早的会话，限制文件体积与反序列化开销。
    // 该上限足够大（约 27 年每日使用），裁剪最早会话在极端情况下可能影响超长历史的连胜统计，
    // 但可避免文件无限增长导致本地追踪器逐渐变慢。
    private const int MaxRetainedSessions = 10000;

    // 后台写盘任务链：串行执行，保证文件写入顺序与提交顺序一致。
    private readonly object _writeLock = new();
    private Task _writeChain = Task.CompletedTask;
    private bool _disposed;

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
        catch (JsonException) { }
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
            catch (JsonException) { }
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

        // 数据版本高于当前程序版本（来自更新的 App）：只读降级，写回原版本号并告警，不静默写坏。
        if (current > CurrentSchemaVersion)
        {
            Log.Warning("数据 schema 版本 {Stored} 高于当前程序版本 {Current}，将仅读取不降级（请升级 App）", current, CurrentSchemaVersion);
            if (!File.Exists(_schemaFile)) SaveSchemaVersion(current);
            return;
        }

        if (current >= CurrentSchemaVersion)
        {
            // 已是最新：确保 _schema.json 存在以便跨端识别
            if (!File.Exists(_schemaFile)) SaveSchemaVersion(CurrentSchemaVersion);
            return;
        }

        Log.Information("执行数据迁移: V{From} → V{To}", current, CurrentSchemaVersion);

        // 逐版本递增迁移，保证跨多版本升级（V0→V1→V2…）也能逐步执行，避免新增版本后“迁移死路”。
        for (int version = current + 1; version <= CurrentSchemaVersion; version++)
        {
            MigrateToVersion(version);
        }

        SaveSchemaVersion(CurrentSchemaVersion);
        Log.Information("数据迁移完成");
    }

    /// <summary>
    /// 执行从 V(version-1) 到 V(version) 的迁移步骤。新增 schema 版本时只需在此追加分支，
    /// 并提升 <see cref="CurrentSchemaVersion"/>；<see cref="RunMigrations"/> 的循环会自动按序执行所有中间步骤。
    /// </summary>
    private void MigrateToVersion(int version)
    {
        switch (version)
        {
            case 1:
                MigrateV0ToV1();
                break;
            // case 2:
            //     MigrateV1ToV2();
            //     break;
            default:
                Log.Warning("未实现 V{Version} 的迁移步骤，已跳过", version);
                break;
        }
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

    private List<TaskItem>? RecoverTasksFromBackup()
    {
        var backupFile = _tasksFile + ".bak";
        if (!File.Exists(backupFile)) return null;

        try
        {
            var content = File.ReadAllText(backupFile);
            var tasks = JsonSerializer.Deserialize<List<TaskItem>>(content);
            if (tasks != null && tasks.Count > 0)
            {
                Log.Information("从备份文件恢复任务列表成功");
                File.WriteAllText(_tasksFile, content);
                UpdateTasksCache(tasks);
                return new List<TaskItem>(tasks);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "从备份恢复任务列表失败");
        }

        return null;
    }

    private List<FocusSession>? RecoverSessionsFromBackup()
    {
        var backupFile = _sessionsFile + ".bak";
        if (!File.Exists(backupFile)) return null;

        try
        {
            var content = File.ReadAllText(backupFile);
            var sessions = JsonSerializer.Deserialize<List<FocusSession>>(content);
            if (sessions != null)
            {
                Log.Information("从备份文件恢复会话数据成功");
                File.WriteAllText(_sessionsFile, content);
                UpdateSessionsCache(sessions);
                return sessions;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "从备份恢复会话数据失败");
        }

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
            catch (JsonException ex)
            {
                Log.Error(ex, "任务文件 JSON 格式损坏，尝试从备份恢复");
                var recovered = RecoverTasksFromBackup();
                if (recovered != null) return recovered;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载任务文件失败，返回默认任务");
            }

            var defaults = GetDefaultTasks();
            SaveTasks(defaults);
            return defaults;
        }
    }

    private static readonly (string Name, string Category, string Color)[] DefaultTaskData =
    [
        // 考研 (Kaoyan)
        ("考研数学", "数学", "#3B82F6"),
        ("考研英语", "英语", "#10B981"),
        ("考研政治", "政治", "#EF4444"),
        ("专业课", "专业课", "#8B5CF6"),
        ("复盘与整理", "其他", "#6B7280"),
        // 公务员考试 (Civil Service)
        ("行测-言语理解", "行测", "#F59E0B"),
        ("行测-数量关系", "行测", "#F97316"),
        ("行测-判断推理", "行测", "#EF4444"),
        ("行测-资料分析", "行测", "#3B82F6"),
        ("行测-常识判断", "行测", "#8B5CF6"),
        ("申论写作", "申论", "#10B981"),
        // 教师资格证 (Teacher Qualification)
        ("综合素质(教资)", "教资", "#8B5CF6"),
        ("教育知识与能力", "教资", "#3B82F6"),
        ("学科知识", "教资", "#10B981"),
        // 其他考试
        ("英语四级", "英语", "#F59E0B"),
        ("英语六级", "英语", "#EF4444"),
        ("法考复习", "法考", "#8B5CF6"),
        ("注册会计师", "CPA", "#3B82F6"),
        ("雅思备考", "英语", "#06B6D4"),
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

    /// <summary>
    /// 恢复默认预设任务。保留用户自建任务（ID 不以 default_ 开头），
    /// 替换/新增所有默认预设。
    /// </summary>
    public void RestoreDefaultTasks()
    {
        lock (_fileLock)
        {
            var existing = LoadTasks();
            var userTasks = existing.Where(t => !t.Id.StartsWith("default_")).ToList();
            var defaults = GetDefaultTasks();

            var merged = new List<TaskItem>(defaults);
            merged.AddRange(userTasks);

            SaveTasks(merged);
            UpdateTasksCache(merged);
        }
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
        catch (JsonException ex)
        {
            Log.Error(ex, "会话文件 JSON 格式损坏，尝试从备份恢复");
            var recovered = RecoverSessionsFromBackup();
            if (recovered != null) return recovered;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载会话文件失败");
        }

        var empty = new List<FocusSession>();
        UpdateSessionsCache(empty);
        return empty;
    }

    private void UpdateSessionsCache(List<FocusSession> sessions)
    {
        _sessionsCache = sessions;
    }


    public void SaveSessions(List<FocusSession> sessions)
    {
        lock (_fileLock)
        {
            var next = new List<FocusSession>(sessions);
            PruneSessions(next);
            UpdateSessionsCache(next);
            InvalidateStatsCache();
            QueueSessionsWrite(next);
        }
    }

    public void AddSession(FocusSession session)
    {
        lock (_fileLock)
        {
            var sessions = GetOrLoadSessions();
            sessions.Add(session);
            PruneSessions(sessions);
            UpdateSessionsCache(sessions);
            InvalidateStatsCache();
            QueueSessionsWrite(sessions);
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
                UpdateSessionsCache(sessions);
                InvalidateStatsCache();
                QueueSessionsWrite(sessions);
            }
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
            InvalidateStatsCache();
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

    /// <summary>
    /// 将完整会话列表的序列化与写盘投递到后台线程，避免阻塞调用方（UI 线程）。
    /// 通过任务链串行化，保证文件内容最终与最后一次提交一致。
    /// </summary>
    private void QueueSessionsWrite(List<FocusSession> sessions)
    {
        var snapshot = new List<FocusSession>(sessions);
        Task previous;
        lock (_writeLock)
        {
            if (_disposed) return;
            previous = _writeChain;
            _writeChain = previous.ContinueWith(
                _ => WriteSessionsSnapshot(snapshot),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    private void WriteSessionsSnapshot(List<FocusSession> snapshot)
    {
        try
        {
            var content = JsonSerializer.Serialize(snapshot, IndentedOptions);
            AtomicWriteAllText(_sessionsFile, content);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "后台写入会话文件失败");
        }
    }

    /// <summary>
    /// 裁剪最早会话以将数量控制在 <see cref="MaxRetainedSessions"/> 以内。
    /// </summary>
    private void PruneSessions(List<FocusSession> sessions)
    {
        if (sessions.Count <= MaxRetainedSessions) return;

        var overflow = sessions.Count - MaxRetainedSessions;
        Log.Warning("会话数 {Count} 超过上限 {Max}，裁剪最早的 {Overflow} 条以限制 sessions.json 体积",
            sessions.Count, MaxRetainedSessions, overflow);
        sessions.RemoveRange(0, overflow);
    }

    /// <summary>
    /// 应用退出时由 DI 容器调用：等待后台写盘任务链完成，尽量不丢失最近一次会话。
    /// </summary>
    public void Dispose()
    {
        Task chain;
        lock (_writeLock)
        {
            _disposed = true;
            chain = _writeChain;
        }

        try
        {
            chain.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "等待会话写盘完成超时，可能存在未落盘数据");
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
            var todaySessions = sessions.Where(s => s.Completed && s.StartTime.Date == today).ToList();

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
        var completed = sessions.Where(s => s.Completed).ToList();
        return InsightEngine.CalculateStreak(completed);
    }
}
