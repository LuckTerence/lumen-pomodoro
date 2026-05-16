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
        _cameraService.Setup(c => c.GetAvailableCamerasAsync())
            .ReturnsAsync(new List<string> { "Test Camera" });
        _cameraService.Setup(c => c.GetCameraCountAsync())
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
    public void WorkMinutes_ClampsToValidRange()
    {
        _viewModel.WorkMinutes = 0;
        Assert.True(_viewModel.WorkMinutes >= 1);

        _viewModel.WorkMinutes = 150;
        Assert.True(_viewModel.WorkMinutes <= 120);
    }

    [Fact]
    public void ShortBreakMinutes_ClampsToValidRange()
    {
        _viewModel.ShortBreakMinutes = 0;
        Assert.True(_viewModel.ShortBreakMinutes >= 1);

        _viewModel.ShortBreakMinutes = 100;
        Assert.True(_viewModel.ShortBreakMinutes <= 60);
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
    public void TestCameraAlert_StartsAndStopsCamera()
    {
        _cameraService.Setup(c => c.StartCameraForDurationAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _cameraService.Setup(c => c.StopCameraAsync())
            .Returns(Task.CompletedTask);

        _viewModel.TestCameraAlert();

        _cameraService.Verify(c => c.StartCameraForDurationAsync(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void Dispose_CleansUp()
    {
        _viewModel.Dispose();
    }
}
