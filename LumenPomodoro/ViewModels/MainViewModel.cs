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
    private bool _shouldSuggestLongBreak;
    
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

    public bool ShouldSuggestLongBreak
    {
        get => _shouldSuggestLongBreak;
        set { _shouldSuggestLongBreak = value; OnPropertyChanged(); }
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
            ShouldSuggestLongBreak = TodayStats.CompletedPomodoros > 0 &&
                                     TodayStats.CompletedPomodoros % AppSettings.LongBreakInterval == 0;
            StartCameraAlert();
            PlayNotificationSound();
            if (AppSettings.PopupEnabled)
            {
                ShowFocusCompleteDialog();
            }
            else
            {
                IsPendingBreak = true;
                CurrentStatus = TimerMode.Idle;
            }
            ShowSystemNotification("专注完成！", "该休息了！");
        }
        else if (e.CompletedMode == TimerMode.Break)
        {
            IsBreakCompleted = true;
            StopCameraAlert();
            PlayNotificationSound();
            if (AppSettings.PopupEnabled)
            {
                ShowBreakCompleteDialog();
            }
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

        PlayNotificationSound();
        ShowSystemNotification("摄像头提醒失败", error);

        if (AppSettings.PopupEnabled)
        {
            var result = MessageBox.Show(
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
                catch { }
            }
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
        if (AppSettings.CameraAlertMode == CameraAlertMode.UntilConfirm && IsCameraAlertActive)
        {
            StopCameraAlert();
        }

        int breakMinutes = isLongBreak ? AppSettings.LongBreakMinutes : AppSettings.ShortBreakMinutes;
        _timerService.StartBreak(breakMinutes);
        
        if (AppSettings.CameraAlertMode == CameraAlertMode.FollowBreak && AppSettings.CameraAlertEnabled)
        {
            _ = _cameraService.StartCameraAsync();
        }
        
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

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

        if (!AppSettings.HasShownCameraPrivacyNotice)
        {
            Application.Current.Dispatcher.Invoke(() =>
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
            });

            if (!AppSettings.CameraAlertEnabled) return;
        }
        
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
        if (!AppSettings.CameraAlertCanManualClose)
        {
            MessageBox.Show("当前设置不允许手动关闭摄像头提醒，请在设置中开启。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
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
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SuggestCount = TodayStats.CompletedPomodoros
            };
            dialog.SetLongBreakSuggestion(ShouldSuggestLongBreak);
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                if (dialog.ShouldStartBreak)
                {
                    StartBreak(isLongBreak: dialog.ShouldStartLongBreak);
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