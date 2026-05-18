using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.Views;
using Serilog;

namespace LumenPomodoro.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITimerService _timerService;
    private readonly ICameraService _cameraService;
    private readonly IStorageService _storageService;
    private readonly ISoundService _soundService;

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

    // 考试倒计时
    public bool ExamCountdown => AppSettings.ExamDate.HasValue && AppSettings.ExamDate.Value > DateTime.Today;
    public string ExamName => AppSettings.ExamName;
    public int DaysUntilExam => ExamCountdown ? (AppSettings.ExamDate!.Value - DateTime.Today).Days : 0;

    // Expose interfaces for other consumers (TrayService, SettingsViewModel, etc.)
    public ICameraService CameraService => _cameraService;
    public IStorageService StorageService => _storageService;

    private FocusSession? _currentSession;
    private string? _lastCompletedSessionId;
    private bool _disposed;
    private CameraIndicatorWindow? _indicatorWindow;
    private DispatcherTimer? _trayUpdateTimer;
    private int _consecutivePresenceLostAlerts = 0;
    private const int MaxPresenceLostAlerts = 3;

    // 专注笔记
    private string _currentNotes = string.Empty;
    public string CurrentNotes
    {
        get => _currentNotes;
        set { if (_currentNotes != value) { _currentNotes = value; OnPropertyChanged(); } }
    }

    // 质量评分跟踪
    private bool _sessionPaused = false;
    private bool _sessionPresenceLost = false;

    // 质量评分显示
    private string _qualityStars = string.Empty;
    public string QualityStars
    {
        get => _qualityStars;
        set { if (_qualityStars != value) { _qualityStars = value; OnPropertyChanged(); } }
    }

    // Streak 显示
    private int _streakDays;
    public int StreakDays
    {
        get => _streakDays;
        set { if (_streakDays != value) { _streakDays = value; OnPropertyChanged(); } }
    }

    private bool _showStreakEncouragement;
    public bool ShowStreakEncouragement
    {
        get => _showStreakEncouragement;
        set { if (_showStreakEncouragement != value) { _showStreakEncouragement = value; OnPropertyChanged(); } }
    }

    public event Action? TrayMenuNeedsUpdate;
    public event Action<string, string>? NotificationRequested;
    public event Action<string, string>? InAppNotificationRequested;
    public event Action<string>? CountdownStartRequested;
    public event Action<string>? CountdownUpdateRequested;
    public event Action? CountdownStopRequested;

    private bool _isWindowTopmost;
    public bool IsWindowTopmost
    {
        get => _isWindowTopmost;
        set { if (_isWindowTopmost != value) { _isWindowTopmost = value; OnPropertyChanged(); } }
    }

    public MainViewModel(IStorageService storageService, ITimerService timerService,
        ICameraService cameraService, ISoundService soundService)
    {
        _storageService = storageService;
        _timerService = timerService;
        _cameraService = cameraService;
        _soundService = soundService;

        _timerService.TimerTick += TimerService_TimerTick;
        _timerService.TimerCompleted += TimerService_TimerCompleted;
        _timerService.ModeChanged += TimerService_ModeChanged;

        LoadData();

        _cameraService.Initialize(AppSettings.CameraIndex, CameraStatusCallback, CameraErrorCallback, OnPresenceLost, OnPresenceRegained);

        if (AppSettings.TrayEnabled)
        {
            _trayUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _trayUpdateTimer.Tick += (s, e) => TrayMenuNeedsUpdate?.Invoke();
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
            var lastId = AppSettings.LastSelectedTaskId;
            SelectedTask = lastId != null
                ? Tasks.FirstOrDefault(t => t.Id == lastId) ?? Tasks.First()
                : Tasks.First();
        }

        RefreshStreak();
    }

    private void TimerService_TimerTick(object? sender, TimerTickEventArgs e)
    {
        // DispatcherTimer 在 UI 线程触发，无需 BeginInvoke
        RemainingTime = FormatTime(e.RemainingSeconds);

        if (e.TotalSeconds > 0)
        {
            Progress = (int)((double)e.RemainingSeconds / e.TotalSeconds * 100);
        }

        // 更新灵动岛倒计时
        if (!IsWindowTopmost)
        {
            CountdownUpdateRequested?.Invoke(RemainingTime);
        }
    }

    private void TimerService_TimerCompleted(object? sender, TimerCompletedEventArgs e)
    {
        // DispatcherTimer 在 UI 线程触发，无需 BeginInvoke
        if (e.CompletedMode == TimerMode.Focus)
        {
            if (_currentSession != null && !_currentSession.Completed)
            {
                _currentSession.EndTime = DateTime.Now;
                _currentSession.Completed = true;

                // 计算质量评分
                int score = 1; // 基础分：完整完成
                if (!_sessionPaused) score++;
                if (!_sessionPresenceLost) score++;
                _currentSession.QualityScore = score;
                QualityStars = new string('★', score) + new string('☆', 3 - score);

                _storageService.AddSession(_currentSession);
                _lastCompletedSessionId = _currentSession.Id;
                TodayStats = _storageService.GetTodayStats();
                _currentSession = null;
            }

            Progress = 0;
            IsFocusCompleted = true;
            IsPendingBreak = true;
            StartCameraAlert();
            PlayNotificationSound("FocusComplete");
            ShowInAppNotification("专注完成", "该休息了！");
            CheckMilestones();
            RefreshStreak();
            CountdownStopRequested?.Invoke();
        }
        else if (e.CompletedMode == TimerMode.Break)
        {
            IsBreakCompleted = true;
            ForceStopCameraAlert();
            PlayNotificationSound("BreakComplete");
            ShowSystemNotification("休息完成！", "准备好开始下一轮了吗？");
            CountdownStopRequested?.Invoke();
        }
    }

    private void TimerService_ModeChanged(object? sender, TimerModeChangedEventArgs e)
    {
        // DispatcherTimer 在 UI 线程触发，无需 BeginInvoke
        CurrentStatus = e.NewMode;
    }

    private void CameraStatusCallback(string status)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CameraStatus = status;
            IsCameraAlertActive = _cameraService.IsRunning;
            if (!_cameraService.IsRunning)
                HideCameraIndicator();
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
                        Log.Warning(ex, "打开摄像头隐私设置失败");
                    }
                }
            }
        });
    }

    public void StartFocus()
    {
        _consecutivePresenceLostAlerts = 0;
        _sessionPaused = false;
        _sessionPresenceLost = false;
        CurrentNotes = string.Empty;
        QualityStars = string.Empty;
        _lastCompletedSessionId = null;

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

        AppSettings.LastSelectedTaskId = SelectedTask.Id;
        _storageService.SaveSettings(AppSettings);

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        _timerService.StartFocus(AppSettings.WorkMinutes);

        // 启动灵动岛倒计时
        if (!IsWindowTopmost)
        {
            var taskName = SelectedTask.Name;
            CountdownStartRequested?.Invoke($"专注 · {taskName}");
        }
    }

    public void PauseFocus()
    {
        _sessionPaused = true;
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
        _currentSession = null;
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        CountdownStopRequested?.Invoke();
    }

    public void StartBreak(bool isLongBreak = false)
    {
        _consecutivePresenceLostAlerts = 0;

        // 保存笔记到已完成的 session
        SaveNotesToLastSession();

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
            FireAndForgetAsync(Task.Run(() => _cameraService.StartCameraAsync()), "启动摄像头(跟随休息)",
                ex => CameraErrorCallback($"摄像头启动失败: {ex.Message}"));
            ShowCameraIndicator(Color.FromRgb(0x10, 0xB9, 0x81));
        }

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        _currentSession = null;

        // 启动灵动岛倒计时
        if (!IsWindowTopmost)
        {
            var breakType = isLongBreak ? "长休息" : "短休息";
            CountdownStartRequested?.Invoke(breakType);
        }
    }

    public void EndBreak()
    {
        ForceStopCameraAlert();
        _timerService.Reset();
        IsBreakCompleted = true;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;

        // 停止灵动岛倒计时
        CountdownStopRequested?.Invoke();
    }

    public void SkipBreak()
    {
        // 保存笔记到已完成的 session
        SaveNotesToLastSession();

        ForceStopCameraAlert();
        _timerService.Reset();
        IsBreakCompleted = false;
        IsFocusCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;

        // 停止灵动岛倒计时
        CountdownStopRequested?.Invoke();
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
            bool cameraStarted = false;
            switch (AppSettings.CameraAlertMode)
            {
                case CameraAlertMode.FixedDuration:
                    FireAndForgetAsync(Task.Run(() => _cameraService.StartCameraForDurationAsync(AppSettings.CameraFixedOnSeconds)), "摄像头固定时长提醒",
                        ex => CameraErrorCallback($"摄像头启动失败: {ex.Message}"));
                    cameraStarted = true;
                    break;
                case CameraAlertMode.UntilConfirm:
                    FireAndForgetAsync(Task.Run(() => _cameraService.StartCameraAsync()), "摄像头直到确认提醒",
                        ex => CameraErrorCallback($"摄像头启动失败: {ex.Message}"));
                    cameraStarted = true;
                    break;
                case CameraAlertMode.FollowBreak:
                    break;
            }

            if (cameraStarted)
            {
                ShowCameraIndicator(Color.FromRgb(0xF5, 0x9E, 0x0B));
                ApplyAlertLevel();
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
        _consecutivePresenceLostAlerts = 0;
        FireAndForgetAsync(Task.Run(() => _cameraService.StopCameraAsync()), "停止摄像头");
        HideCameraIndicator();
    }

    private void OnPresenceLost()
    {
        if (!AppSettings.PresenceDetectionEnabled) return;

        _sessionPresenceLost = true;

        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _consecutivePresenceLostAlerts++;
            if (_consecutivePresenceLostAlerts > MaxPresenceLostAlerts) return;

            ShowSystemNotification("走神提醒", "检测到你已离开，请回到专注状态。");

            if (AppSettings.CameraAlertLevel == CameraAlertLevel.Severe)
            {
                if (Application.Current.MainWindow is Window mainWindow)
                {
                    mainWindow.Activate();
                    mainWindow.Topmost = true;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (s, e) =>
                    {
                        mainWindow.Topmost = false;
                        ((DispatcherTimer)s!).Stop();
                    };
                    timer.Start();
                }
            }
        });
    }

    private void OnPresenceRegained()
    {
        _consecutivePresenceLostAlerts = 0;
    }

    private void ShowCameraIndicator(Color color)
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _indicatorWindow ??= new CameraIndicatorWindow();
            _indicatorWindow.ShowIndicator(color);
        });
    }

    private void HideCameraIndicator()
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _indicatorWindow?.HideIndicator();
        });
    }

    private void ApplyAlertLevel()
    {
        switch (AppSettings.CameraAlertLevel)
        {
            case CameraAlertLevel.Light:
                break;
            case CameraAlertLevel.Medium:
                break;
            case CameraAlertLevel.Severe:
                if (Application.Current?.Dispatcher == null) return;
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (Application.Current.MainWindow is Window mainWindow)
                    {
                        mainWindow.Activate();
                        mainWindow.Topmost = true;
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                        timer.Tick += (s, e) =>
                        {
                            mainWindow.Topmost = false;
                            ((DispatcherTimer)s!).Stop();
                        };
                        timer.Start();
                    }
                });
                break;
        }
    }

    private void PlayNotificationSound(string soundName)
    {
        if (!AppSettings.SoundEnabled) return;

        // SoundPlayer.Play() 本身是异步非阻塞的，无需 Task.Run
        try
        {
            _soundService.PlaySound(soundName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "播放音效 {Name} 失败", soundName);
        }
    }

    private void ShowInAppNotification(string title, string message)
    {
        if (!AppSettings.PopupEnabled) return;
        InAppNotificationRequested?.Invoke(title, message);
    }

    private void SaveNotesToLastSession()
    {
        if (_lastCompletedSessionId != null && !string.IsNullOrWhiteSpace(CurrentNotes))
        {
            _storageService.UpdateSession(_lastCompletedSessionId, session =>
            {
                session.Notes = CurrentNotes.Trim();
            });
        }
        CurrentNotes = string.Empty;
        _lastCompletedSessionId = null;
    }

    private void RefreshStreak()
    {
        var completed = _storageService.LoadSessions()
            .Where(s => s.Completed && s.EndTime.HasValue).ToList();
        StreakDays = InsightEngine.CalculateStreak(completed);

        ShowStreakEncouragement = false;
        if (StreakDays == 0 && completed.Any())
        {
            var lastSession = completed.MaxBy(s => s.EndTime);
            if (lastSession != null && (DateTime.Today - lastSession.EndTime!.Value.Date).TotalDays >= 1)
                ShowStreakEncouragement = true;
        }
    }

    private void CheckMilestones()
    {
        var todayCount = TodayStats.CompletedPomodoros;
        var todayMinutes = TodayStats.TotalFocusMinutes;

        if (todayCount == 1)
            ShowInAppNotification("里程碑", "第一个番茄完成！");

        if (todayMinutes >= AppSettings.DailyGoalMinutes && AppSettings.DailyGoalMinutes > 0)
            ShowInAppNotification("里程碑", "今日目标达成！");

        if (AppSettings.DailyTargetPomodoros > 0 && todayCount >= AppSettings.DailyTargetPomodoros)
            ShowInAppNotification("里程碑", "今日番茄目标达成！");
    }

    public DailyReport? GetYesterdayReport()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var sessions = _storageService.LoadSessions()
            .Where(s => s.Completed && s.EndTime.HasValue && s.EndTime.Value.Date == yesterday)
            .ToList();

        if (!sessions.Any()) return null;

        var mainTask = sessions.GroupBy(s => s.TaskName)
            .OrderByDescending(g => g.Sum(s => s.FocusMinutes))
            .First().Key;

        var uniqueTasks = sessions.Select(s => s.TaskName).Distinct().Count();
        var avgQuality = sessions.Where(s => s.QualityScore > 0).Average(s => (double)s.QualityScore);

        return new DailyReport
        {
            Date = yesterday,
            CompletedPomodoros = sessions.Count,
            TotalMinutes = sessions.Sum(s => s.FocusMinutes),
            MainTask = mainTask,
            StreakDays = CalculateStreak(),
            AvgQualityScore = Math.Round(avgQuality, 1),
            UniqueTasksCount = uniqueTasks
        };
    }

    private int CalculateStreak()
    {
        var completed = _storageService.LoadSessions()
            .Where(s => s.Completed && s.EndTime.HasValue)
            .ToList();

        return InsightEngine.CalculateStreak(completed);
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
            Log.Warning(ex, "系统通知失败");
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
        var raw = AppSettings.WorkMinutes + delta;
        var rounded = (int)(Math.Round(raw / 5.0) * 5);
        var newVal = Math.Clamp(rounded, 1, 120);
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

        _cameraService.Initialize(AppSettings.CameraIndex, CameraStatusCallback, CameraErrorCallback, OnPresenceLost, OnPresenceRegained);
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

    private static async Task FireAndForgetAsync(Task task, string operationName, Action<Exception>? onError = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("[{Operation}] 操作被取消", operationName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "FireAndForget [{Operation}] 异常", operationName);
            onError?.Invoke(ex);
        }
    }

    // 兼容旧调用的包装方法（标记为过时）
    [Obsolete("请使用 FireAndForgetAsync 替代")]
    private static async void FireAndForget(Task task, string operationName, Action<Exception>? onError = null)
    {
        await FireAndForgetAsync(task, operationName, onError);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 保存进行中的 session
        if (_currentSession != null && !_currentSession.Completed)
        {
            _currentSession.EndTime = DateTime.Now;
            _currentSession.Completed = false;
            _storageService.AddSession(_currentSession);
            _currentSession = null;
        }

        _trayUpdateTimer?.Stop();
        try { _indicatorWindow?.ForceClose(); } catch { }
        _indicatorWindow = null;

        _timerService.TimerTick -= TimerService_TimerTick;
        _timerService.TimerCompleted -= TimerService_TimerCompleted;
        _timerService.ModeChanged -= TimerService_ModeChanged;
        _timerService.Dispose();
        _soundService.Dispose();

        try
        {
            var task = Task.Run(() => _cameraService.StopCameraAsync());
            FireAndForgetAsync(task, "Dispose 停止摄像头");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Dispose 停止摄像头异常");
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected bool SetProperty<T>(ref T field, T value, IEqualityComparer<T> comparer, [CallerMemberName] string? propertyName = null)
    {
        if (comparer.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
