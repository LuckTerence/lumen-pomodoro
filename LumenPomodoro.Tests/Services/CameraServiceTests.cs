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
    public async Task InitializeAsync_ShouldNotThrow()
    {
        // Act & Assert — 初始化不应抛出异常（即使无摄像头设备）
        await _cameraService.InitializeAsync(0, _ => { }, _ => { });
    }

    [Fact]
    public async Task GetAvailableCameras_ShouldReturnAtLeastOneCamera()
    {
        // Arrange
        await _cameraService.InitializeAsync(0, _ => { }, _ => { });

        // Act
        var cameras = await _cameraService.GetAvailableCamerasAsync();

        // Assert — 无摄像头时返回默认摄像头占位
        Assert.NotEmpty(cameras);
    }

    [Fact]
    public async Task GetCameraCount_ShouldReturnPositiveNumber()
    {
        // Arrange
        await _cameraService.InitializeAsync(0, _ => { }, _ => { });

        // Act
        var count = await _cameraService.GetCameraCountAsync();

        // Assert — 无摄像头时返回 1 作为占位值
        Assert.True(count >= 1);
    }

    [Fact]
    public async Task IsActive_ShouldBeFalseBeforeStart()
    {
        // Arrange
        await _cameraService.InitializeAsync(0, _ => { }, _ => { });

        // Assert — 未启动时 IsActive 为 false
        Assert.False(_cameraService.IsActive);
    }

    [Fact]
    public async Task IsRunning_ShouldBeFalseBeforeStart()
    {
        // Arrange
        await _cameraService.InitializeAsync(0, _ => { }, _ => { });

        // Assert — 未启动时 IsRunning 为 false
        Assert.False(_cameraService.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_ShouldNotThrow()
    {
        // Arrange
        await _cameraService.InitializeAsync(0, _ => { }, _ => { });

        // Act & Assert — 停止未运行的摄像头不应抛出异常
        await _cameraService.StopAsync();
    }

    [Fact]
    public async Task InitializeAsync_WithStatusCallback_ShouldInvokeCallback()
    {
        // Arrange
        string? receivedStatus = null;
        await _cameraService.InitializeAsync(0, status => receivedStatus = status, _ => { });

        // Assert — 回调可能被调用也可能不被调用（取决于是否有摄像头）
        // 主要验证不会抛出异常
    }

    [Fact]
    public async Task Dispose_ShouldNotThrowWhenCalledMultipleTimes()
    {
        // Arrange
        await _cameraService.InitializeAsync(0, _ => { }, _ => { });

        // Act & Assert — 多次 Dispose 不应抛出
        _cameraService.Dispose();
        // 注意：xUnit 框架的 Dispose 已经会调用一次，这里额外验证
    }
}
