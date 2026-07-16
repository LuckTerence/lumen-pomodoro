using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class CameraAlertControllerTests
{
    private readonly Mock<ICameraService> _cameraServiceMock;
    private readonly CameraAlertController _controller;
    private bool _cameraStarted;
    private bool _cameraStopped;

    public CameraAlertControllerTests()
    {
        _cameraServiceMock = new Mock<ICameraService>();
        _cameraServiceMock
            .Setup(s => s.StartCameraAsync())
            .Callback(() => _cameraStarted = true)
            .Returns(Task.CompletedTask);
        _cameraServiceMock
            .Setup(s => s.StartCameraForDurationAsync(It.IsAny<int>()))
            .Callback(() => _cameraStarted = true)
            .Returns(Task.CompletedTask);
        _cameraServiceMock
            .Setup(s => s.StopCameraAsync())
            .Callback(() => _cameraStopped = true)
            .Returns(Task.CompletedTask);

        _controller = new CameraAlertController(_cameraServiceMock.Object);
    }

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        Assert.False(_controller.IsActive);
        Assert.Equal(string.Empty, _controller.Status);
    }

    [Fact]
    public void Initialize_ConfiguresCameraService()
    {
        var settings = new Settings { CameraIndex = 1 };

        _controller.Initialize(settings);

        _cameraServiceMock.Verify(
            s => s.Initialize(
                It.Is<int>(v => v == 1),
                It.IsAny<Action<string>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<Action?>(),
                It.IsAny<Action?>()),
            Times.Once);
    }

    [Fact]
    public void Start_CameraAlertDisabled_ReturnsEarly()
    {
        var settings = new Settings { CameraAlertEnabled = false };

        _controller.Start(settings);

        Assert.False(_cameraStarted);
    }

    [Fact]
    public async Task Start_UntilConfirmMode_StartsCamera()
    {
        var settings = new Settings
        {
            CameraAlertEnabled = true,
            CameraAlertMode = CameraAlertMode.UntilConfirm,
            HasShownCameraPrivacyNotice = true
        };

        _controller.Start(settings);

        // Fire-and-forget runs on thread pool; give it time to complete
        await Task.Delay(100);

        Assert.True(_cameraStarted);
    }

    [Fact]
    public async Task Start_FixedDurationMode_StartsCameraForDuration()
    {
        var settings = new Settings
        {
            CameraAlertEnabled = true,
            CameraAlertMode = CameraAlertMode.FixedDuration,
            CameraFixedOnSeconds = 60,
            HasShownCameraPrivacyNotice = true
        };

        _controller.Start(settings);

        await Task.Delay(100);

        Assert.True(_cameraStarted);
    }

    [Fact]
    public void Start_FollowBreakMode_DoesNotStartCamera()
    {
        var settings = new Settings
        {
            CameraAlertEnabled = true,
            CameraAlertMode = CameraAlertMode.FollowBreak,
            HasShownCameraPrivacyNotice = true
        };

        _controller.Start(settings);

        Assert.False(_cameraStarted);
    }

    [Fact]
    public async Task StartForBreak_FollowBreakEnabled_StartsCamera()
    {
        var settings = new Settings
        {
            CameraAlertMode = CameraAlertMode.FollowBreak,
            CameraAlertEnabled = true,
            CameraFollowBreakEnabled = true,
            HasShownCameraPrivacyNotice = true
        };

        _controller.StartForBreak(settings);

        await Task.Delay(100);

        Assert.True(_cameraStarted);
    }

    [Fact]
    public void StartForBreak_FollowBreakDisabled_DoesNotStart()
    {
        var settings = new Settings
        {
            CameraAlertMode = CameraAlertMode.FollowBreak,
            CameraAlertEnabled = true,
            CameraFollowBreakEnabled = false,
            HasShownCameraPrivacyNotice = true
        };

        _controller.StartForBreak(settings);

        Assert.False(_cameraStarted);
    }

    [Fact]
    public async Task ForceStop_StopsCamera()
    {
        _controller.ForceStop();

        await Task.Delay(100);

        Assert.True(_cameraStopped);
    }

    [Fact]
    public async Task TryStop_ManualCloseAllowed_ReturnsTrue()
    {
        var settings = new Settings { CameraAlertCanManualClose = true };

        var result = _controller.TryStop(settings);

        Assert.True(result);
        await Task.Delay(100);
        Assert.True(_cameraStopped);
    }

    [Fact]
    public async Task StopCameraAsync_StopsCameraService()
    {
        await _controller.StopCameraAsync();

        _cameraServiceMock.Verify(s => s.StopCameraAsync(), Times.Once);
    }
}
