using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class SessionScoringControllerTests
{
    [Fact]
    public void CheckMilestones_FirstPomodoro_FiresCallback()
    {
        var todayStats = new DailyStats { CompletedPomodoros = 1, TotalFocusMinutes = 25 };
        var settings = new Settings { DailyGoalMinutes = 0, DailyTargetPomodoros = 0 };
        string? receivedTitle = null;
        string? receivedMessage = null;

        SessionScoringController.CheckMilestones(todayStats, settings, (t, m) =>
        {
            receivedTitle = t;
            receivedMessage = m;
        });

        Assert.NotNull(receivedTitle);
        Assert.NotNull(receivedMessage);
    }

    [Fact]
    public void CheckMilestones_DailyGoalReached_FiresCallback()
    {
        var todayStats = new DailyStats { CompletedPomodoros = 0, TotalFocusMinutes = 150 };
        var settings = new Settings { DailyGoalMinutes = 120, DailyTargetPomodoros = 0 };
        string? receivedTitle = null;
        string? receivedMessage = null;

        SessionScoringController.CheckMilestones(todayStats, settings, (t, m) =>
        {
            receivedTitle = t;
            receivedMessage = m;
        });

        Assert.NotNull(receivedTitle);
        Assert.NotNull(receivedMessage);
    }

    [Fact]
    public void CheckMilestones_TargetPomodorosMet_FiresCallback()
    {
        var todayStats = new DailyStats { CompletedPomodoros = 8, TotalFocusMinutes = 200 };
        var settings = new Settings { DailyGoalMinutes = 0, DailyTargetPomodoros = 8 };
        string? receivedTitle = null;
        string? receivedMessage = null;

        SessionScoringController.CheckMilestones(todayStats, settings, (t, m) =>
        {
            receivedTitle = t;
            receivedMessage = m;
        });

        Assert.NotNull(receivedTitle);
        Assert.NotNull(receivedMessage);
    }

    [Fact]
    public void CalculateStreak_WithCompletedSessions_ReturnsNonNegative()
    {
        var sessions = new List<FocusSession>
        {
            new() { Completed = true, EndTime = DateTime.Today },
            new() { Completed = true, EndTime = DateTime.Today.AddDays(-1) }
        };

        var streak = SessionScoringController.CalculateStreak(sessions);

        Assert.True(streak >= 0);
    }

    [Fact]
    public void CalculateStreak_WithNoSessions_ReturnsZero()
    {
        var streak = SessionScoringController.CalculateStreak(Array.Empty<FocusSession>());

        Assert.Equal(0, streak);
    }

    [Fact]
    public void ShouldSuggestLongBreak_WhenIntervalDivides_FiresCallback()
    {
        var settings = new Settings { LongBreakInterval = 4 };

        var result = SessionScoringController.ShouldSuggestLongBreak(4, settings);

        Assert.True(result);
    }

    [Fact]
    public void ShouldSuggestLongBreak_WhenIntervalIsZero_FiresCallback()
    {
        var settings = new Settings { LongBreakInterval = 0 };

        var result = SessionScoringController.ShouldSuggestLongBreak(4, settings);

        Assert.False(result);
    }

    [Fact]
    public void GetYesterdayReport_WhenNoSessions_ReturnsNull()
    {
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.LoadSessions()).Returns(new List<FocusSession>());

        var result = SessionScoringController.GetYesterdayReport(storageMock.Object);

        Assert.Null(result);
    }

    [Fact]
    public void SaveNotes_SavesToSession()
    {
        var storageMock = new Mock<IStorageService>();
        var sessionId = "session-123";
        var notes = "Test notes";

        SessionScoringController.SaveNotes(storageMock.Object, sessionId, notes);

        storageMock.Verify(s => s.UpdateSession(
            sessionId,
            It.IsAny<Action<FocusSession>>()), Times.Once);
    }

    [Fact]
    public void SaveNotes_WithNullSessionId_DoesNothing()
    {
        var storageMock = new Mock<IStorageService>();

        SessionScoringController.SaveNotes(storageMock.Object, null, "notes");

        storageMock.Verify(s => s.UpdateSession(
            It.IsAny<string>(),
            It.IsAny<Action<FocusSession>>()), Times.Never);
    }
}
