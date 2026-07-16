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
        var tasks = new List<TaskItem> { new() { Id = "1", Name = "Test" } };
        _storageService.Setup(s => s.LoadTasks()).Returns(tasks);
        // 重新创建 viewModel 以加载任务
        var vm = new MainViewModel(
            _storageService.Object,
            _timerService.Object,
            _cameraService.Object,
            _soundService.Object);

        vm.StartFocus();
        _timerService.Verify(t => t.StartFocus(It.IsAny<int>()), Times.Once);
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
    public void EndBreak_CallsTimerServiceReset()
    {
        _viewModel.EndBreak();
        _timerService.Verify(t => t.Reset(), Times.Once);
    }

    [Fact]
    public void SkipBreak_CallsTimerServiceReset()
    {
        _viewModel.SkipBreak();
        _timerService.Verify(t => t.Reset(), Times.Once);
    }

    [Fact]
    public void StopCameraAlert_CallsCameraServiceStopAsync()
    {
        _viewModel.StopCameraAlert();
        _cameraService.Verify(c => c.StopCameraAsync(), Times.Once);
    }

    [Fact]
    public void RefreshStats_ReloadsFromStorage()
    {
        _viewModel.RefreshStats();
        _storageService.Verify(s => s.GetTodayStats(), Times.AtLeastOnce);
    }

    [Fact]
    public void UpdateSettings_UpdatesAppSettings()
    {
        var settings = new Settings { WorkMinutes = 30 };
        _viewModel.UpdateSettings(settings);
        Assert.Equal(30, _viewModel.AppSettings.WorkMinutes);
        Assert.Equal(settings, _viewModel.AppSettings);
    }

    [Fact]
    public void ReloadSettings_LoadsFromStorage()
    {
        _viewModel.ReloadSettings();
        _storageService.Verify(s => s.LoadSettings(), Times.AtLeast(2));
    }

    [Fact]
    public void UpdateTasks_UpdatesTasksProperty()
    {
        var tasks = new List<TaskItem> { new() { Id = "1", Name = "Test" } };
        _viewModel.UpdateTasks(tasks);
        Assert.Equal(tasks, _viewModel.Tasks);
        Assert.Equal(tasks[0], _viewModel.SelectedTask);
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
        // Try to set below minimum
        _viewModel.AdjustWorkMinutes(-100);
        Assert.True(_viewModel.AppSettings.WorkMinutes >= 1);

        // Try to set above maximum
        _viewModel.AdjustWorkMinutes(200);
        Assert.True(_viewModel.AppSettings.WorkMinutes <= 120);
    }

    [Fact]
    public void Dispose_StopsCamera()
    {
        _viewModel.Dispose();
        _cameraService.Verify(c => c.StopCameraAsync(), Times.Once);
    }

    [Fact]
    public void ApplyPreset_Standard_SetsWorkAndBreakMinutes()
    {
        _viewModel.ApplyPreset(PomodoroPreset.Standard);

        Assert.Equal(25, _viewModel.AppSettings.WorkMinutes);
        Assert.Equal(5, _viewModel.AppSettings.ShortBreakMinutes);
        Assert.Equal(15, _viewModel.AppSettings.LongBreakMinutes);
        Assert.Equal(4, _viewModel.AppSettings.LongBreakInterval);
    }

    [Fact]
    public void ApplyPreset_Custom_DoesNotOverrideValues()
    {
        var original = _viewModel.AppSettings.WorkMinutes;
        _viewModel.ApplyPreset(PomodoroPreset.Custom);
        Assert.Equal(original, _viewModel.AppSettings.WorkMinutes);
    }
}
