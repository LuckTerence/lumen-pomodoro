using LumenPomodoro.Services;

namespace LumenPomodoro.Tests.Services;

public class CameraServiceTests : IDisposable
{
    private readonly CameraService _cameraService;

    public CameraServiceTests()
    {
        _cameraService = new CameraService();
    }

    public void Dispose()
    {
        _cameraService.Dispose();
    }

    [Fact]
    public void Initialize_ShouldNotThrow()
    {
        // Act & Assert — 初始化不应抛出异常（即使无摄像头设备）
        _cameraService.Initialize(0, _ => { }, _ => { });
    }

    [Fact]
    public async Task GetAvailableCameras_ShouldReturnAtLeastOneCamera()
    {
        // Arrange
        _cameraService.Initialize(0, _ => { }, _ => { });

        // Act
        var cameras = await _cameraService.GetAvailableCamerasAsync();

        // Assert — 无摄像头时返回默认摄像头占位
        Assert.NotEmpty(cameras);
    }

    [Fact]
    public async Task GetCameraCount_ShouldReturnPositiveNumber()
    {
        // Arrange
        _cameraService.Initialize(0, _ => { }, _ => { });

        // Act
        var count = await _cameraService.GetCameraCountAsync();

        // Assert — 无摄像头时返回 1 作为占位值
        Assert.True(count >= 1);
    }

    [Fact]
    public void IsRunning_ShouldBeFalseBeforeStart()
    {
        // Arrange
        _cameraService.Initialize(0, _ => { }, _ => { });

        // Assert — 未启动时 IsRunning 为 false
        Assert.False(_cameraService.IsRunning);
    }

    [Fact]
    public async Task StopCameraAsync_WhenNotRunning_ShouldNotThrow()
    {
        // Arrange
        _cameraService.Initialize(0, _ => { }, _ => { });

        // Act & Assert — 停止未运行的摄像头不应抛出异常
        await _cameraService.StopCameraAsync();
    }

    [Fact]
    public void Initialize_WithStatusCallback_ShouldNotThrow()
    {
        // Arrange
        string? receivedStatus = null;

        // Act & Assert — 带回调初始化不应抛出异常
        _cameraService.Initialize(0, status => receivedStatus = status, _ => { });
    }

    [Fact]
    public void Dispose_ShouldNotThrowWhenCalledMultipleTimes()
    {
        // Arrange
        _cameraService.Initialize(0, _ => { }, _ => { });

        // Act & Assert — 多次 Dispose 不应抛出
        _cameraService.Dispose();
    }
}
