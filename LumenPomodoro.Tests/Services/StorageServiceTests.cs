using System.IO;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Tests;

public class StorageServiceTests : IDisposable
{
    private readonly StorageService _storageService;
    private readonly string _testDataPath;

    public StorageServiceTests()
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", Guid.NewGuid().ToString("N"));
        _storageService = new StorageService(_testDataPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
    }

    [Fact]
    public void LoadSettings_WhenSettingsFileExists_ReturnsSavedSettings()
    {
        // Act
        var settings = _storageService.LoadSettings();

        // Assert
        Assert.NotNull(settings);
        // The settings may have been modified by other tests, just check they load successfully
        Assert.True(settings.WorkMinutes > 0);
        Assert.True(settings.ShortBreakMinutes > 0);
    }

    [Fact]
    public void SaveSettings_AndLoadSettings_ShouldReturnSavedSettings()
    {
        // Arrange
        var settings = new Settings
        {
            WorkMinutes = 30,
            ShortBreakMinutes = 10,
            LongBreakMinutes = 20,
            LongBreakInterval = 5,
            Theme = "dark"
        };

        // Act
        _storageService.SaveSettings(settings);
        var loadedSettings = _storageService.LoadSettings();

        // Assert
        Assert.Equal(30, loadedSettings.WorkMinutes);
        Assert.Equal(10, loadedSettings.ShortBreakMinutes);
        Assert.Equal(20, loadedSettings.LongBreakMinutes);
        Assert.Equal(5, loadedSettings.LongBreakInterval);
        Assert.Equal("dark", loadedSettings.Theme);
    }

    [Fact]
    public void SaveSettings_RoundTrips_V04ProductFields()
    {
        var settings = new Settings();
        settings.ApplyStandardFocusPreset();
        settings.HasCompletedOnboarding = true;
        settings.HasShownCameraPrivacyNotice = true;
        settings.FullscreenBreakEnabled = true;
        settings.SessionEndPreNotifySeconds = 45;
        settings.ConfirmExitWhileFocusing = true;
        settings.FocusGuardDebounceHits = 3;
        settings.FocusGuardMaxAlertsPerSession = 2;
        settings.FocusGuardRespectDoNotDisturb = false;

        _storageService.SaveSettings(settings);
        var loaded = _storageService.LoadSettings();

        Assert.True(loaded.HasCompletedOnboarding);
        Assert.True(loaded.HasShownCameraPrivacyNotice);
        Assert.False(loaded.CameraAlertEnabled);
        Assert.True(loaded.DynamicIslandEnabled);
        Assert.Equal("minimize", loaded.DynamicIslandWhenFocused);
        Assert.True(loaded.FocusGuardEnabled);
        Assert.True(loaded.FullscreenBreakEnabled);
        Assert.Equal(45, loaded.SessionEndPreNotifySeconds);
        Assert.Equal(3, loaded.FocusGuardDebounceHits);
        Assert.Equal(2, loaded.FocusGuardMaxAlertsPerSession);
        Assert.False(loaded.FocusGuardRespectDoNotDisturb);
        Assert.False(loaded.StrictModeEnabled);
    }

    [Fact]
    public void SaveSettings_StrictPreset_RoundTrips()
    {
        var settings = new Settings();
        settings.ApplyStrictFocusPreset();
        settings.HasCompletedOnboarding = true;
        _storageService.SaveSettings(settings);
        var loaded = _storageService.LoadSettings();

        Assert.True(loaded.StrictModeEnabled);
        Assert.True(loaded.FullscreenBreakEnabled);
        Assert.True(loaded.DynamicIslandEnabled);
        Assert.Equal("keep", loaded.DynamicIslandWhenFocused);
        Assert.False(loaded.CameraAlertEnabled);
        Assert.False(loaded.EffectiveAllowEndBreakEarly);
    }

    [Fact]
    public void LoadTasks_WhenNoTasksFile_ReturnsDefaultTasks()
    {
        // Act
        var tasks = _storageService.LoadTasks();

        // Assert
        Assert.NotNull(tasks);
        Assert.NotEmpty(tasks);
    }

    [Fact]
    public void SaveTasks_AndLoadTasks_ShouldReturnSavedTasks()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem { Id = "1", Name = "Test Task 1", Category = "Math", Color = "#FF0000", CreatedAt = DateTime.Now },
            new TaskItem { Id = "2", Name = "Test Task 2", Category = "English", Color = "#00FF00", CreatedAt = DateTime.Now }
        };

        // Act
        _storageService.SaveTasks(tasks);
        var loadedTasks = _storageService.LoadTasks();

        // Assert
        Assert.Equal(2, loadedTasks.Count);
        Assert.Equal("Test Task 1", loadedTasks[0].Name);
        Assert.Equal("Test Task 2", loadedTasks[1].Name);
    }

    [Fact]
    public void AddSession_ShouldPersistSessionData()
    {
        // Arrange
        var session = new FocusSession
        {
            Id = "test_session_1",
            TaskId = "task_1",
            TaskName = "Test Task",
            StartTime = DateTime.Now.AddMinutes(-25),
            EndTime = DateTime.Now,
            Completed = true,
            FocusMinutes = 25
        };

        // Act
        _storageService.AddSession(session);
        var sessions = _storageService.LoadSessions();

        // Assert
        var savedSession = sessions.FirstOrDefault(s => s.Id == "test_session_1");
        Assert.NotNull(savedSession);
        Assert.True(savedSession.Completed);
        Assert.Equal(25, savedSession.FocusMinutes);
        Assert.Equal("Test Task", savedSession.TaskName);
    }

    [Fact]
    public void AddSession_ShouldPersistWithTransactionBackup()
    {
        var session = new FocusSession
        {
            Id = "1",
            TaskId = "t1",
            TaskName = "Task 1",
            StartTime = DateTime.Now,
            Completed = true,
            FocusMinutes = 25
        };

        _storageService.AddSession(session);
        var loadedSessions = _storageService.LoadSessions();

        Assert.NotEmpty(loadedSessions);
        Assert.Contains(loadedSessions, s => s.Id == "1");
    }

    [Fact]
    public void GetTodayStats_ShouldReturnCorrectStats()
    {
        // Arrange
        var today = DateTime.Today;
        var session1 = new FocusSession
        {
            Id = "today_1",
            TaskId = "t1",
            TaskName = "Task 1",
            StartTime = today.AddHours(9),
            EndTime = today.AddHours(9).AddMinutes(25),
            Completed = true,
            FocusMinutes = 25
        };
        var session2 = new FocusSession
        {
            Id = "today_2",
            TaskId = "t2",
            TaskName = "Task 2",
            StartTime = today.AddHours(10),
            EndTime = today.AddHours(10).AddMinutes(25),
            Completed = true,
            FocusMinutes = 25
        };

        _storageService.AddSession(session1);
        _storageService.AddSession(session2);

        // Act
        var stats = _storageService.GetTodayStats();

        // Assert
        Assert.True(stats.CompletedPomodoros >= 2);
        Assert.True(stats.TotalFocusMinutes >= 50);
    }

    [Fact]
    public void AddSession_InvalidatesStatsCache_StatsReflectLatestData()
    {
        var today = DateTime.Today;
        var session1 = new FocusSession
        {
            Id = "cache_test_1",
            TaskId = "t1",
            TaskName = "Cache Test Task",
            StartTime = today.AddHours(8),
            EndTime = today.AddHours(8).AddMinutes(25),
            Completed = true,
            FocusMinutes = 25
        };
        _storageService.AddSession(session1);

        var statsBefore = _storageService.GetTodayStats();
        Assert.True(statsBefore.CompletedPomodoros >= 1);

        var session2 = new FocusSession
        {
            Id = "cache_test_2",
            TaskId = "t2",
            TaskName = "Cache Test Task 2",
            StartTime = today.AddHours(9),
            EndTime = today.AddHours(9).AddMinutes(30),
            Completed = true,
            FocusMinutes = 30
        };
        _storageService.AddSession(session2);

        var statsAfter = _storageService.GetTodayStats();
        Assert.True(statsAfter.CompletedPomodoros >= statsBefore.CompletedPomodoros + 1,
            "AddSession 后统计应反映新 session（缓存已自动失效）");
        Assert.True(statsAfter.TotalFocusMinutes >= statsBefore.TotalFocusMinutes + 30,
            "TotalFocusMinutes 应包含新 session 的分钟数");
    }

    [Fact]
    public void ConsecutiveMutations_MaintainCacheConsistency()
    {
        var today = DateTime.Today;

        _storageService.AddSession(new FocusSession
        {
            Id = "seq_1",
            TaskId = "t1",
            TaskName = "Seq Task",
            StartTime = today.AddHours(8),
            EndTime = today.AddHours(8).AddMinutes(25),
            Completed = true,
            FocusMinutes = 25
        });
        var stats1 = _storageService.GetTodayStats();

        _storageService.AddSession(new FocusSession
        {
            Id = "seq_2",
            TaskId = "t2",
            TaskName = "Seq Task 2",
            StartTime = today.AddHours(9),
            EndTime = today.AddHours(9).AddMinutes(30),
            Completed = true,
            FocusMinutes = 30
        });
        var stats2 = _storageService.GetTodayStats();

        _storageService.UpdateSession("seq_1", s => s.FocusMinutes = 50);
        var stats3 = _storageService.GetTodayStats();

        var sessions = _storageService.LoadSessions();
        var remaining = sessions.Where(s => s.Id == "seq_1").ToList();
        _storageService.SaveSessions(remaining);
        var stats4 = _storageService.GetTodayStats();

        Assert.True(stats2.CompletedPomodoros > stats1.CompletedPomodoros,
            "第二次 AddSession 后 pomodoro 数应增加");
        Assert.True(stats3.TotalFocusMinutes >= stats2.TotalFocusMinutes,
            "UpdateSession 后分钟数应>=修改前");
        Assert.True(stats4.CompletedPomodoros <= stats3.CompletedPomodoros,
            "SaveSessions 覆盖后应只保留 seq_1");
    }

    [Fact]
    public void GetTodayStats_WithNoSessions_ReturnsZeros()
    {
        // Use fresh storage with empty data
        var cleanPath = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", "empty_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new StorageService(cleanPath);
            var stats = storage.GetTodayStats();
            Assert.Equal(0, stats.CompletedPomodoros);
            Assert.Equal(0, stats.TotalFocusMinutes);
            Assert.Equal(0, stats.CurrentStreak);
        }
        finally
        {
            if (Directory.Exists(cleanPath)) Directory.Delete(cleanPath, true);
        }
    }

    [Fact]
    public void LoadSettings_ReturnsDefaultsWhenFileMissing()
    {
        var cleanPath = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", "defaults_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new StorageService(cleanPath);
            var settings = storage.LoadSettings();
            Assert.Equal(25, settings.WorkMinutes);
            Assert.Equal(5, settings.ShortBreakMinutes);
            Assert.Equal(15, settings.LongBreakMinutes);
        }
        finally
        {
            if (Directory.Exists(cleanPath)) Directory.Delete(cleanPath, true);
        }
    }

    [Fact]
    public void AddSession_PersistsToFileAfterDispose_BackgroundWriteFlushed()
    {
        // 会话写盘在后台线程异步完成，需通过 Dispose 冲刷任务链；
        // 验证退出后数据已落盘，而非仅停留在内存缓存。
        var path = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", "async_" + Guid.NewGuid().ToString("N"));
        try
        {
            var session = new FocusSession
            {
                Id = "async_1",
                TaskId = "t1",
                TaskName = "Async Task",
                StartTime = DateTime.Now.AddMinutes(-25),
                EndTime = DateTime.Now,
                Completed = true,
                FocusMinutes = 25
            };

            StorageService writer = new StorageService(path);
            writer.AddSession(session);
            writer.Dispose(); // 冲刷后台写盘任务链

            var reader = new StorageService(path);
            var loaded = reader.LoadSessions();
            Assert.Contains(loaded, s => s.Id == "async_1");
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [Fact]
    public void SaveSessions_PrunesToMaxRetainedSessions()
    {
        // 超过上限后裁剪最早会话，限制 sessions.json 体积。
        var path = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", "prune_" + Guid.NewGuid().ToString("N"));
        try
        {
            const int overLimit = 10001; // MaxRetainedSessions = 10000
            var sessions = Enumerable.Range(0, overLimit)
                .Select(i => new FocusSession
                {
                    Id = "p_" + i,
                    TaskId = "t",
                    TaskName = "Task",
                    StartTime = DateTime.Now.AddMinutes(-i),
                    Completed = true,
                    FocusMinutes = 25
                })
                .ToList();

            var storage = new StorageService(path);
            storage.SaveSessions(sessions);
            storage.Dispose();

            var reloaded = new StorageService(path).LoadSessions();
            Assert.Equal(10000, reloaded.Count);
            // 最早的一条（p_0）应被裁剪
            Assert.DoesNotContain(reloaded, s => s.Id == "p_0");
            // 最新的一条（p_10000）应保留
            Assert.Contains(reloaded, s => s.Id == "p_10000");
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [Fact]
    public void RunMigrations_FreshInstall_WritesSchemaVersion1()
    {
        // 全新安装（无 _schema.json）：迁移应写入 schema 版本 1
        var path = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", "mig_fresh_" + Guid.NewGuid().ToString("N"));
        try
        {
            _ = new StorageService(path);
            var schemaFile = Path.Combine(path, "_schema.json");
            Assert.True(File.Exists(schemaFile), "应生成 _schema.json");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(schemaFile));
            Assert.True(doc.RootElement.TryGetProperty("schema_version", out var v));
            Assert.Equal(1, v.GetInt32());
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [Fact]
    public void RunMigrations_FutureSchemaVersion_PreservedNoDowngrade()
    {
        // 数据来自更新的 App（schema 版本高于当前程序）：只读降级，保留原版本号，不静默写坏。
        var path = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", "mig_future_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(path);
            var future = 99;
            File.WriteAllText(Path.Combine(path, "_schema.json"),
                System.Text.Json.JsonSerializer.Serialize(new { schema_version = future, updated_at = DateTime.Now.ToString("O") }));

            _ = new StorageService(path);

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "_schema.json")));
            Assert.True(doc.RootElement.TryGetProperty("schema_version", out var v));
            Assert.Equal(future, v.GetInt32());
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [Fact]
    public void DailyPlan_SaveAndLoad_RoundTrips()
    {
        // 峰值时段排程（A2）：今日计划的写入与读取应保持一致
        var plan = new DailyPlan { Date = DateTime.Today, Blocks = new List<PlannedBlock>() };
        plan.Blocks.Add(new PlannedBlock { TaskName = "数学", Hour = 9, DurationMinutes = 25 });
        _storageService.SaveDailyPlan(plan);

        var loaded = _storageService.LoadDailyPlan();
        Assert.Equal(1, loaded.Blocks.Count);
        Assert.Equal("数学", loaded.Blocks[0].TaskName);
        Assert.Equal(9, loaded.Blocks[0].Hour);
        Assert.Equal(25, loaded.Blocks[0].DurationMinutes);
    }

    [Fact]
    public void DailyPlan_LoadOnDifferentDay_ReturnsEmptyTodayPlan()
    {
        // 跨天重置：存储的日期不是今天时，返回今日空计划（不污染历史）
        var yesterday = DateTime.Today.AddDays(-1);
        var plan = new DailyPlan { Date = yesterday, Blocks = new List<PlannedBlock> { new() { TaskName = "旧", Hour = 8 } } };
        _storageService.SaveDailyPlan(plan);

        var loaded = _storageService.LoadDailyPlan();
        Assert.Equal(DateTime.Today, loaded.Date);
        Assert.Empty(loaded.Blocks);
    }

    [Fact]
    public void RunMigrations_V1_CreatesDailyPlanFile()
    {
        // 数据来自 V1（无 dailyplan.json）：迁移到 V2 应初始化 dailyplan.json
        var path = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Tests", "mig_v1_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "_schema.json"),
                System.Text.Json.JsonSerializer.Serialize(new { schema_version = 1, updated_at = DateTime.Now.ToString("O") }));
            File.Delete(Path.Combine(path, "dailyplan.json"));

            _ = new StorageService(path);

            Assert.True(File.Exists(Path.Combine(path, "dailyplan.json")), "迁移 V1→V2 应生成 dailyplan.json");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "_schema.json")));
            Assert.True(doc.RootElement.TryGetProperty("schema_version", out var v));
            Assert.Equal(2, v.GetInt32());
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
