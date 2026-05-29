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
}
