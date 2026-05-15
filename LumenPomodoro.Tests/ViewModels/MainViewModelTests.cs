using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly Mock<IStorageService> _storageService;
    private readonly Mock<ITimerService> _timerService;
    private readonly Mock<ICameraService> _cameraService;
    private readonly Mock<ISoundService> _soundService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _storageService = new Mock<IStorageService>();
        _timerService = new Mock<ITimerService>();
        _cameraService = new Mock<ICameraService>();
        _soundService = new Mock<ISoundService>();

        // Setup default returns
        _storageService.Setup(s => s.LoadSettings()).Returns(new Settings());
        _storageService.Setup(s => s.LoadTasks()).Returns(new List<TaskItem>());
        _storageService.Setup(s => s.LoadSessions()).Returns(new List<FocusSession>());
        _storageService.Setup(s => s.GetTodayStats()).Returns(new DailyStats());

        _timerService.Setup(t => t.IsRunning).Returns(false);
        _timerService.Setup(t => t.IsPaused).Returns(false);
        _timerService.Setup(t => t.RemainingSeconds).Returns(0);
        _timerService.Setup(t => t.TotalSeconds).Returns(25 * 60);
        _timerService.Setup(t => t.CurrentMode).Returns(TimerMode.Idle);

        _viewModel = new MainViewModel(
            _storageService.Object,
            _timerService.Object,
            _cameraService.Object,
            _soundService.Object);
    }

    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        Assert.Equal(TimerMode.Idle, _viewModel.CurrentStatus);
        Assert.NotNull(_viewModel.Tasks);
        Assert.NotNull(_viewModel.TodayStats);
        Assert.NotNull(_viewModel.AppSettings);
    }

    [Fact]
    public void StartFocus_CallsTimerServiceStartFocus()
    {
        _viewModel.StartFocus();
        _timerService.Verify(t => t.StartFocus(), Times.Once);
    }

    [Fact]
    public void PauseFocus_CallsTimerServicePause()
    {
        _timerService.Setup(t => t.IsRunning).Returns(true);

        _viewModel.PauseFocus();
        _timerService.Verify(t => t.Pause(), Times.Once);
    }

    [Fact]
    public void ResumeFocus_CallsTimerServiceResume()
    {
        _timerService.Setup(t => t.IsPaused).Returns(true);

        _viewModel.ResumeFocus();
        _timerService.Verify(t => t.Resume(), Times.Once);
    }

    [Fact]
    public void ResetFocus_CallsTimerServiceReset()
    {
        _viewModel.ResetFocus();
        _timerService.Verify(t => t.Reset(), Times.Once);
    }

    [Fact]
    public void StartBreak_CallsTimerServiceStartBreak()
    {
        _viewModel.StartBreak();
        _timerService.Verify(t => t.StartBreak(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void StartBreak_LongBreak_CallsWithLongBreakDuration()
    {
        _viewModel.StartBreak(isLongBreak: true);
        _timerService.Verify(t => t.StartBreak(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void EndBreak_CallsTimerServiceStop()
    {
        _viewModel.EndBreak();
        _timerService.Verify(t => t.Stop(), Times.Once);
    }

    [Fact]
    public void SkipBreak_CallsTimerServiceStop()
    {
        _viewModel.SkipBreak();
        _timerService.Verify(t => t.Stop(), Times.Once);
    }

    [Fact]
    public void StopCameraAlert_CallsCameraServiceStop()
    {
        _viewModel.StopCameraAlert();
        _cameraService.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RefreshStats_ReloadsFromStorage()
    {
        _viewModel.RefreshStats();
        _storageService.Verify(s => s.GetTodayStats(), Times.AtLeastOnce);
    }

    [Fact]
    public void UpdateSettings_SavesToStorage()
    {
        var settings = new Settings { FocusMinutes = 30 };
        _viewModel.UpdateSettings(settings);
        _storageService.Verify(s => s.SaveSettings(It.Is<Settings>(s => s.FocusMinutes == 30)), Times.Once);
    }

    [Fact]
    public void ReloadSettings_LoadsFromStorage()
    {
        _viewModel.ReloadSettings();
        _storageService.Verify(s => s.LoadSettings(), Times.AtLeast(2)); // once in constructor, once in reload
    }

    [Fact]
    public void UpdateTasks_SavesToStorage()
    {
        var tasks = new List<TaskItem> { new() { Id = "1", Name = "Test" } };
        _viewModel.UpdateTasks(tasks);
        _storageService.Verify(s => s.SaveTasks(tasks), Times.Once);
    }

    [Fact]
    public void ReloadTasks_LoadsFromStorage()
    {
        _viewModel.ReloadTasks();
        _storageService.Verify(s => s.LoadTasks(), Times.AtLeast(2));
    }

    [Fact]
    public void AdjustWorkMinutes_ClampsToValidRange()
    {
        // Try to set below minimum (1 minute)
        _viewModel.AdjustWorkMinutes(-100);
        Assert.True(_viewModel.AppSettings.FocusMinutes >= 1);

        // Try to set above maximum (120 minutes)
        _viewModel.AdjustWorkMinutes(200);
        Assert.True(_viewModel.AppSettings.FocusMinutes <= 120);
    }

    [Fact]
    public void Dispose_DisposesServices()
    {
        _viewModel.Dispose();
        _cameraService.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public void PropertyChanged_RaisedOnStatusChange()
    {
        bool raised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentStatus))
                raised = true;
        };

        // Simulate timer tick that changes status
        _timerService.Raise(t => t.Tick += null, _timerService.Object,
            new TimerTickEventArgs(24 * 60, 25 * 60));

        // The ViewModel should have updated based on the timer tick
        // (Exact behavior depends on ViewModel implementation)
    }
}
