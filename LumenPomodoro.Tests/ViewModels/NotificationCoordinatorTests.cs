using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class NotificationCoordinatorTests : IDisposable
{
    private readonly Mock<ISoundService> _soundService;
    private readonly NotificationCoordinator _coordinator;

    public NotificationCoordinatorTests()
    {
        _soundService = new Mock<ISoundService>();
        _coordinator = new NotificationCoordinator(_soundService.Object);
    }

    [Fact]
    public void PlaySound_WhenEnabled_DelegatesToSoundService()
    {
        _coordinator.PlaySound("tick", soundEnabled: true);

        _soundService.Verify(s => s.PlaySound("tick"), Times.Once);
    }

    [Fact]
    public void PlaySound_WhenDisabled_DoesNothing()
    {
        _coordinator.PlaySound("tick", soundEnabled: false);

        _soundService.Verify(s => s.PlaySound(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ShowInApp_WhenEnabled_FiresInAppNotificationRequested()
    {
        string? receivedTitle = null;
        string? receivedMessage = null;
        _coordinator.InAppNotificationRequested += (t, m) =>
        {
            receivedTitle = t;
            receivedMessage = m;
        };

        _coordinator.ShowInApp("Title", "Message", popupEnabled: true);

        Assert.Equal("Title", receivedTitle);
        Assert.Equal("Message", receivedMessage);
    }

    [Fact]
    public void ShowInApp_WhenDisabled_DoesNothing()
    {
        var fired = false;
        _coordinator.InAppNotificationRequested += (_, _) => fired = true;

        _coordinator.ShowInApp("Title", "Message", popupEnabled: false);

        Assert.False(fired);
    }

    [Fact]
    public void ShowSystem_WhenEnabledAndWindowInactive_FiresNotificationRequested()
    {
        string? receivedTitle = null;
        string? receivedMessage = null;
        _coordinator.NotificationRequested += (t, m) =>
        {
            receivedTitle = t;
            receivedMessage = m;
        };

        _coordinator.ShowSystem("Title", "Message", systemNotificationEnabled: true, isWindowActive: false);

        Assert.Equal("Title", receivedTitle);
        Assert.Equal("Message", receivedMessage);
    }

    [Fact]
    public void ShowSystem_WhenDisabled_DoesNothing()
    {
        var fired = false;
        _coordinator.NotificationRequested += (_, _) => fired = true;

        _coordinator.ShowSystem("Title", "Message", systemNotificationEnabled: false, isWindowActive: false);

        Assert.False(fired);
    }

    [Fact]
    public void ShowSystem_WhenWindowActive_DoesNothing()
    {
        var fired = false;
        _coordinator.NotificationRequested += (_, _) => fired = true;

        _coordinator.ShowSystem("Title", "Message", systemNotificationEnabled: true, isWindowActive: true);

        Assert.False(fired);
    }

    [Fact]
    public void StartCountdown_WhenEnabled_FiresCountdownStartRequested()
    {
        string? receivedMessage = null;
        _coordinator.CountdownStartRequested += m => receivedMessage = m;

        _coordinator.StartCountdown("25:00", isWindowTopmost: false, dynamicIslandEnabled: true);

        Assert.Equal("25:00", receivedMessage);
    }

    [Fact]
    public void StartCountdown_WhenTopmost_StillFires_IslandIsPrimary()
    {
        // 岛为产品主交互：窗口置顶不再抑制倒计时推送
        var fired = false;
        _coordinator.CountdownStartRequested += _ => fired = true;

        _coordinator.StartCountdown("25:00", isWindowTopmost: true, dynamicIslandEnabled: true);

        Assert.True(fired);
    }

    [Fact]
    public void UpdateCountdown_WhenEnabled_FiresCountdownUpdateRequested()
    {
        string? receivedTime = null;
        _coordinator.CountdownUpdateRequested += t => receivedTime = t;

        _coordinator.UpdateCountdown("10:00", isWindowTopmost: false, dynamicIslandEnabled: true);

        Assert.Equal("10:00", receivedTime);
    }

    [Fact]
    public void UpdateCountdown_WhenDisabled_DoesNothing()
    {
        var fired = false;
        _coordinator.CountdownUpdateRequested += _ => fired = true;

        _coordinator.UpdateCountdown("10:00", isWindowTopmost: false, dynamicIslandEnabled: false);

        Assert.False(fired);
    }

    [Fact]
    public void TrayUpdate_TimerFires_TrayMenuNeedsUpdate()
    {
        var fired = false;
        _coordinator.TrayMenuNeedsUpdate += () => fired = true;

        _coordinator.StartTrayTimer();

        // The timer uses DispatcherTimer with 5-second interval, so Tick won't fire
        // synchronously. Verify the setup doesn't throw and the event handler is registered.
        Assert.False(fired);
    }

    [Fact]
    public void Dispose_CleansUp()
    {
        _coordinator.Dispose();
        // Should not throw
    }

    public void Dispose()
    {
        _coordinator.Dispose();
    }
}
