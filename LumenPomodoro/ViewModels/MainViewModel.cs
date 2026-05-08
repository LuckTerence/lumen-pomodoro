using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TimerService _timerService;
    private readonly CameraService _cameraService;
    private readonly StorageService _storageService;
    private readonly SoundService _soundService;

    private TimerMode _currentStatus = TimerMode.Idle;
    private string _remainingTime = "25:00";
    private int _progress = 100;
    private TaskItem? _selectedTask;
    private List<TaskItem> _tasks = new();
    private DailyStats _todayStats = new();
    private string _cameraStatus = string.Empty;
    private bool _isCameraAlertActive;
    private bool _isFocusCompleted;
    private bool _isBreakCompleted;
    private bool _isPendingBreak;
    private bool _isWindowActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsWindowActive
    {
        get => _isWindowActive;
        set { if (_isWindowActive != value) { _isWindowActive = value; OnPropertyChanged(); } }
    }

    public TimerMode CurrentStatus
    {
        get => _currentStatus;
        set { if (_currentStatus != value) { _currentStatus = value; OnPropertyChanged(); } }
    }

    public string RemainingTime
    {
        get => _remainingTime;
        set { if (_remainingTime != value) { _remainingTime = value; OnPropertyChanged(); } }
    }

    public int Progress
    {
        get => _progress;
        set { if (_progress != value) { _progress = value; OnPropertyChanged(); } }
    }

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set { if (!ReferenceEquals(_selectedTask, value)) { _selectedTask = value; OnPropertyChanged(); } }
    }

    public List<TaskItem> Tasks
    {
        get => _tasks;
        set { if (!ReferenceEquals(_tasks, value)) { _tasks = value; OnPropertyChanged(); } }
    }

    public DailyStats TodayStats
    {
        get => _todayStats;
        set { if (!ReferenceEquals(_todayStats, value)) { _todayStats = value; OnPropertyChanged(); } }
    }

    public string CameraStatus
    {
        get => _cameraStatus;
        set { if (_cameraStatus != value) { _cameraStatus = value; OnPropertyChanged(); } }
    }

    public bool IsCameraAlertActive
    {
        get => _isCameraAlertActive;
        set { if (_isCameraAlertActive != value) { _isCameraAlertActive = value; OnPropertyChanged(); } }
    }

    public bool IsFocusCompleted
    {
        get => _isFocusCompleted;
        set { if (_isFocusCompleted != value) { _isFocusCompleted = value; OnPropertyChanged(); } }
    }

    public bool IsBreakCompleted
    {
        get => _isBreakCompleted;
        set { if (_isBreakCompleted != value) { _isBreakCompleted = value; OnPropertyChanged(); } }
    }

    public bool IsPendingBreak
    {
        get => _isPendingBreak;
        set { if (_isPendingBreak != value) { _isPendingBreak = value; OnPropertyChanged(); } }
    }

    public Settings AppSettings { get; private set; } = new();

    public CameraService CameraService => _cameraService;
    public StorageService StorageService => _storageService;

    private FocusSession? _currentSession;
    private bool _disposed;
    private System.Timers.Timer? _trayUpdateTimer;

    public event Action? TrayMenuNeedsUpdate;
    public event Action<string, string>? NotificationRequested;

    public MainViewModel(StorageService storageService)
    {
        _storageService = storageService;
        _timerService = new TimerService();
        _cameraService = new CameraService();
        _soundService = new SoundService();

        _timerService.TimerTick += TimerService_TimerTick;
        _timerService.TimerCompleted += TimerService_TimerCompleted;
        _timerService.ModeChanged += TimerService_ModeChanged;

        _cameraService.Initialize(0, CameraStatusCallback, CameraErrorCallback);

        LoadData();

        if (AppSettings.TrayEnabled)
        {
            _trayUpdateTimer = new System.Timers.Timer(2000);
            _trayUpdateTimer.Elapsed += (s, e) => TrayMenuNeedsUpdate?.Invoke();
            _trayUpdateTimer.Start();
        }

        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        CurrentStatus = TimerMode.Idle;
        Progress = 100;
    }

    private void LoadData()
    {
        AppSettings = _storageService.LoadSettings();
        Tasks = _storageService.LoadTasks();
        TodayStats = _storageService.GetTodayStats();

        if (Tasks.Any())
        {
            SelectedTask = Tasks.First();
        }
    }

    private void TimerService_TimerTick(object? sender, TimerTickEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            RemainingTime = FormatTime(e.RemainingSeconds);

            if (e.TotalSeconds > 0)
            {
                Progress = (int)((double)e.RemainingSeconds / e.TotalSeconds * 100);
            }
        });
    }

    private void TimerService_TimerCompleted(object? sender, TimerCompletedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (e.CompletedMode == TimerMode.Focus)
            {
                if (_currentSession != null && !_currentSession.Completed)
                {
                    _currentSession.EndTime = DateTime.Now;
                    _currentSession.Completed = true;
                    _storageService.AddSession(_currentSession);
                    TodayStats = _storageService.GetTodayStats();
                    _currentSession = null;
                }

                IsFocusCompleted = true;
                IsPendingBreak = true;
                StartCameraAlert();
                PlayNotificationSound("FocusComplete");
                ShowSystemNotification("专注完成！", "该休息了！");
            }
            else if (e.CompletedMode == TimerMode.Break)
            {
                IsBreakCompleted = true;
                ForceStopCameraAlert();
                PlayNotificationSound("BreakComplete");
                ShowSystemNotification("休息完成！", "准备好开始下一轮了吗？");
            }
        });
    }

    private void TimerService_ModeChanged(object? sender, TimerModeChangedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CurrentStatus = e.NewMode;
        });
    }

    private void CameraStatusCallback(string status)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CameraStatus = status;
            IsCameraAlertActive = _cameraService.IsRunning;
        });
    }

    private void CameraErrorCallback(string error)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CameraStatus = error;
            IsCameraAlertActive = false;

            PlayNotificationSound("FocusComplete");
            ShowSystemNotification("摄像头提醒失败", error);

            if (error.Contains("保护释放"))
            {
                IsFocusCompleted = true;
            }

            if (AppSettings.PopupEnabled)
            {
                MessageBox.Show(
                    $"{error}\n\n如果摄像头权限未开启，可以前往 Windows 隐私设置开启摄像头权限。",
                    "摄像头错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                if (error.Contains("权限") || error.Contains("denied") || error.Contains("access"))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("ms-settings:privacy-webcam");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MainViewModel] 打开摄像头隐私设置失败: {ex.Message}");
                    }
                }
            }
        });
    }

    public void StartFocus()
    {
        if (SelectedTask == null)
        {
            MessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _currentSession = new FocusSession
        {
            TaskId = SelectedTask.Id,
            TaskName = SelectedTask.Name,
            StartTime = DateTime.Now,
            FocusMinutes = AppSettings.WorkMinutes
        };

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        _timerService.StartFocus(AppSettings.WorkMinutes);
    }

    public void PauseFocus()
    {
        _timerService.Pause();
    }

    public void ResumeFocus()
    {
        _timerService.Resume();
    }

    public void ResetFocus()
    {
        ForceStopCameraAlert();
        _timerService.Reset();
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
    }

    public void StartBreak(bool isLongBreak = false)
    {
        if (AppSettings.CameraAlertMode == CameraAlertMode.UntilConfirm && IsCameraAlertActive)
        {
            ForceStopCameraAlert();
        }

        int breakMinutes = isLongBreak ? AppSettings.LongBreakMinutes : AppSettings.ShortBreakMinutes;
        _timerService.StartBreak(breakMinutes);

        if (AppSettings.CameraAlertMode == CameraAlertMode.FollowBreak &&
            AppSettings.CameraAlertEnabled &&
            AppSettings.CameraFollowBreakEnabled)
        {
            FireAndForget(_cameraService.StartCameraAsync(), "启动摄像头(跟随休息)");
        }

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        _currentSession = null;
    }

    public void EndBreak()
    {
        ForceStopCameraAlert();
        _timerService.Reset();
        IsBreakCompleted = true;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
    }

    public void SkipBreak()
    {
        ForceStopCameraAlert();
        _timerService.Reset();
        IsBreakCompleted = false;
        IsFocusCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
    }

    private void StartCameraAlert()
    {
        if (!AppSettings.CameraAlertEnabled) return;

        if (!AppSettings.HasShownCameraPrivacyNotice)
        {
            var result = MessageBox.Show(
                "本软件仅在番茄钟结束或休息阶段根据你的设置调用摄像头，用于触发摄像头指示灯提醒。\n\n软件不会拍照、录像、保存或上传摄像头画面。\n\n是否同意启用摄像头提醒？",
                "摄像头隐私声明",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.No)
            {
                AppSettings.CameraAlertEnabled = false;
                _storageService.SaveSettings(AppSettings);
                return;
            }

            AppSettings.HasShownCameraPrivacyNotice = true;
            _storageService.SaveSettings(AppSettings);
        }

        try
        {
            switch (AppSettings.CameraAlertMode)
            {
                case CameraAlertMode.FixedDuration:
                    FireAndForget(_cameraService.StartCameraForDurationAsync(AppSettings.CameraFixedOnSeconds), "摄像头固定时长提醒");
                    break;
                case CameraAlertMode.UntilConfirm:
                    FireAndForget(_cameraService.StartCameraAsync(), "摄像头直到确认提醒");
                    break;
                case CameraAlertMode.FollowBreak:
                    break;
            }
        }
        catch (Exception ex)
        {
            CameraErrorCallback($"摄像头启动失败: {ex.Message}");
        }
    }

    public void StopCameraAlert()
    {
        if (!AppSettings.CameraAlertCanManualClose)
        {
            MessageBox.Show("当前设置不允许手动关闭摄像头提醒，请在设置中开启。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ForceStopCameraAlert();
    }

    private void ForceStopCameraAlert()
    {
        FireAndForget(_cameraService.StopCameraAsync(), "停止摄像头");
    }

    private void PlayNotificationSound(string soundName)
    {
        if (!AppSettings.SoundEnabled) return;

        Task.Run(() =>
        {
            try
            {
                _soundService.PlaySound(soundName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] 播放音效失败: {ex.Message}");
            }
        });
    }

    private void ShowSystemNotification(string title, string message)
    {
        if (!AppSettings.SystemNotificationEnabled) return;
        if (IsWindowActive) return;

        if (NotificationRequested != null)
        {
            NotificationRequested.Invoke(title, message);
            return;
        }

        try
        {
            var notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Information,
                BalloonTipTitle = title,
                BalloonTipText = message
            };
            notifyIcon.ShowBalloonTip(3000);
            notifyIcon.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] 系统通知失败: {ex.Message}");
        }
    }

    private string FormatTime(int seconds)
    {
        int mins = seconds / 60;
        int secs = seconds % 60;
        return $"{mins:D2}:{secs:D2}";
    }

    public void RefreshStats()
    {
        TodayStats = _storageService.GetTodayStats();
    }

    public void RefreshTimerOnWake()
    {
        _timerService.CorrectAfterWake();
        RefreshStats();
    }

    public void AdjustWorkMinutes(int delta)
    {
        var newVal = Math.Clamp(AppSettings.WorkMinutes + delta, 1, 120);
        if (newVal == AppSettings.WorkMinutes) return;
        AppSettings.WorkMinutes = newVal;
        _storageService.SaveSettings(AppSettings);

        if (CurrentStatus == TimerMode.Idle)
        {
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        }
    }

    public void UpdateSettings(Settings settings)
    {
        AppSettings = settings;

        if (CurrentStatus == TimerMode.Idle)
        {
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        }
    }

    public void ReloadSettings()
    {
        AppSettings = _storageService.LoadSettings();

        if (CurrentStatus == TimerMode.Idle)
        {
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        }

        _cameraService.Initialize(AppSettings.CameraIndex, CameraStatusCallback, CameraErrorCallback);
    }

    public void UpdateTasks(List<TaskItem> tasks)
    {
        Tasks = tasks;

        if (SelectedTask == null || !Tasks.Any(t => t.Id == SelectedTask.Id))
        {
            SelectedTask = Tasks.Any() ? Tasks.First() : null;
        }
    }

    public void ReloadTasks()
    {
        Tasks = _storageService.LoadTasks();

        if (SelectedTask == null || !Tasks.Any(t => t.Id == SelectedTask.Id))
        {
            SelectedTask = Tasks.Any() ? Tasks.First() : null;
        }
    }

    private static async void FireAndForget(Task task, string operationName)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] FireAndForget [{operationName}] 异常: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _trayUpdateTimer?.Stop();
        _trayUpdateTimer?.Dispose();

        _timerService.TimerTick -= TimerService_TimerTick;
        _timerService.TimerCompleted -= TimerService_TimerCompleted;
        _timerService.ModeChanged -= TimerService_ModeChanged;
        _timerService.Dispose();
        _soundService.Dispose();

        // 同步停止摄像头（不再 fire-and-forget）
        try
        {
            _cameraService.StopCameraAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] Dispose 停止摄像头异常: {ex.Message}");
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
