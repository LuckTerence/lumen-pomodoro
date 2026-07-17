using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Tests;

public class TimerServiceTests
{
    private readonly TimerService _timerService;

    public TimerServiceTests()
    {
        _timerService = new TimerService();
    }

    [Fact]
    public void StartFocus_ShouldSetCorrectMode()
    {
        // Arrange
        TimerMode? changedMode = null;
        _timerService.ModeChanged += (s, e) => changedMode = e.NewMode;

        // Act
        _timerService.StartFocus(25);

        // Assert
        Assert.Equal(TimerMode.Focus, _timerService.CurrentMode);
        Assert.True(_timerService.IsRunning);
        Assert.False(_timerService.IsPaused);
        Assert.Equal(25 * 60, _timerService.RemainingSeconds);
        Assert.Equal(changedMode, TimerMode.Focus);

        // Cleanup
        _timerService.Stop();
    }

    [Fact]
    public void StartBreak_ShouldSetCorrectMode()
    {
        // Arrange
        TimerMode? changedMode = null;
        _timerService.ModeChanged += (s, e) => changedMode = e.NewMode;

        // Act
        _timerService.StartBreak(5);

        // Assert
        Assert.Equal(TimerMode.Break, _timerService.CurrentMode);
        Assert.True(_timerService.IsRunning);
        Assert.False(_timerService.IsPaused);
        Assert.Equal(5 * 60, _timerService.RemainingSeconds);
        Assert.Equal(changedMode, TimerMode.Break);

        // Cleanup
        _timerService.Stop();
    }

    [Fact]
    public void Pause_ShouldStopTimer()
    {
        // Arrange
        _timerService.StartFocus(25);

        // Act
        _timerService.Pause();

        // Assert
        Assert.True(_timerService.IsPaused);

        // Cleanup
        _timerService.Stop();
    }

    [Fact]
    public void Resume_ShouldRestartTimer()
    {
        // Arrange
        _timerService.StartFocus(25);
        _timerService.Pause();

        // Act
        _timerService.Resume();

        // Assert
        Assert.False(_timerService.IsPaused);
        Assert.True(_timerService.IsRunning);

        // Cleanup
        _timerService.Stop();
    }

    [Fact]
    public void Reset_ShouldReturnToIdle()
    {
        // Arrange
        _timerService.StartFocus(25);
        TimerMode? changedMode = null;
        _timerService.ModeChanged += (s, e) => changedMode = e.NewMode;

        // Act
        _timerService.Reset();

        // Assert
        Assert.False(_timerService.IsRunning);
        Assert.False(_timerService.IsPaused);
        Assert.Equal(0, _timerService.RemainingSeconds);
        Assert.Equal(TimerMode.Idle, _timerService.CurrentMode);
        Assert.Equal(changedMode, TimerMode.Idle);
    }

    // 注：倒计时递减与完成的真实行为已迁移到纯逻辑类 TimerEngine，
    // 由 TimerEngineTests 以虚拟时钟全面覆盖（不再依赖 DispatcherTimer / UI 线程）。

    [Fact]
    public void TimerDispose_ShouldNotThrow()
    {
        // Arrange
        var service = new TimerService();
        service.StartFocus(25);
        service.Stop();

        // Act & Assert — Dispose 不应抛出异常
        service.Dispose();
    }
}
