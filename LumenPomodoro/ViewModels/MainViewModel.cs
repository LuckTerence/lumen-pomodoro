using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly TimerController _timerController;
    private readonly NotificationCoordinator _notifications;
    private readonly CameraAlertController _cameraAlert;
    private readonly IFocusGuardService _focusGuard;
    private string? _lastCompletedSessionId;
    private bool _disposed;
    /// <summary>本轮计时是否已发过结束前预告。</summary>
    private bool _sessionEndPreNotifySent;

    // ── Bindable Properties ──

    [ObservableProperty]
    private TimerMode _currentStatus = TimerMode.Idle;

    [ObservableProperty]
    private string _remainingTime = "25:00";

    [ObservableProperty]
    private int _progress = 100;

    [ObservableProperty]
    private TaskItem? _selectedTask;

    [ObservableProperty]
    private List<TaskItem> _tasks = new();

    [ObservableProperty]
    private DailyStats _todayStats = new();

    [ObservableProperty]
    private bool _isFocusCompleted;

    [ObservableProperty]
    private bool _isBreakCompleted;

    [ObservableProperty]
    private bool _isPendingBreak;

    [ObservableProperty]
    private bool _isWindowActive;

    [ObservableProperty]
    private string _currentNotes = string.Empty;

    [ObservableProperty]
    private int _userRating;

    [ObservableProperty]
    private string _ratingStars = string.Empty;

    [ObservableProperty]
    private string _lastCompletedTaskName = string.Empty;

    [ObservableProperty]
    private int _lastCompletedFocusMinutes;

    [ObservableProperty]
    private int _streakDays;

    [ObservableProperty]
    private bool _showStreakEncouragement;

    [ObservableProperty]
    private bool _suggestLongBreak;

    [ObservableProperty]
    private int _todayCompletedCount;

    [ObservableProperty]
    private bool _isWindowTopmost;

    // ── Computed / Forwarded Properties ──

    public string CameraStatus => _cameraAlert.Status;
    public bool IsCameraAlertActive => _cameraAlert.IsActive;

    /// <summary>计时页展示的可读摄像头状态（含关闭/待命/异常）。</summary>
    public string CameraStatusDisplay => Models.CameraAlertStatusText.Describe(
        AppSettings.CameraAlertEnabled,
        IsCameraAlertActive,
        CameraStatus,
        AppSettings.EffectiveCameraAlertCanManualClose);
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

    /// <summary>专注、休息或暂停中，关闭应用前宜二次确认。</summary>
    public bool IsSessionActive =>
        CurrentStatus is TimerMode.Focus or TimerMode.Break or TimerMode.Paused;

    /// <summary>
    /// 若正在计时且开启确认，弹出对话框；返回 true 表示允许退出。
    /// </summary>
    public bool ConfirmExitIfNeeded(Window? owner = null)
    {
        if (!AppSettings.ConfirmExitWhileFocusing || !IsSessionActive)
            return true;

        var result = MessageBox.Show(
            owner,
            Properties.LocalizedStrings.ConfirmExitWhileFocusing_Message,
            Properties.LocalizedStrings.ConfirmExitWhileFocusing_Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
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

    // ── Side-effect hooks for [ObservableProperty] ──

    partial void OnTodayStatsChanged(DailyStats value)
    {
        OnPropertyChanged(nameof(TodayPomodoroProgress));
        OnPropertyChanged(nameof(TodayPomodoroBarWidth));
    }

    partial void OnUserRatingChanged(int value)
    {
        RatingStars = SessionScoringController.GetRatingStars(value);
    }

    partial void OnLastCompletedTaskNameChanged(string value)
    {
        OnPropertyChanged(nameof(LastCompletedSummary));
    }

    partial void OnLastCompletedFocusMinutesChanged(int value)
    {
        OnPropertyChanged(nameof(LastCompletedSummary));
    }

    // ── Events (View 订阅) ──

    public event Action? TrayMenuNeedsUpdate;
    public event Action<string, string>? NotificationRequested;
    public event Action<string, string>? InAppNotificationRequested;
    public event Action<string>? CountdownStartRequested;
    public event Action<string>? CountdownUpdateRequested;
    public event Action? CountdownStopRequested;

    internal const double TopmostDurationSeconds = 3;

    // ── Constructor ──

    public MainViewModel(IStorageService storageService, ITimerService timerService,
        ICameraService cameraService, ISoundService soundService,
        IFocusGuardService? focusGuard = null)
    {
        _storageService = storageService;
        CameraService = cameraService;
        _focusGuard = focusGuard ?? new FocusGuardService();

        _timerController = new TimerController(timerService);
        _notifications = new NotificationCoordinator(soundService);
        _cameraAlert = new CameraAlertController(cameraService);

        WireEvents();
        LoadData();

        _cameraAlert.Initialize(AppSettings);

        if (AppSettings.TrayEnabled)
            _notifications.StartTrayTimer();

        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        CurrentStatus = TimerMode.Idle;
        Progress = 100;
    }

    private void WireEvents()
    {
        // NotificationCoordinator → MainViewModel events（命名方法便于 Dispose 正确解绑）
        _notifications.TrayMenuNeedsUpdate += OnTrayMenuNeedsUpdate;
        _notifications.NotificationRequested += OnNotificationRequested;
        _notifications.InAppNotificationRequested += OnInAppNotificationRequested;
        _notifications.CountdownStartRequested += OnCountdownStartRequested;
        _notifications.CountdownUpdateRequested += OnCountdownUpdateRequested;
        _notifications.CountdownStopRequested += OnCountdownStopRequested;

        // TimerController → handlers
        _timerController.TickUpdated += OnTimerTickUpdated;
        _timerController.FocusCompleted += OnFocusCompleted;
        _timerController.BreakCompleted += OnBreakCompleted;
        _timerController.ModeChanged += OnTimerModeChanged;

        // CameraAlertController → handlers
        _cameraAlert.StatusChanged += OnCameraStatusChanged;
        _cameraAlert.ErrorOccurred += HandleCameraError;
        _cameraAlert.SystemNotificationRequested += OnCameraSystemNotification;
        _cameraAlert.WindowActivationRequested += OnWindowActivation;

        // FocusGuard
        _focusGuard.DistractionDetected += OnFocusGuardDistraction;
        _focusGuard.FocusRegained += OnFocusGuardRegained;
    }

    private void OnTrayMenuNeedsUpdate() => TrayMenuNeedsUpdate?.Invoke();
    private void OnNotificationRequested(string t, string m) => NotificationRequested?.Invoke(t, m);
    private void OnInAppNotificationRequested(string t, string m) => InAppNotificationRequested?.Invoke(t, m);
    private void OnCountdownStartRequested(string m) => CountdownStartRequested?.Invoke(m);
    private void OnCountdownUpdateRequested(string m) => CountdownUpdateRequested?.Invoke(m);
    private void OnCountdownStopRequested() => CountdownStopRequested?.Invoke();
    private void OnTimerModeChanged(TimerMode mode) => CurrentStatus = mode;

    private void OnCameraStatusChanged(string _)
    {
        OnPropertyChanged(nameof(CameraStatus));
        OnPropertyChanged(nameof(IsCameraAlertActive));
        OnPropertyChanged(nameof(CameraStatusDisplay));
    }

    private void OnCameraSystemNotification(string t, string m) =>
        _notifications.ShowSystem(t, m, AppSettings.SystemNotificationEnabled, IsWindowActive);

    // ── RelayCommands（替代 View 代码后置中的方法调用） ──

    [RelayCommand]
    public void StartFocus()
    {
        CurrentNotes = string.Empty;
        UserRating = 0;
        _lastCompletedSessionId = null;
        SuggestLongBreak = false;

        if (SelectedTask == null)
        {
            MessageBox.Show(Properties.LocalizedStrings.NoTaskSelected, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _timerController.StartFocus(SelectedTask, AppSettings);
        _storageService.SaveSettings(AppSettings);
        _sessionEndPreNotifySent = false;

        // 告警计数/防抖在 FocusGuardService 会话内管理（Start 时 ResetSession）
        _focusGuard.Start(AppSettings);

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        _notifications.StartCountdown(
            string.Format(Properties.LocalizedStrings.Focus_Start, SelectedTask.Name),
            IsWindowTopmost, AppSettings.DynamicIslandEnabled);
    }

    [RelayCommand]
    private void Pause()
    {
        _timerController.PauseFocus();
        _focusGuard.Stop();
    }

    [RelayCommand]
    private void Resume()
    {
        _timerController.ResumeFocus();
        // 暂停后恢复：保留本会话告警计数
        _focusGuard.Start(AppSettings, resetSessionCounters: false);
    }

    [RelayCommand]
    private void Reset()
    {
        _focusGuard.Stop();
        _cameraAlert.ForceStop();
        _timerController.ResetFocus();
        _sessionEndPreNotifySent = false;
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        SuggestLongBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        _notifications.StopCountdown();
        FullscreenBreakHideRequested?.Invoke();
    }

    /// <summary>严格模式下禁止提前结束休息。</summary>
    private bool TryAllowEndBreakEarly()
    {
        if (AppSettings.EffectiveAllowEndBreakEarly) return true;
        if (CurrentStatus != TimerMode.Break) return true;

        MessageBox.Show(
            Properties.LocalizedStrings.StrictMode_EndBreakBlocked,
            "严格模式",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    [RelayCommand]
    private void StartShortBreak() => StartBreakCore(isLong: false);

    [RelayCommand]
    private void StartLongBreak() => StartBreakCore(isLong: true);

    /// <summary>保留公共 API 以兼容 View 代码后置和 TimerPage。</summary>
    public void StartBreak(bool isLongBreak = false) => StartBreakCore(isLong: isLongBreak);

    // 保留旧方法名作为公共 API 以兼容 TimerPage.xaml.cs
    public void PauseFocus() => Pause();
    public void ResumeFocus() => Resume();
    public void ResetFocus() => Reset();

    private void StartBreakCore(bool isLong)
    {
        SaveNotesToLastSession();
        _focusGuard.Stop();
        _sessionEndPreNotifySent = false;

        if (AppSettings.CameraAlertMode == CameraAlertMode.UntilConfirm && _cameraAlert.IsActive)
            _cameraAlert.ForceStop();

        _timerController.StartBreak(isLong, AppSettings);
        _cameraAlert.StartForBreak(AppSettings);

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        var breakMinutes = isLong ? AppSettings.LongBreakMinutes : AppSettings.ShortBreakMinutes;
        RemainingTime = FormatTime(breakMinutes * 60);
        Progress = 100;

        var breakTitle = isLong
            ? Properties.LocalizedStrings.FullscreenBreak_Long
            : Properties.LocalizedStrings.FullscreenBreak_Short;
        _notifications.StartCountdown(breakTitle, IsWindowTopmost, AppSettings.DynamicIslandEnabled);

        if (AppSettings.FullscreenBreakEnabled)
        {
            FullscreenBreakShowRequested?.Invoke(
                breakTitle,
                RemainingTime,
                AppSettings.EffectiveAllowEndBreakEarly);
        }
    }

    [RelayCommand]
    public void EndBreak()
    {
        if (!TryAllowEndBreakEarly()) return;

        _cameraAlert.ForceStop();
        _timerController.EndBreak();
        IsBreakCompleted = true;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        _notifications.StopCountdown();
        FullscreenBreakHideRequested?.Invoke();
    }

    [RelayCommand]
    public void SkipBreak()
    {
        if (!TryAllowEndBreakEarly()) return;

        SaveNotesToLastSession();

        _cameraAlert.ForceStop();
        _timerController.SkipBreak();
        IsBreakCompleted = false;
        IsFocusCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        FullscreenBreakHideRequested?.Invoke();
        _notifications.StopCountdown();
    }

    public void StopCameraAlert()
    {
        _cameraAlert.TryStop(AppSettings);
    }

    [RelayCommand]
    public void ApplyPreset(PomodoroPreset preset)
    {
        if (preset.WorkMinutes <= 0) return;

        AppSettings.WorkMinutes = preset.WorkMinutes;
        AppSettings.ShortBreakMinutes = preset.ShortBreakMinutes;
        AppSettings.LongBreakMinutes = preset.LongBreakMinutes;
        AppSettings.LongBreakInterval = preset.LongBreakInterval;
        _storageService.SaveSettings(AppSettings);

        if (CurrentStatus == TimerMode.Idle)
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
    }

    public void SetRating(int stars)
    {
        UserRating = stars;
        SessionScoringController.SaveRating(_storageService, _lastCompletedSessionId, stars);
    }

    [RelayCommand]
    public void AdjustWorkMinutes(int delta)
    {
        _timerController.AdjustWorkMinutes(delta, AppSettings);
        _storageService.SaveSettings(AppSettings);

        if (CurrentStatus == TimerMode.Idle)
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
    }

    /// <summary>全屏休息：title, remainingTime, allowEndEarly</summary>
    public event Action<string, string, bool>? FullscreenBreakShowRequested;
    public event Action<string>? FullscreenBreakUpdateRequested;
    public event Action? FullscreenBreakHideRequested;

    // ── Timer Handlers ──

    private void OnTimerTickUpdated(int remainingSeconds, int totalSeconds)
    {
        RemainingTime = FormatTime(remainingSeconds);
        if (totalSeconds > 0)
            Progress = (int)((double)remainingSeconds / totalSeconds * 100);

        _notifications.UpdateCountdown(RemainingTime, IsWindowTopmost, AppSettings.DynamicIslandEnabled);
        if (CurrentStatus == TimerMode.Break && AppSettings.FullscreenBreakEnabled)
            FullscreenBreakUpdateRequested?.Invoke(RemainingTime);
        MaybeSendSessionEndPreNotify(remainingSeconds);
    }

    private void MaybeSendSessionEndPreNotify(int remainingSeconds)
    {
        var threshold = AppSettings.SessionEndPreNotifySeconds;
        if (threshold <= 0 || _sessionEndPreNotifySent) return;
        // 仅在专注/休息倒计时中预告（暂停不报）
        if (CurrentStatus is not (TimerMode.Focus or TimerMode.Break)) return;
        if (remainingSeconds > threshold || remainingSeconds <= 0) return;

        _sessionEndPreNotifySent = true;
        var title = Properties.LocalizedStrings.SessionEndSoon_Title;
        var message = string.Format(Properties.LocalizedStrings.SessionEndSoon_Message, remainingSeconds);
        // 预告应尽量送达：窗口在前台时也弹应用内提示；系统通知在后台
        _notifications.ShowInApp(title, message, AppSettings.PopupEnabled);
        _notifications.ShowSystem(title, message, AppSettings.SystemNotificationEnabled, isWindowActive: false);
        if (AppSettings.SoundEnabled)
            _notifications.PlaySound("FocusComplete", soundEnabled: true);
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
        _notifications.ShowSystem(Properties.LocalizedStrings.Break_Complete, Properties.LocalizedStrings.Break_Ready,
            AppSettings.SystemNotificationEnabled, IsWindowActive);
        _notifications.StopCountdown();
        FullscreenBreakHideRequested?.Invoke();
    }

    // ── Focus Guard ──

    private void OnFocusGuardDistraction(string reason)
    {
        // 次数上限已在 FocusGuardEngine 内执行；此处只做 UI 反馈
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        dispatcher.BeginInvoke(() =>
        {
            var level = AppSettings.FocusGuardAlertLevel;
            _notifications.ShowSystem(Properties.LocalizedStrings.DistractionAlert, reason, systemNotificationEnabled: true, IsWindowActive);

            if (level >= CameraAlertLevel.Medium)
                _notifications.PlaySound("FocusComplete", soundEnabled: true);

            if (level == CameraAlertLevel.Severe &&
                Application.Current?.MainWindow is Window mainWindow)
                OnWindowActivation(mainWindow);
        });
    }

    private void OnFocusGuardRegained()
    {
        // 有意留空：恢复专注不重置告警计数（防反复进出刷通知）
    }

    // ── Window Activation ──

    private static void OnWindowActivation(Window window)
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
        _notifications.ShowSystem(Properties.LocalizedStrings.CameraError, error, AppSettings.SystemNotificationEnabled, IsWindowActive);
        OnPropertyChanged(nameof(CameraStatusDisplay));

        if (error.Contains(Properties.LocalizedStrings.CameraProtectedRelease))
            IsFocusCompleted = true;

        // 始终给出可操作诊断（不仅依赖 Popup 开关）
        var likelyPermission = LooksLikeCameraPermissionError(error);
        var owner = Application.Current?.MainWindow;
        var result = MessageBox.Show(
            owner,
            $"{error}\n\n" +
            (likelyPermission
                ? "这通常是系统未允许本应用使用摄像头。是否打开 Windows「摄像头隐私」设置？"
                : "若指示灯未亮，常见原因是摄像头被占用或权限关闭。是否打开 Windows「摄像头隐私」设置排查？"),
            Properties.LocalizedStrings.CameraErrorTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Yes)
            OpenWindowsCameraPrivacySettings();
    }

    /// <summary>打开系统摄像头隐私页（供错误诊断与设置页调用）。</summary>
    public static void OpenWindowsCameraPrivacySettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:privacy-webcam",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "打开摄像头隐私设置失败");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:privacy",
                    UseShellExecute = true
                });
            }
            catch (Exception ex2)
            {
                Log.Warning(ex2, "打开隐私设置失败");
            }
        }
    }

    private static bool LooksLikeCameraPermissionError(string error)
    {
        if (string.IsNullOrEmpty(error)) return false;
        return error.Contains("权限", StringComparison.OrdinalIgnoreCase)
               || error.Contains("denied", StringComparison.OrdinalIgnoreCase)
               || error.Contains("access", StringComparison.OrdinalIgnoreCase)
               || error.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
               || error.Contains("不可用", StringComparison.Ordinal)
               || error.Contains("失败", StringComparison.Ordinal)
               || error.Contains("占用", StringComparison.Ordinal)
               || error.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    // ── Data & Streak ──

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

    private void RefreshStreak()
    {
        var sessions = _storageService.LoadSessions()
            .Where(s => s.Completed && s.EndTime.HasValue)
            .ToList();
        StreakDays = SessionScoringController.CalculateStreak(sessions);
        ShowStreakEncouragement = SessionScoringController.ShouldShowStreakEncouragement(sessions);
    }

    private void SaveNotesToLastSession()
    {
        SessionScoringController.SaveNotes(_storageService, _lastCompletedSessionId, CurrentNotes);
        CurrentNotes = string.Empty;
        _lastCompletedSessionId = null;
    }

    public DailyReport? GetYesterdayReport() => SessionScoringController.GetYesterdayReport(_storageService);
    public void RefreshStats() => TodayStats = _storageService.GetTodayStats();

    public void RefreshTimerOnWake()
    {
        _timerController.CorrectAfterWake();
        RefreshStats();
    }

    public void UpdateSettings(Settings settings)
    {
        AppSettings = settings;

        if (CurrentStatus == TimerMode.Idle)
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);

        OnPropertyChanged(nameof(TodayPomodoroProgress));
        OnPropertyChanged(nameof(TodayPomodoroBarWidth));
        OnPropertyChanged(nameof(ExamCountdown));
        OnPropertyChanged(nameof(DaysUntilExam));
    }

    public void ReloadSettings()
    {
        AppSettings = _storageService.LoadSettings();

        if (CurrentStatus == TimerMode.Idle)
            RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);

        OnPropertyChanged(nameof(TodayPomodoroProgress));
        OnPropertyChanged(nameof(TodayPomodoroBarWidth));
        OnPropertyChanged(nameof(ExamCountdown));
        OnPropertyChanged(nameof(DaysUntilExam));

        _cameraAlert.Initialize(AppSettings);
        OnPropertyChanged(nameof(CameraStatusDisplay));
        OnPropertyChanged(nameof(IsInsightsEnabled));
        OnPropertyChanged(nameof(IsDailyReportEnabled));
        OnPropertyChanged(nameof(IsDynamicIslandEnabled));
    }

    public void UpdateTasks(List<TaskItem> tasks)
    {
        Tasks = tasks;
        if (SelectedTask == null || !Tasks.Any(t => t.Id == SelectedTask.Id))
            SelectedTask = Tasks.Any() ? Tasks.First() : null;
    }

    public void ReloadTasks()
    {
        Tasks = _storageService.LoadTasks();
        if (SelectedTask == null || !Tasks.Any(t => t.Id == SelectedTask.Id))
            SelectedTask = Tasks.Any() ? Tasks.First() : null;
    }

    // ── Formatting ──

    private static string FormatTime(int seconds)
    {
        int mins = seconds / 60;
        int secs = seconds % 60;
        return $"{mins:D2}:{secs:D2}";
    }

    // ── Dispose ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var abandonedSession = _timerController.AbandonIncompleteSession();
        if (abandonedSession != null)
            _storageService.AddSession(abandonedSession);

        _notifications.TrayMenuNeedsUpdate -= OnTrayMenuNeedsUpdate;
        _notifications.NotificationRequested -= OnNotificationRequested;
        _notifications.InAppNotificationRequested -= OnInAppNotificationRequested;
        _notifications.CountdownStartRequested -= OnCountdownStartRequested;
        _notifications.CountdownUpdateRequested -= OnCountdownUpdateRequested;
        _notifications.CountdownStopRequested -= OnCountdownStopRequested;

        _cameraAlert.StatusChanged -= OnCameraStatusChanged;
        _cameraAlert.ErrorOccurred -= HandleCameraError;
        _cameraAlert.SystemNotificationRequested -= OnCameraSystemNotification;
        _cameraAlert.WindowActivationRequested -= OnWindowActivation;

        _focusGuard.DistractionDetected -= OnFocusGuardDistraction;
        _focusGuard.FocusRegained -= OnFocusGuardRegained;
        _focusGuard.Stop();

        _timerController.TickUpdated -= OnTimerTickUpdated;
        _timerController.FocusCompleted -= OnFocusCompleted;
        _timerController.BreakCompleted -= OnBreakCompleted;
        _timerController.ModeChanged -= OnTimerModeChanged;

        _timerController.Dispose();
        _notifications.Dispose();

        _ = CameraAlertController.FireAndForgetAsync(_cameraAlert.StopCameraAsync(), "Dispose 停止摄像头");
    }
}
