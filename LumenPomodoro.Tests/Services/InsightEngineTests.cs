using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Tests.Services;

public class InsightEngineTests
{
    private readonly InsightEngine _engine = new();

    [Fact]
    public void GetHeatmapData_WithNoSessions_ReturnsMinDays()
    {
        var result = _engine.GetHeatmapData([]);
        Assert.Equal(90, result.Count);
        Assert.All(result, day => Assert.Equal(0, day.IntensityLevel));
    }

    [Fact]
    public void GetHeatmapData_WithSessions_ComputesCorrectIntensity()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today.AddHours(10), FocusMinutes = 100 },
            new() { Completed = true, EndTime = DateTime.Today.AddDays(-1).AddHours(10), FocusMinutes = 25 },
        };
        var result = _engine.GetHeatmapData(sessions);
        var today = result.First(d => d.Date == DateTime.Today);
        Assert.Equal(4, today.IntensityLevel);
        var yesterday = result.First(d => d.Date == DateTime.Today.AddDays(-1));
        Assert.True(yesterday.IntensityLevel > 0);
    }

    [Fact]
    public void GetHourlyDistribution_GroupsCorrectlyByHour()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today.AddHours(9), FocusMinutes = 25 },
            new() { Completed = true, EndTime = DateTime.Today.AddHours(9).AddMinutes(30), FocusMinutes = 25 },
            new() { Completed = true, EndTime = DateTime.Today.AddHours(14), FocusMinutes = 25 },
        };
        var result = _engine.GetHourlyDistribution(sessions, DateTime.Today, DateTime.Today);
        Assert.Equal(24, result.Count);
        Assert.Equal(50, result[9].TotalMinutes);
        Assert.Equal(2, result[9].SessionCount);
        Assert.Equal(25, result[14].TotalMinutes);
        Assert.Equal(0, result[0].TotalMinutes);
    }

    [Fact]
    public void GetTaskBreakdown_ComputesPercentages()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today, TaskName = "数学", FocusMinutes = 25 },
            new() { Completed = true, EndTime = DateTime.Today, TaskName = "数学", FocusMinutes = 25 },
            new() { Completed = true, EndTime = DateTime.Today, TaskName = "英语", FocusMinutes = 25 },
        };
        var result = _engine.GetTaskBreakdown(sessions, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
        Assert.Equal(2, result.Count);
        var math = result.First(s => s.TaskName == "数学");
        Assert.Equal(2, math.PomodoroCount);
        Assert.InRange(math.Percentage, 66.0, 67.0);
    }

    [Fact]
    public void GetTaskBreakdown_EmptyReturnsEmpty()
    {
        var result = _engine.GetTaskBreakdown([], DateTime.Today, DateTime.Today);
        Assert.Empty(result);
    }

    [Fact]
    public void GetWeeklyTrend_Returns8DataPoints()
    {
        var sessions = new List<FocusSession>();
        for (int w = 0; w < 10; w++)
        {
            for (int d = 0; d < 5; d++)
            {
                sessions.Add(new FocusSession
                {
                    Completed = true,
                    EndTime = DateTime.Today.AddDays(-w * 7 - d).AddHours(10),
                    FocusMinutes = 25
                });
            }
        }
        var result = _engine.GetWeeklyTrend(sessions);
        Assert.Equal(8, result.Count);
    }

    [Fact]
    public void GetInsights_WithEnoughData_ReturnsPeakHour()
    {
        var sessions = Enumerable.Range(0, 20).Select(i => new FocusSession
        {
            Completed = true,
            EndTime = DateTime.Today.AddDays(-i).AddHours(9),
            FocusMinutes = 25
        }).ToList();

        var insights = _engine.GetInsights(sessions, []);
        Assert.Contains(insights, i => i.Type == InsightType.PeakHour);
    }

    [Fact]
    public void GetInsights_WithStreak_ReturnsStreakInsight()
    {
        var sessions = Enumerable.Range(0, 5).Select(i => new FocusSession
        {
            Completed = true,
            StartTime = DateTime.Today.AddDays(-i).AddHours(9),
            EndTime = DateTime.Today.AddDays(-i).AddHours(10),
            FocusMinutes = 25
        }).ToList();

        var insights = _engine.GetInsights(sessions, []);
        Assert.Contains(insights, i => i.Type == InsightType.Streak);
    }

    [Fact]
    public void GetInsights_WithNoData_ReturnsMotivationFallback()
    {
        var insights = _engine.GetInsights([], []);
        Assert.Single(insights);
        Assert.Equal(InsightType.Motivation, insights[0].Type);
    }

    [Fact]
    public void GetInsights_ReturnsMax5Insights()
    {
        var sessions = new List<FocusSession>();
        for (int i = 0; i < 60; i++)
        {
            sessions.Add(new FocusSession
            {
                Completed = true,
                EndTime = DateTime.Today.AddDays(-i % 10).AddHours(8 + i % 12),
                TaskName = i % 2 == 0 ? "数学" : "英语",
                FocusMinutes = 25
            });
        }

        var insights = _engine.GetInsights(sessions, [new TaskItem { Name = "数学" }, new TaskItem { Name = "英语" }]);
        Assert.True(insights.Count <= 5);
        Assert.True(insights.Count > 0);
    }

    [Fact]
    public void GetGoalProgress_WithZeroGoals_ReturnsEmpty()
    {
        var result = _engine.GetGoalProgress([], 0, 0);
        Assert.Empty(result);
    }

    [Fact]
    public void GetGoalProgress_WithDailyGoal_ComputesCorrectly()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today.AddHours(10), FocusMinutes = 60 },
            new() { Completed = true, EndTime = DateTime.Today.AddHours(11), FocusMinutes = 30 },
        };
        var result = _engine.GetGoalProgress(sessions, 120, 600);
        Assert.Equal(2, result.Count);
        var daily = result.First(r => r.Label == "每日目标");
        Assert.Equal(90, daily.CurrentMinutes);
        Assert.Equal(120, daily.TargetMinutes);
        Assert.Equal(75.0, daily.ProgressPercent, 1);
        Assert.False(daily.IsCompleted);
    }

    [Fact]
    public void GetGoalProgress_DailyOverTarget_IsCompleted()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today.AddHours(9), FocusMinutes = 150 },
        };
        var result = _engine.GetGoalProgress(sessions, 120, 0);
        var daily = result.First(r => r.Label == "每日目标");
        Assert.True(daily.IsCompleted);
        Assert.Equal(100.0, daily.ProgressPercent, 1);
    }

    [Fact]
    public void CalculateStreak_SingleYesterdaySession_Returns1()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, StartTime = DateTime.Today.AddDays(-1).AddHours(10), FocusMinutes = 25 },
        };
        // 昨天有一次完成 → 连胜从昨天起算为 1（今天无记录也计入）
        Assert.Equal(1, InsightEngine.CalculateStreak(sessions));
    }

    [Fact]
    public void CalculateStreak_GapGreaterThanOneDay_Returns0()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, StartTime = DateTime.Today.AddDays(-3).AddHours(10), FocusMinutes = 25 },
        };
        // 最近一次完成距今天 > 1 天 → 连胜中断为 0
        Assert.Equal(0, InsightEngine.CalculateStreak(sessions));
    }

    [Fact]
    public void CalculateStreak_ConsecutiveDays_CountsBackward()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, StartTime = DateTime.Today.AddDays(-2).AddHours(10), FocusMinutes = 25 },
            new() { Completed = true, StartTime = DateTime.Today.AddDays(-1).AddHours(10), FocusMinutes = 25 },
            // 今天无 session，昨天与前天连续
        };
        Assert.Equal(2, InsightEngine.CalculateStreak(sessions));
    }

    [Fact]
    public void GetHeatmapData_AllFutureSessions_AllZeroIntensity()
    {
        var result = _engine.GetHeatmapData([]);
        Assert.All(result, day => Assert.Equal(0, day.IntensityLevel));
    }

    [Fact]
    public void GetWeeklyTrend_EmptySessions_Returns8ZeroWeeks()
    {
        var result = _engine.GetWeeklyTrend([]);
        Assert.Equal(8, result.Count);
        Assert.All(result, week =>
        {
            Assert.Equal(0, week.TotalMinutes);
            Assert.Equal(0, week.CompletedPomodoros);
        });
    }

    [Fact]
    public void AllInsightMethods_IgnoreNullEndTimeSessions_NoThrow()
    {
        // 契约 §3.6：Completed=true 但无 EndTime 的脏数据，统计应忽略而非崩溃
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today.AddHours(9), FocusMinutes = 25, TaskName = "数学" },
            new() { Completed = true, EndTime = DateTime.Today.AddDays(-1).AddHours(9), FocusMinutes = 25, TaskName = "数学" },
            new() { Completed = true, EndTime = null, FocusMinutes = 25, TaskName = "英语" },
        };
        var start = DateTime.Today.AddDays(-7);
        var end = DateTime.Today;
        var tasks = new List<TaskItem> { new() { Name = "数学" }, new() { Name = "英语" } };

        // 以下方法曾因对无 EndTime 的 session 执行 EndTime!.Value 而空引用崩溃
        _ = _engine.GetHeatmapData(sessions);
        _ = _engine.GetHourlyDistribution(sessions, start, end);
        _ = _engine.GetTaskBreakdown(sessions, start, end, tasks);
        _ = _engine.GetWeeklyTrend(sessions);
        _ = _engine.GetInsights(sessions, tasks);
        _ = _engine.GetGoalProgress(sessions, 120, 600);
        _ = _engine.GetComparisons(sessions);
    }

    [Fact]
    public void ShouldShowStreakEncouragement_AllNullEndTime_DoesNotThrow()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = null, FocusMinutes = 25 },
            new() { Completed = true, EndTime = null, FocusMinutes = 25 },
        };
        // MaxBy(EndTime) 在全部 EndTime 为空时会选中空键，历史版本此处 EndTime!.Value 崩溃
        var result = SessionScoringController.ShouldShowStreakEncouragement(sessions);
        Assert.False(result);
    }

    [Fact]
    public void GetInsights_WeakSubject_ReturnsStartFocusAction()
    {
        // 洞察→行动闭环（A1）：弱科目应返回可一键开始专注的结构化动作
        var tasks = new List<TaskItem> { new() { Name = "数学" }, new() { Name = "英语" } };
        var sessions = new List<FocusSession>();
        // 数学：近 7 天 5 次（日均 5/7 < 1 阈值 → 弱科目）；英语：近 7 天 10 次（不触发）
        for (int i = 0; i < 5; i++)
            sessions.Add(new FocusSession { Completed = true, EndTime = DateTime.Today.AddDays(-i).AddHours(9), TaskName = "数学", FocusMinutes = 25 });
        for (int i = 0; i < 10; i++)
            sessions.Add(new FocusSession { Completed = true, EndTime = DateTime.Today.AddDays(-i).AddHours(14), TaskName = "英语", FocusMinutes = 25 });

        var insights = _engine.GetInsights(sessions, tasks);
        var actionInsight = insights.FirstOrDefault(x => x.Action != null && x.Action.Kind == SuggestedActionKind.StartFocus);
        Assert.NotNull(actionInsight);
        Assert.Equal("数学", actionInsight!.Action!.TaskName);
        Assert.Contains("数学", actionInsight.Action.ActionLabel);
    }

    [Fact]
    public void GetInsights_PeakHour_ReturnsScheduleBlockAction()
    {
        // 峰值时段排程（A2）：黄金时段洞察应返回可加入今日计划的 ScheduleBlock 动作
        var tasks = new List<TaskItem> { new() { Name = "数学" }, new() { Name = "英语" }, new() { Name = "政治" } };
        var sessions = new List<FocusSession>();
        // 数学在 9:00 形成最明显峰值（avgMinutes 最高）
        for (int i = 0; i < 5; i++)
            sessions.Add(new FocusSession { Completed = true, EndTime = DateTime.Today.AddDays(-i).AddHours(9), TaskName = "数学", FocusMinutes = 30 });
        for (int i = 0; i < 4; i++)
            sessions.Add(new FocusSession { Completed = true, EndTime = DateTime.Today.AddDays(-i).AddHours(14), TaskName = "英语", FocusMinutes = 25 });
        for (int i = 0; i < 3; i++)
            sessions.Add(new FocusSession { Completed = true, EndTime = DateTime.Today.AddDays(-i).AddHours(20), TaskName = "政治", FocusMinutes = 20 });

        var insights = _engine.GetInsights(sessions, tasks);
        var peak = insights.FirstOrDefault(x => x.Type == InsightType.PeakHour);
        Assert.NotNull(peak);
        Assert.NotNull(peak!.Action);
        Assert.Equal(SuggestedActionKind.ScheduleBlock, peak.Action!.Kind);
        Assert.Equal(9, peak.Action.PreferredHour);
        Assert.False(string.IsNullOrEmpty(peak.Action.TaskName));
        Assert.Contains("9:00", peak.Action.ActionLabel);
    }
}
