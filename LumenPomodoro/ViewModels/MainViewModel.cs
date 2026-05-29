using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly TimerController _timerController;
    private readonly NotificationCoordinator _notifications;
    private readonly CameraAlertController _cameraAlert;
    private readonly IFocusGuardService _focusGuard;
    private int _focusGuardConsecutiveAlerts;
    private const int MaxFocusGuardAlerts = 3;

    private TimerMode _currentStatus = TimerMode.Idle;
    private string _remainingTime = "25:00";
    private int _progress = 100;
    private TaskItem? _selectedTask;
    private List<TaskItem> _tasks = new();
    private DailyStats _todayStats = new();
    private bool _isFocusCompleted;
    private bool _isBreakCompleted;
    private bool _isPendingBreak;
    private bool _isWindowActive;
    private bool _disposed;

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
        set { if (!ReferenceEquals(_todayStats, value)) { _todayStats = value; OnPropertyChanged(); OnPropertyChanged(nameof(TodayPomodoroProgress)); OnPropertyChanged(nameof(TodayPomodoroBarWidth)); } }
    }

    public string CameraStatus => _cameraAlert.Status;

    public bool IsCameraAlertActive => _cameraAlert.IsActive;

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

    public bool ExamCountdown => AppSettings.ExamCountdownEnabled && AppSettings.ExamDate.HasValue && AppSettings.ExamDate.Value > DateTime.Today;
    public string ExamName => AppSettings.ExamName;
    public int DaysUntilExam => ExamCountdown ? (AppSettings.ExamDate!.Value - DateTime.Today).Days : 0;

    public int LongBreakInterval => AppSettings.LongBreakInterval;
    public bool IsInsightsEnabled => AppSettings.InsightsEnabled;
    public bool IsDailyReportEnabled => AppSettings.DailyReportEnabled;
    public bool IsDynamicIslandEnabled => AppSettings.DynamicIslandEnabled;

    public ICameraService CameraService { get; }
    public IStorageService StorageService => _storageService;

    private string? _lastCompletedSessionId;

    // 专注笔记
    private string _currentNotes = string.Empty;
    public string CurrentNotes
    {
        get => _currentNotes;
        set { if (_currentNotes != value) { _currentNotes = value; OnPropertyChanged(); } }
    }

    // 手动评分 (1-5 星)
    private int _userRating;
    private string _ratingStars = string.Empty;
    public int UserRating
    {
        get => _userRating;
        set
        {
            if (_userRating != value)
            {
                _userRating = value;
                _ratingStars = SessionScoringController.GetRatingStars(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(RatingStars));
            }
        }
    }
    public string RatingStars => _ratingStars;

    // 最近完成的专注摘要
    private string _lastCompletedTaskName = string.Empty;
    public string LastCompletedTaskName
    {
        get => _lastCompletedTaskName;
        set { if (_lastCompletedTaskName != value) { _lastCompletedTaskName = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastCompletedSummary)); } }
    }

    private int _lastCompletedFocusMinutes;
    public int LastCompletedFocusMinutes
    {
        get => _lastCompletedFocusMinutes;
        set { if (_lastCompletedFocusMinutes != value) { _lastCompletedFocusMinutes = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastCompletedSummary)); } }
    }

    public string LastCompletedSummary =>
        SessionScoringController.GetCompletedSummary(LastCompletedTaskName, LastCompletedFocusMinutes);

    public double TodayPomodoroProgress =>
        AppSettings.DailyTargetPomodoros > 0
            ? Math.Min(100.0, (double)TodayStats.CompletedPomodoros / AppSettings.DailyTargetPomodoros * 100)
            : 0;

    public double TodayPomodoroBarWidth =>
        AppSettings.DailyTargetPomodoros > 0
            ? Math.Min(240.0, 240.0 * TodayStats.CompletedPomodoros / AppSettings.DailyTargetPomodoros)
            : 0;

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

    private bool _suggestLongBreak;
    public bool SuggestLongBreak
    {
        get => _suggestLongBreak;
        set { if (_suggestLongBreak != value) { _suggestLongBreak = value; OnPropertyChanged(); } }
    }

    private int _todayCompletedCount;
    public int TodayCompletedCount
    {
        get => _todayCompletedCount;
        set { if (_todayCompletedCount != value) { _todayCompletedCount = value; OnPropertyChanged(); } }
    }

    // 事件（View 订阅）
    public event Action? TrayMenuNeedsUpdate;
    public event Action<string, string>? NotificationRequested;
    public event Action<string, string>? InAppNotificationRequested;
    public event Action<string>? CountdownStartRequested;
    public event Action<string>? CountdownUpdateRequested;
    public event Action? CountdownStopRequested;

    internal const double TopmostDurationSeconds = 3;
    private bool _isWindowTopmost;
    public bool IsWindowTopmost
    {
        get => _isWindowTopmost;
        set { if (_isWindowTopmost != value) { _isWindowTopmost = value; OnPropertyChanged(); } }
    }

    public MainViewModel(IStorageService storageService, ITimerService timerService,
        ICameraService cameraService, ISoundService soundService,
        IFocusGuardService? focusGuard = null)
    {
        _storageService = storageService;
        CameraService = cameraService;
        _focusGuard = focusGuard ?? new FocusGuardService();

        _timerController = new TimerController(timerService);
        _notifications = new NotificationCoordinator(soundService);

        // 代理 NotificationCoordinator 事件到 MainViewModel 事件
        _notifications.TrayMenuNeedsUpdate += () => TrayMenuNeedsUpdate?.Invoke();
        _notifications.NotificationRequested += (t, m) => NotificationRequested?.Invoke(t, m);
        _notifications.InAppNotificationRequested += (t, m) => InAppNotificationRequested?.Invoke(t, m);
        _notifications.CountdownStartRequested += m => CountdownStartRequested?.Invoke(m);
        _notifications.CountdownUpdateRequested += m => CountdownUpdateRequested?.Invoke(m);
        _notifications.CountdownStopRequested += () => CountdownStopRequested?.Invoke();

        // 代理 TimerController 事件
        _timerController.TickUpdated += OnTimerTickUpdated;
        _timerController.FocusCompleted += OnFocusCompleted;
        _timerController.BreakCompleted += OnBreakCompleted;
        _timerController.ModeChanged += mode => CurrentStatus = mode;

        // CameraAlertController
        _cameraAlert = new CameraAlertController(cameraService);
        _cameraAlert.StatusChanged += OnCameraStatusChanged;
        _cameraAlert.ErrorOccurred += HandleCameraError;
        _cameraAlert.SystemNotificationRequested += OnCameraSystemNotification;
        _cameraAlert.WindowActivationRequested += OnCameraWindowActivation;

        // FocusGuardService（防走神：前台窗口 + 键鼠空闲）
        _focusGuard.DistractionDetected += OnFocusGuardDistraction;
        _focusGuard.FocusRegained += OnFocusGuardRegained;

        LoadData();

        _cameraAlert.Initialize(AppSettings);

        if (AppSettings.TrayEnabled)
        {
            _notifications.StartTrayTimer();
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
                ? Tasks.FirstOrDefault(t => t.Id == lastId) ?? Tasks.FirstOrDefault()
                : Tasks.FirstOrDefault();
        }

        RefreshStreak();
    }

    // ── TimerController 事件处理 ──

    private void OnTimerTickUpdated(int remainingSeconds, int totalSeconds)
    {
        RemainingTime = FormatTime(remainingSeconds);

        if (totalSeconds > 0)
        {
            Progress = (int)((double)remainingSeconds / totalSeconds * 100);
        }

        _notifications.UpdateCountdown(RemainingTime, IsWindowTopmost, AppSettings.DynamicIslandEnabled);
    }

    private void OnFocusCompleted(FocusSession session)
    {
        UserRating = 0;
        LastCompletedTaskName = session.TaskName;
        LastCompletedFocusMinutes = session.FocusMinutes;
        _storageService.AddSession(session);
        _lastCompletedSessionId = session.Id;
        TodayStats = _storageService.GetTodayStats();

        _focusGuard.Stop();

        Progress = 0;
        IsFocusCompleted = true;
        IsPendingBreak = true;

        var todayCount = TodayStats.CompletedPomodoros;
        TodayCompletedCount = todayCount;
        SuggestLongBreak = SessionScoringController.ShouldSuggestLongBreak(todayCount, AppSettings);

        _cameraAlert.Start(AppSettings);
        _storageService.SaveSettings(AppSettings);
        _notifications.PlaySound("FocusComplete", AppSettings.SoundEnabled);
        _notifications.ShowInApp(Properties.LocalizedStrings.Focus_Complete, Properties.LocalizedStrings.Break_Time, AppSettings.PopupEnabled);
        SessionScoringController.CheckMilestones(TodayStats, AppSettings,
            (t, m) => _notifications.ShowInApp(t, m, AppSettings.PopupEnabled));
        RefreshStreak();
        _notifications.StopCountdown();
    }

    private void OnBreakCompleted()
    {
        IsBreakCompleted = true;
        _cameraAlert.ForceStop();
        _notifications.PlaySound("BreakComplete", AppSettings.SoundEnabled);
        _notifications.ShowSystem(Properties.LocalizedStrings.Break_Complete, Properties.LocalizedStrings.Break_Ready, AppSettings.SystemNotificationEnabled, IsWindowActive);
        _notifications.StopCountdown();
    }

    // ── CameraAlertController 事件处理 ──

    private void OnCameraStatusChanged(string _)
    {
        OnPropertyChanged(nameof(CameraStatus));
        OnPropertyChanged(nameof(IsCameraAlertActive));
    }

    private void OnCameraSystemNotification(string title, string message)
    {
        _notifications.ShowSystem(title, message, AppSettings.SystemNotificationEnabled, IsWindowActive);
    }

    private void OnCameraWindowActivation(Window window)
    {
        window.Activate();
        window.Topmost = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TopmostDurationSeconds) };
        timer.Tick += (s, e) => { window.Topmost = false; ((DispatcherTimer)s!).Stop(); };
        timer.Start();
    }

    private void HandleCameraError(string error)
    {
        _notifications.PlaySound("FocusComplete", AppSettings.SoundEnabled);
        _notifications.ShowSystem("摄像头提醒失败", error, AppSettings.SystemNotificationEnabled, IsWindowActive);

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
    }

    // ── FocusGuardService 事件处理（防走神） ──

    private void OnFocusGuardDistraction(string reason)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        dispatcher.BeginInvoke(() =>
        {
            _focusGuardConsecutiveAlerts++;
            if (_focusGuardConsecutiveAlerts > MaxFocusGuardAlerts) return;

            // 防走神有独立开关与强度，提醒不受番茄钟完成提醒的全局开关（声音/系统通知）静音。
            var level = AppSettings.FocusGuardAlertLevel;

            _notifications.ShowSystem("走神提醒", reason, systemNotificationEnabled: true, IsWindowActive);

            if (level >= CameraAlertLevel.Medium)
            {
                _notifications.PlaySound("FocusComplete", soundEnabled: true);
            }

            if (level == CameraAlertLevel.Severe &&
                Application.Current?.MainWindow is Window mainWindow)
            {
                OnCameraWindowActivation(mainWindow);
            }
        });
    }

    private void OnFocusGuardRegained()
    {
        Application.Current?.Dispatcher?.BeginInvoke(() => _focusGuardConsecutiveAlerts = 0);
    }

    // ── 公共方法（对外 API 不变） ──

    public void StartFocus()
    {
        CurrentNotes = string.Empty;
        UserRating = 0;
        _lastCompletedSessionId = null;
        SuggestLongBreak = false;

        if (SelectedTask == null)
        {
            MessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _timerController.StartFocus(SelectedTask, AppSettings);
        _storageService.SaveSettings(AppSettings);

        _focusGuardConsecutiveAlerts = 0;
        _focusGuard.Start(AppSettings);

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        _notifications.StartCountdown(string.Format(Properties.LocalizedStrings.Focus_Start, SelectedTask.Name), IsWindowTopmost, AppSettings.DynamicIslandEnabled);
    }

    public void PauseFocus()
    {
        _timerController.PauseFocus();
        _focusGuard.Stop();
    }

    public void ResumeFocus()
    {
        _timerController.ResumeFocus();
        _focusGuardConsecutiveAlerts = 0;
        _focusGuard.Start(AppSettings);
    }

    public void ResetFocus()
    {
        _focusGuard.Stop();
        _cameraAlert.ForceStop();
        _timerController.ResetFocus();
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        SuggestLongBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        _notifications.StopCountdown();
    }

    public void StartBreak(bool isLongBreak = false)
    {
        SaveNotesToLastSession();
        _focusGuard.Stop();

        if (AppSettings.CameraAlertMode == CameraAlertMode.UntilConfirm && _cameraAlert.IsActive)
        {
            _cameraAlert.ForceStop();
        }

        _timerController.StartBreak(isLongBreak, AppSettings);

        _cameraAlert.StartForBreak(AppSettings);

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        _notifications.StartCountdown(
            isLongBreak ? Properties.LocalizedStrings.Long_Break : Properties.LocalizedStrings.Short_Break,
            IsWindowTopmost,
            AppSettings.DynamicIslandEnabled);
    }

    public void EndBreak()
    {
        _cameraAlert.ForceStop();
        _timerController.EndBreak();
        IsBreakCompleted = true;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        _notifications.StopCountdown();
    }

    public void SkipBreak()
    {
        SaveNotesToLastSession();

        _cameraAlert.ForceStop();
        _timerController.SkipBreak();
        IsBreakCompleted = false;
        IsFocusCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        _notifications.StopCountdown();
    }

    public void StopCameraAlert()
    {
        _cameraAlert.TryStop(AppSettings);
    }

    public void ApplyPreset(PomodoroPreset preset)
    {
        if (preset.WorkMinutes <= 0) return; // 自定义预设不覆盖当前值

        AppSettings.WorkMinutes = preset.WorkMinutes;
        AppSettings.ShortBreakMinutes = preset.ShortBreakMinutes;
        AppSettings.LongBreakMinutes = preset.LongBreakMinutes;
        AppSettings.LongBreakInterval = preset.LongBreakInterval;
        _storageService.SaveSettings(AppSettings);

        if (CurrentStatus == TimerMode.Idle)
        {
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        }
    }

    private void SaveNotesToLastSession()
    {
        SessionScoringController.SaveNotes(_storageService, _lastCompletedSessionId, CurrentNotes);
        CurrentNotes = string.Empty;
        _lastCompletedSessionId = null;
    }

    public void SetRating(int stars)
    {
        UserRating = stars;
        SessionScoringController.SaveRating(_storageService, _lastCompletedSessionId, stars);
    }

    private void RefreshStreak()
    {
        var sessions = _storageService.LoadSessions()
            .Where(s => s.Completed && s.EndTime.HasValue)
            .ToList();
        StreakDays = SessionScoringController.CalculateStreak(sessions);
        ShowStreakEncouragement = SessionScoringController.ShouldShowStreakEncouragement(sessions);
    }

    public DailyReport? GetYesterdayReport()
    {
        return SessionScoringController.GetYesterdayReport(_storageService);
    }

    public void RefreshStats()
    {
        TodayStats = _storageService.GetTodayStats();
    }

    public void RefreshTimerOnWake()
    {
        _timerController.CorrectAfterWake();
        RefreshStats();
    }

    public void AdjustWorkMinutes(int delta)
    {
        _timerController.AdjustWorkMinutes(delta, AppSettings);
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

        OnPropertyChanged(nameof(TodayPomodoroProgress));
        OnPropertyChanged(nameof(TodayPomodoroBarWidth));
        OnPropertyChanged(nameof(ExamCountdown));
        OnPropertyChanged(nameof(DaysUntilExam));
    }

    public void ReloadSettings()
    {
        AppSettings = _storageService.LoadSettings();

        if (CurrentStatus == TimerMode.Idle)
        {
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        }

        OnPropertyChanged(nameof(TodayPomodoroProgress));
        OnPropertyChanged(nameof(TodayPomodoroBarWidth));
        OnPropertyChanged(nameof(ExamCountdown));
        OnPropertyChanged(nameof(DaysUntilExam));

        _cameraAlert.Initialize(AppSettings);
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

    private static string FormatTime(int seconds)
    {
        int mins = seconds / 60;
        int secs = seconds % 60;
        return $"{mins:D2}:{secs:D2}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 保存未完成的 session
        var abandonedSession = _timerController.AbandonIncompleteSession();
        if (abandonedSession != null)
        {
            _storageService.AddSession(abandonedSession);
        }

        // 显式取消 CameraAlertController 事件订阅
        _cameraAlert.StatusChanged -= OnCameraStatusChanged;
        _cameraAlert.ErrorOccurred -= HandleCameraError;
        _cameraAlert.SystemNotificationRequested -= OnCameraSystemNotification;
        _cameraAlert.WindowActivationRequested -= OnCameraWindowActivation;

        // 解除 FocusGuardService 事件订阅并停止监控
        _focusGuard.DistractionDetected -= OnFocusGuardDistraction;
        _focusGuard.FocusRegained -= OnFocusGuardRegained;
        _focusGuard.Stop();

        // 解除 TimerController 事件代理
        _timerController.TickUpdated -= OnTimerTickUpdated;
        _timerController.FocusCompleted -= OnFocusCompleted;
        _timerController.BreakCompleted -= OnBreakCompleted;
        _timerController.ModeChanged -= mode => CurrentStatus = mode;

        _timerController.Dispose();
        _notifications.Dispose();

        CameraAlertController.FireAndForgetAsync(_cameraAlert.StopCameraAsync(), "Dispose 停止摄像头");
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
