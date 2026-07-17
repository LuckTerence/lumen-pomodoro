using System.IO;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Tests.Services;

/// <summary>
/// 跨端黄金测试（Win 侧）：同一份 sessions 在 Win / Mac 必须产出一致的连胜与「今日」统计。
/// 归属规则（见 docs/cross-platform-contract.md §3.6）：一个 FocusSession 归属于它「开始当天」(StartTime)，
/// 而非结束当天 (EndTime)。这样跨零点（今晚开始、明早结束）的番茄计入开始当天，两端一致。
/// Mac 侧等价测试见 LumenPomodoroMac 的 XCTest（Task #7）。
/// </summary>
public class GoldenStatsTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly StorageService _storage;

    public GoldenStatsTests()
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), "LumenPomodoro.Golden", Guid.NewGuid().ToString("N"));
        _storage = new StorageService(_testDataPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataPath)) Directory.Delete(_testDataPath, true);
    }

    [Fact]
    public void MidnightSpanningSession_BelongsToStartDay_NotEndDay()
    {
        // 今晚 23:00 开始，明早 00:10 结束 → 应计入「今天」(StartTime 当天)
        var session = new FocusSession
        {
            Id = "midnight",
            Completed = true,
            StartTime = DateTime.Today.AddHours(23),
            EndTime = DateTime.Today.AddDays(1).AddMinutes(10),
            FocusMinutes = 25
        };
        _storage.AddSession(session);

        var stats = _storage.GetTodayStats();
        Assert.Equal(1, stats.CompletedPomodoros);
        Assert.Equal(25, stats.TotalFocusMinutes);
        // 连胜从今天起算为 1
        Assert.Equal(1, stats.CurrentStreak);
    }

    [Fact]
    public void Streak_ConsecutiveDaysIncludingYesterday()
    {
        // 前天、昨天各有一次完成，今天无 → 连胜=2
        var sessions = new List<FocusSession>
        {
            new() { Id = "d2", Completed = true, StartTime = DateTime.Today.AddDays(-2).AddHours(10), FocusMinutes = 25 },
            new() { Id = "d1", Completed = true, StartTime = DateTime.Today.AddDays(-1).AddHours(10), FocusMinutes = 25 },
        };
        foreach (var s in sessions) _storage.AddSession(s);

        Assert.Equal(2, _storage.GetTodayStats().CurrentStreak);
    }

    [Fact]
    public void Streak_BrokenByGapGreaterThanOneDay_IsZero()
    {
        // 仅在 3 天前完成 → 连胜中断为 0
        _storage.AddSession(new FocusSession
        {
            Id = "old",
            Completed = true,
            StartTime = DateTime.Today.AddDays(-3).AddHours(10),
            FocusMinutes = 25
        });

        Assert.Equal(0, _storage.GetTodayStats().CurrentStreak);
    }

    [Fact]
    public void TodayFilter_IgnoresSessionsStartedOnOtherDays()
    {
        var today = DateTime.Today;
        _storage.AddSession(new FocusSession
        {
            Id = "today",
            Completed = true,
            StartTime = today.AddHours(9),
            EndTime = today.AddHours(9).AddMinutes(25),
            FocusMinutes = 25
        });
        _storage.AddSession(new FocusSession
        {
            Id = "yesterday",
            Completed = true,
            StartTime = today.AddDays(-1).AddHours(9),
            EndTime = today.AddDays(-1).AddHours(9).AddMinutes(25),
            FocusMinutes = 25
        });

        var stats = _storage.GetTodayStats();
        Assert.Equal(1, stats.CompletedPomodoros); // 仅今天开始的那条
    }
}
