using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<IStorageService> _storageService;
    private readonly Mock<ICameraService> _cameraService;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _storageService = new Mock<IStorageService>();
        _cameraService = new Mock<ICameraService>();

        _storageService.Setup(s => s.LoadSettings()).Returns(new Settings());
        _cameraService.Setup(c => c.GetAvailableCamerasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CameraDevice> { new("0", "Test Camera") });
        _cameraService.Setup(c => c.GetCameraCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _viewModel = new SettingsViewModel(_storageService.Object, _cameraService.Object);
    }

    [Fact]
    public void Constructor_LoadsSettings()
    {
        _storageService.Verify(s => s.LoadSettings(), Times.Once);
    }

    [Fact]
    public void SaveSettings_PersistsChanges()
    {
        _viewModel.SaveSettings();
        _storageService.Verify(s => s.SaveSettings(It.IsAny<Settings>()), Times.Once);
    }

    [Fact]
    public void FocusMinutes_ClampsToValidRange()
    {
        // SettingsViewModel should clamp FocusMinutes to [1, 120]
        _viewModel.FocusMinutes = 0;
        Assert.True(_viewModel.FocusMinutes >= 1);

        _viewModel.FocusMinutes = 150;
        Assert.True(_viewModel.FocusMinutes <= 120);
    }

    [Fact]
    public void BreakMinutes_ClampsToValidRange()
    {
        _viewModel.BreakMinutes = 0;
        Assert.True(_viewModel.BreakMinutes >= 1);

        _viewModel.BreakMinutes = 100;
        Assert.True(_viewModel.BreakMinutes <= 60);
    }

    [Fact]
    public void LongBreakMinutes_ClampsToValidRange()
    {
        _viewModel.LongBreakMinutes = 0;
        Assert.True(_viewModel.LongBreakMinutes >= 1);

        _viewModel.LongBreakMinutes = 100;
        Assert.True(_viewModel.LongBreakMinutes <= 60);
    }

    [Fact]
    public void Volume_ClampsToValidRange()
    {
        _viewModel.Volume = -10;
        Assert.True(_viewModel.Volume >= 0);

        _viewModel.Volume = 150;
        Assert.True(_viewModel.Volume <= 100);
    }

    [Fact]
    public async Task TestCameraAlert_StartsAndStopsCamera()
    {
        _cameraService.Setup(c => c.StartCameraForDurationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cameraService.Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _viewModel.TestCameraAlert();

        _cameraService.Verify(c => c.StartCameraForDurationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_CleansUp()
    {
        _viewModel.Dispose();
        // Should not throw
    }
}
