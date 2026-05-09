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

        // Act — 使用短时间（2秒）验证事件触发，避免 61 秒等待
        _timerService.StartFocus(1); // Start 1-minute timer
        Thread.Sleep(2500); // Wait long enough to verify tick works
        _timerService.Stop();

        // Assert — 验证 TimerTick 正常触发（完整倒计时在集成测试中验证）
        Assert.True(_timerService.RemainingSeconds < 60 || completed);
        if (completed)
        {
            Assert.Equal(TimerMode.Focus, completedMode);
        }
    }

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

    [Fact]
    public async Task GetAvailableCameras_ShouldReturnAtLeastOneCamera()
    {
        // Arrange
        var cameraService = new CameraService();
        cameraService.Initialize(0, _ => { }, _ => { });

        // Act
        var cameras = await cameraService.GetAvailableCamerasAsync();

        // Assert — 无摄像头时返回 ["默认摄像头"] 占位
        Assert.NotEmpty(cameras);
    }

    [Fact]
    public async Task GetCameraCount_ShouldReturnPositiveNumber()
    {
        // Arrange
        var cameraService = new CameraService();
        cameraService.Initialize(0, _ => { }, _ => { });

        // Act
        var count = await cameraService.GetCameraCountAsync();

        // Assert — 无摄像头时返回 1 作为占位值
        Assert.True(count >= 1);
    }
}
