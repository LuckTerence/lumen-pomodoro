using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TimerService _timerService;
    private readonly CameraService _cameraService;
    private readonly StorageService _storageService;
    private readonly SoundService _soundService;
    
    private TimerMode _currentStatus;
    private string _remainingTime;
    private int _progress;
    private TaskItem? _selectedTask;
    private List<TaskItem> _tasks;
    private DailyStats _todayStats;
    private string _cameraStatus;
    private bool _isCameraAlertActive;
    private bool _isFocusCompleted;
    private bool _isBreakCompleted;
    private bool _isPendingBreak;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public TimerMode CurrentStatus
    {
        get => _currentStatus;
        set { _currentStatus = value; OnPropertyChanged(); }
    }

    public string RemainingTime
    {
        get => _remainingTime;
        set { _remainingTime = value; OnPropertyChanged(); }
    }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set { _selectedTask = value; OnPropertyChanged(); }
    }

    public List<TaskItem> Tasks
    {
        get => _tasks;
        set { _tasks = value; OnPropertyChanged(); }
    }

    public DailyStats TodayStats
    {
        get => _todayStats;
        set { _todayStats = value; OnPropertyChanged(); }
    }

    public string CameraStatus
    {
        get => _cameraStatus;
        set { _cameraStatus = value; OnPropertyChanged(); }
    }

    public bool IsCameraAlertActive
    {
        get => _isCameraAlertActive;
        set { _isCameraAlertActive = value; OnPropertyChanged(); }
    }

    public bool IsFocusCompleted
    {
        get => _isFocusCompleted;
        set { _isFocusCompleted = value; OnPropertyChanged(); }
    }

    public bool IsBreakCompleted
    {
        get => _isBreakCompleted;
        set { _isBreakCompleted = value; OnPropertyChanged(); }
    }

    public bool IsPendingBreak
    {
        get => _isPendingBreak;
        set { _isPendingBreak = value; OnPropertyChanged(); }
    }

    public Settings AppSettings { get; private set; }
    
    private FocusSession? _currentSession;

    public MainViewModel()
    {
        _timerService = new TimerService();
        _cameraService = new CameraService();
        _storageService = new StorageService();
        _soundService = new SoundService();
        
        SoundService.GenerateDefaultWavFiles();
        
        _timerService.TimerTick += TimerService_TimerTick;
        _timerService.TimerCompleted += TimerService_TimerCompleted;
        _timerService.ModeChanged += TimerService_ModeChanged;
        
        _cameraService.Initialize(0, CameraStatusCallback, CameraErrorCallback);
        
        LoadData();
        
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        CurrentStatus = TimerMode.Idle;
        Progress = 100;
        CameraStatus = string.Empty;
        IsCameraAlertActive = false;
        IsFocusCompleted = false;
        IsBreakCompleted = false;
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
        RemainingTime = FormatTime(e.RemainingSeconds);
        
        if (e.TotalSeconds > 0)
        {
            Progress = (int)((double)e.RemainingSeconds / e.TotalSeconds * 100);
        }
    }

    private void TimerService_TimerCompleted(object? sender, TimerCompletedEventArgs e)
    {
        if (e.CompletedMode == TimerMode.Focus)
        {
            // Record session immediately when focus completes
            if (_currentSession != null && !_currentSession.Completed)
            {
                _currentSession.EndTime = DateTime.Now;
                _currentSession.Completed = true;
                _storageService.AddSession(_currentSession);
                TodayStats = _storageService.GetTodayStats();
                _currentSession = null; // Clear after recording
            }
            
            IsFocusCompleted = true;
            StartCameraAlert();
            PlayNotificationSound();
            ShowFocusCompleteDialog();
            ShowSystemNotification("专注完成！", "该休息了！");
        }
        else if (e.CompletedMode == TimerMode.Break)
        {
            IsBreakCompleted = true;
            StopCameraAlert();
            PlayNotificationSound();
            ShowBreakCompleteDialog();
            ShowSystemNotification("休息完成！", "准备好开始下一轮了吗？");
        }
    }

    private void TimerService_ModeChanged(object? sender, TimerModeChangedEventArgs e)
    {
        CurrentStatus = e.NewMode;
    }

    private void CameraStatusCallback(string status)
    {
        CameraStatus = status;
        IsCameraAlertActive = _cameraService.IsRunning;
    }

    private void CameraErrorCallback(string error)
    {
        CameraStatus = error;
        IsCameraAlertActive = false;

        // 兜底提醒：摄像头不可用时，确保其他提醒方式仍然触发
        PlayNotificationSound();
        ShowSystemNotification("摄像头提醒失败", error);

        if (AppSettings.PopupEnabled)
        {
            MessageBox.Show(error, "摄像头错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        StopCameraAlert();
        _timerService.Reset();
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
    }

    public void StartBreak(bool isLongBreak = false)
    {
        int breakMinutes = isLongBreak ? AppSettings.LongBreakMinutes : AppSettings.ShortBreakMinutes;
        _timerService.StartBreak(breakMinutes);
        
        if (AppSettings.CameraAlertMode == CameraAlertMode.FollowBreak && AppSettings.CameraAlertEnabled)
        {
            _ = _cameraService.StartCameraAsync();
        }
        
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        // Session already recorded in TimerCompleted handler before this method is called
        _currentSession = null;
    }

    public void EndBreak()
    {
        StopCameraAlert();
        _timerService.Reset();
        IsBreakCompleted = true;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
    }

    public void SkipBreak()
    {
        // If user skips break, session is already recorded
        StopCameraAlert();
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
        
        try
        {
            switch (AppSettings.CameraAlertMode)
            {
                case CameraAlertMode.FixedDuration:
                    _ = _cameraService.StartCameraForDurationAsync(AppSettings.CameraFixedOnSeconds);
                    break;
                case CameraAlertMode.UntilConfirm:
                    _ = _cameraService.StartCameraAsync();
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
        _ = _cameraService.StopCameraAsync();
        IsCameraAlertActive = false;
    }

    private void PlayNotificationSound()
    {
        if (!AppSettings.SoundEnabled) return;
        
        try
        {
            _soundService.PlaySound("FocusComplete");
        }
        catch { }
    }

    private void ShowSystemNotification(string title, string message)
    {
        if (!AppSettings.SystemNotificationEnabled) return;
        
        try
        {
            var notification = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Information,
                BalloonTipTitle = title,
                BalloonTipText = message
            };
            notification.ShowBalloonTip(3000);
            notification.Dispose();
        }
        catch { }
    }

    private void ShowFocusCompleteDialog()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.FocusCompleteDialog
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                if (dialog.ShouldStartBreak)
                {
                    StartBreak(isLongBreak: false);
                }
            }
            else
            {
                IsFocusCompleted = false;
                IsPendingBreak = true;
                CurrentStatus = TimerMode.Idle;
            }
        });
    }

    private void ShowBreakCompleteDialog()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.BreakCompleteDialog
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.ShowDialog();
        });
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

    public void UpdateSettings(Settings settings)
    {
        AppSettings = settings;
        _storageService.SaveSettings(settings);
        
        if (CurrentStatus == TimerMode.Idle)
        {
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        }
    }

    public void UpdateTasks(List<TaskItem> tasks)
    {
        Tasks = tasks;
        _storageService.SaveTasks(tasks);
        
        if (SelectedTask == null && Tasks.Any())
        {
            SelectedTask = Tasks.First();
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}