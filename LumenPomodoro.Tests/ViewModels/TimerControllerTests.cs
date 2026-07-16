using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class TimerControllerTests : IDisposable
{
    private readonly Mock<ITimerService> _timerService;
    private readonly TimerController _controller;

    public TimerControllerTests()
    {
        _timerService = new Mock<ITimerService>();
        _controller = new TimerController(_timerService.Object);
    }

    [Fact]
    public void StartFocus_CreatesSessionAndStartsTimer()
    {
        var task = new TaskItem { Id = "1", Name = "Test" };
        var settings = new Settings { WorkMinutes = 30 };

        var session = _controller.StartFocus(task, settings);

        Assert.Equal("Test", session.TaskName);
        Assert.Equal(30, session.FocusMinutes);
        _timerService.Verify(t => t.StartFocus(30), Times.Once);
    }

    [Fact]
    public void AdjustWorkMinutes_AppliesDeltaAndClamps()
    {
        var settings = new Settings { WorkMinutes = 25 };
        _controller.AdjustWorkMinutes(5, settings);
        Assert.Equal(30, settings.WorkMinutes);
        _controller.AdjustWorkMinutes(-100, settings);
        Assert.Equal(1, settings.WorkMinutes);
        _controller.AdjustWorkMinutes(200, settings);
        Assert.Equal(120, settings.WorkMinutes);
    }

    [Fact]
    public void TickUpdated_TriggeredByTimerTick()
    {
        int? r = null, t = null;
        _controller.TickUpdated += (a, b) => { r = a; t = b; };
        _timerService.Raise(x => x.TimerTick += null, new TimerTickEventArgs(100, 200, TimerMode.Focus));
        Assert.Equal(100, r);
        Assert.Equal(200, t);
    }

    [Fact]
    public void FocusCompleted_TriggeredWithSession()
    {
        var task = new TaskItem { Id = "1", Name = "Task" };
        _controller.StartFocus(task, new Settings { WorkMinutes = 25 });
        FocusSession? s = null;
        _controller.FocusCompleted += x => s = x;
        _timerService.Raise(x => x.TimerCompleted += null, new TimerCompletedEventArgs(TimerMode.Focus));
        Assert.NotNull(s);
        Assert.Equal("Task", s!.TaskName);
    }

    [Fact]
    public void BreakCompleted_TriggeredByTimer()
    {
        bool fired = false;
        _controller.BreakCompleted += () => fired = true;
        _timerService.Raise(x => x.TimerCompleted += null, new TimerCompletedEventArgs(TimerMode.Break));
        Assert.True(fired);
    }

    [Fact]
    public void ModeChanged_ForwardsToSubscriber()
    {
        TimerMode? m = null;
        _controller.ModeChanged += x => m = x;
        _timerService.Raise(x => x.ModeChanged += null, new TimerModeChangedEventArgs(TimerMode.Idle, TimerMode.Focus));
        Assert.Equal(TimerMode.Focus, m);
    }

    [Fact]
    public void AbandonIncompleteSession_HandlesNullCurrentSession()
    {
        var result = _controller.AbandonIncompleteSession();
        Assert.Null(result);
    }

    public void Dispose() => _controller.Dispose();
}
