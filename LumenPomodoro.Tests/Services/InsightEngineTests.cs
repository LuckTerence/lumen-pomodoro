using LumenPomodoro.Models;
using LumenPomodoro.Services;

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
}

public class ExportServiceTests
{
    private readonly ExportService _exportService = new();

    [Fact]
    public void ExportToCsv_ProducesCorrectFormat()
    {
        var sessions = new List<FocusSession>
        {
            new() { Id = "1", TaskId = "t1", TaskName = "数学",
                    StartTime = new DateTime(2025, 1, 1, 9, 0, 0),
                    EndTime = new DateTime(2025, 1, 1, 9, 25, 0),
                    FocusMinutes = 25, Completed = true }
        };
        var csv = _exportService.ExportToCsv(sessions);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("TaskName", lines[0]);
        Assert.Contains("数学", lines[1]);
    }

    [Fact]
    public void ExportToCsv_WithEmptyList_ReturnsHeaderOnly()
    {
        var csv = _exportService.ExportToCsv([]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void ExportToJson_ProducesValidJson()
    {
        var sessions = new List<FocusSession>
        {
            new() { Id = "1", TaskName = "数学", Completed = true, FocusMinutes = 25 }
        };
        var json = _exportService.ExportToJson(sessions);
        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FocusSession>>(json);
        Assert.NotNull(parsed);
        Assert.Single(parsed);
    }
}
