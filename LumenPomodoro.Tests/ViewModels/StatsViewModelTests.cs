using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class StatsViewModelTests
{
    private readonly Mock<IStorageService> _storageMock;
    private readonly Mock<IInsightEngine> _insightMock;
    private readonly StatsViewModel _viewModel;

    public StatsViewModelTests()
    {
        _storageMock = new Mock<IStorageService>();
        _insightMock = new Mock<IInsightEngine>();
        SetupDefaultMocks();
        _viewModel = new StatsViewModel(_storageMock.Object, _insightMock.Object);
    }

    private void SetupDefaultMocks()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today.AddHours(10), FocusMinutes = 25, QualityScore = 4, TaskId = "task1" }
        };
        var tasks = new List<TaskItem>
        {
            new() { Id = "task1", Name = "数学", Category = "理科", Color = "#FF0000" }
        };

        _storageMock.Setup(s => s.LoadSessions()).Returns(sessions);
        _storageMock.Setup(s => s.LoadTasks()).Returns(tasks);
        _storageMock.Setup(s => s.LoadSettings()).Returns(new Settings());

        _insightMock.Setup(i => i.GetHeatmapData(It.IsAny<List<FocusSession>>())).Returns(new List<HeatmapDay>());
        _insightMock.Setup(i => i.GetHourlyDistribution(It.IsAny<List<FocusSession>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<HourlyDataPoint>());
        _insightMock.Setup(i => i.GetTaskBreakdown(It.IsAny<List<FocusSession>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<List<TaskItem>?>()))
            .Returns(new List<TaskSlice>());
        _insightMock.Setup(i => i.GetWeeklyTrend(It.IsAny<List<FocusSession>>())).Returns(new List<WeeklyDataPoint>());
        _insightMock.Setup(i => i.GetInsights(It.IsAny<List<FocusSession>>(), It.IsAny<List<TaskItem>>()))
            .Returns(new List<Insight>());
        _insightMock.Setup(i => i.GetGoalProgress(It.IsAny<List<FocusSession>>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<GoalProgress>());
        _insightMock.Setup(i => i.GetComparisons(It.IsAny<List<FocusSession>>())).Returns(new List<ComparisonData>());
        _insightMock.Setup(i => i.GetEfficiencyTrend(It.IsAny<List<FocusSession>>())).Returns(new List<EfficiencyDataPoint>());
    }

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        Assert.Equal(0, _viewModel.CompletedPomodoros);
        Assert.Equal(0, _viewModel.TotalFocusMinutes);
        Assert.Equal(0, _viewModel.AvgQualityScore);
        Assert.Equal(0, _viewModel.StreakDays);
        Assert.Equal("今日统计", _viewModel.StatsDateLabel);
    }

    [Fact]
    public void Refresh_LoadsStatsForDayPeriod()
    {
        _viewModel.Refresh();

        Assert.Equal(1, _viewModel.CompletedPomodoros);
        Assert.Equal(25, _viewModel.TotalFocusMinutes);
        Assert.False(_viewModel.CanGoNext);
    }

    [Fact]
    public void PeriodSelection_Week_SetsCorrectPeriod()
    {
        _viewModel.PeriodSelection = "Week";

        Assert.Equal("Week", _viewModel.PeriodSelection);
        Assert.NotEqual("今日统计", _viewModel.StatsDateLabel);
    }

    [Fact]
    public void PeriodSelection_Month_SetsCorrectPeriod()
    {
        _viewModel.PeriodSelection = "Month";

        Assert.Equal("Month", _viewModel.PeriodSelection);
        Assert.Contains("年", _viewModel.StatsDateLabel);
        Assert.Contains("月", _viewModel.StatsDateLabel);
    }

    [Fact]
    public void PeriodSelection_Invalid_DefaultsToDay()
    {
        _viewModel.PeriodSelection = "Invalid";

        Assert.Equal("今日统计", _viewModel.StatsDateLabel);
    }

    [Fact]
    public void ShiftDate_DayForward_FutureNotAllowed()
    {
        _viewModel.PeriodSelection = "Day";
        _viewModel.Refresh();

        _viewModel.ShiftDate(1);

        Assert.Equal("今日统计", _viewModel.StatsDateLabel);
    }

    [Fact]
    public void ShiftDate_DayBackward_Allowed()
    {
        _viewModel.PeriodSelection = "Day";
        _viewModel.Refresh();

        _viewModel.ShiftDate(-1);

        Assert.NotEqual("今日统计", _viewModel.StatsDateLabel);
    }

    [Fact]
    public void Refresh_WithInsightsDisabled_DoesNotLoadGoals()
    {
        var settings = new Settings { InsightsEnabled = false };
        _storageMock.Setup(s => s.LoadSettings()).Returns(settings);

        _viewModel.Refresh();

        Assert.Empty(_viewModel.GoalProgress);
        Assert.Empty(_viewModel.Comparisons);
        Assert.Empty(_viewModel.EfficiencyTrend);
    }

    [Fact]
    public void Refresh_WithInsightsEnabled_LoadsGoals()
    {
        _viewModel.Refresh();

        _insightMock.Verify(i => i.GetGoalProgress(It.IsAny<List<FocusSession>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetAllSessions_ReturnsFromStorage()
    {
        var result = _viewModel.GetAllSessions();

        Assert.Single(result);
        _storageMock.Verify(s => s.LoadSessions(), Times.Once);
    }
}
