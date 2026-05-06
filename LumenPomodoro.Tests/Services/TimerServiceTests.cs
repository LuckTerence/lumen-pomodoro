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

    [Fact]
    public void TimerTick_ShouldDecreaseRemainingSeconds()
    {
        // Arrange
        int tickCount = 0;
        int lastRemaining = -1;
        _timerService.TimerTick += (s, e) =>
        {
            if (lastRemaining != -1 && tickCount < 3)
            {
                Assert.Equal(lastRemaining - 1, e.RemainingSeconds);
            }
            lastRemaining = e.RemainingSeconds;
            tickCount++;
        };

        // Act
        _timerService.StartFocus(1); // 1 minute for quick test
        Thread.Sleep(2100); // Wait for 2 ticks
        _timerService.Stop();

        // Assert
        Assert.True(tickCount >= 2);
    }

    [Fact]
    public void TimerComplete_ShouldFireTimerCompletedEvent()
    {
        // Arrange
        bool completed = false;
        TimerMode? completedMode = null;
        _timerService.TimerCompleted += (s, e) =>
        {
            completed = true;
            completedMode = e.CompletedMode;
        };

        // Act
        _timerService.StartFocus(1); // Use short duration for test
        Thread.Sleep(61000); // Wait for completion
        _timerService.Stop();

        // Assert
        Assert.True(completed);
        Assert.Equal(TimerMode.Focus, completedMode);
    }

    [Fact]
    public void GetAvailableCameras_ShouldReturnAtLeastOneCamera()
    {
        // Arrange
        var cameraService = new CameraService();
        cameraService.Initialize(0, _ => { }, _ => { });

        // Act
        var cameras = cameraService.GetAvailableCameras();

        // Assert
        Assert.NotEmpty(cameras);
        Assert.True(cameras.Count > 0);
    }

    [Fact]
    public void GetCameraCount_ShouldReturnPositiveNumber()
    {
        // Arrange
        var cameraService = new CameraService();
        cameraService.Initialize(0, _ => { }, _ => { });

        // Act
        var count = cameraService.GetCameraCount();

        // Assert
        Assert.True(count > 0);
    }
}
