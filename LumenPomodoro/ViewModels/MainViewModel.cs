using System.ComponentModel;
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
    private readonly ITimerService _timerService;
    private readonly IStorageService _storageService;
    private readonly ISoundService _soundService;
    private readonly CameraAlertController _cameraAlert;

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

    // 考试倒计时
    public bool ExamCountdown => AppSettings.ExamCountdownEnabled && AppSettings.ExamDate.HasValue && AppSettings.ExamDate.Value > DateTime.Today;
    public string ExamName => AppSettings.ExamName;
    public int DaysUntilExam => ExamCountdown ? (AppSettings.ExamDate!.Value - DateTime.Today).Days : 0;

    public int LongBreakInterval => AppSettings.LongBreakInterval;
    public bool IsInsightsEnabled => AppSettings.InsightsEnabled;
    public bool IsDailyReportEnabled => AppSettings.DailyReportEnabled;
    public bool IsDynamicIslandEnabled => AppSettings.DynamicIslandEnabled;

    public ICameraService CameraService { get; }
    public IStorageService StorageService => _storageService;

    private FocusSession? _currentSession;
    private string? _lastCompletedSessionId;
    private bool _disposed;
    private DispatcherTimer? _trayUpdateTimer;

    // 专注笔记
    private string _currentNotes = string.Empty;
    public string CurrentNotes
    {
        get => _currentNotes;
        set { if (_currentNotes != value) { _currentNotes = value; OnPropertyChanged(); } }
    }

    // 手动评分 (1-5 星)
    private int _userRating;
    public int UserRating
    {
        get => _userRating;
        set { if (_userRating != value) { _userRating = value; OnPropertyChanged(); OnPropertyChanged(nameof(RatingStars)); } }
    }
    public string RatingStars => UserRating > 0 ? string.Concat(Enumerable.Repeat("★", UserRating)) + string.Concat(Enumerable.Repeat("☆", 5 - UserRating)) : string.Empty;

    // 最近完成的专注摘要
    private string _lastCompletedTaskName = string.Empty;
    public string LastCompletedTaskName
    {
        get => _lastCompletedTaskName;
        set { if (_lastCompletedTaskName != value) { _lastCompletedTaskName = value; OnPropertyChanged(); } }
    }

    private int _lastCompletedFocusMinutes;
    public int LastCompletedFocusMinutes
    {
        get => _lastCompletedFocusMinutes;
        set { if (_lastCompletedFocusMinutes != value) { _lastCompletedFocusMinutes = value; OnPropertyChanged(); } }
    }

    public string LastCompletedSummary =>
        string.IsNullOrEmpty(LastCompletedTaskName)
            ? string.Empty
            : $"刚刚完成：{LastCompletedTaskName} · {LastCompletedFocusMinutes} 分钟";

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
        _soundService = soundService;
        CameraService = cameraService;

        _cameraAlert = new CameraAlertController(cameraService);
        _cameraAlert.StatusChanged += _ =>
        {
            OnPropertyChanged(nameof(CameraStatus));
            OnPropertyChanged(nameof(IsCameraAlertActive));
        };
        _cameraAlert.ErrorOccurred += HandleCameraError;
        _cameraAlert.SystemNotificationRequested += (title, msg) =>
        {
            if (AppSettings.SystemNotificationEnabled && !IsWindowActive)
                NotificationRequested?.Invoke(title, msg);
        };
        _cameraAlert.WindowActivationRequested += window =>
        {
            window.Activate();
            window.Topmost = true;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) => { window.Topmost = false; ((DispatcherTimer)s!).Stop(); };
            timer.Start();
        };

        _timerService.TimerTick += TimerService_TimerTick;
        _timerService.TimerCompleted += TimerService_TimerCompleted;
        _timerService.ModeChanged += TimerService_ModeChanged;

        LoadData();

        _cameraAlert.Initialize(AppSettings);

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
                ? Tasks.FirstOrDefault(t => t.Id == lastId) ?? Tasks.FirstOrDefault()
                : Tasks.FirstOrDefault();
        }

        RefreshStreak();
    }

    private void TimerService_TimerTick(object? sender, TimerTickEventArgs e)
    {
        RemainingTime = FormatTime(e.RemainingSeconds);

        if (e.TotalSeconds > 0)
        {
            Progress = (int)((double)e.RemainingSeconds / e.TotalSeconds * 100);
        }

        if (!IsWindowTopmost && AppSettings.DynamicIslandEnabled)
        {
            CountdownUpdateRequested?.Invoke(RemainingTime);
        }
    }

    private void TimerService_TimerCompleted(object? sender, TimerCompletedEventArgs e)
    {
        if (e.CompletedMode == TimerMode.Focus)
        {
            if (_currentSession != null && !_currentSession.Completed)
            {
                _currentSession.EndTime = DateTime.Now;
                _currentSession.Completed = true;

                UserRating = 0;
                LastCompletedTaskName = _currentSession.TaskName;
                LastCompletedFocusMinutes = _currentSession.FocusMinutes;
                _storageService.AddSession(_currentSession);
                _lastCompletedSessionId = _currentSession.Id;
                TodayStats = _storageService.GetTodayStats();
                _currentSession = null;
            }

            Progress = 0;
            IsFocusCompleted = true;
            IsPendingBreak = true;

            var todayCount = TodayStats.CompletedPomodoros;
            TodayCompletedCount = todayCount;
            SuggestLongBreak = todayCount > 0 && AppSettings.LongBreakInterval > 0
                && todayCount % AppSettings.LongBreakInterval == 0;

            _cameraAlert.Start(AppSettings);
            _storageService.SaveSettings(AppSettings);
            PlayNotificationSound("FocusComplete");
            ShowInAppNotification("专注完成", "该休息了！");
            CheckMilestones();
            RefreshStreak();
            CountdownStopRequested?.Invoke();
        }
        else if (e.CompletedMode == TimerMode.Break)
        {
            IsBreakCompleted = true;
            _cameraAlert.ForceStop();
            PlayNotificationSound("BreakComplete");
            ShowSystemNotification("休息完成！", "准备好开始下一轮了吗？");
            CountdownStopRequested?.Invoke();
        }
    }

    private void TimerService_ModeChanged(object? sender, TimerModeChangedEventArgs e)
    {
        CurrentStatus = e.NewMode;
    }

    private void HandleCameraError(string error)
    {
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
    }

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

        if (!IsWindowTopmost && AppSettings.DynamicIslandEnabled)
        {
            CountdownStartRequested?.Invoke($"专注 · {SelectedTask.Name}");
        }
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
        _cameraAlert.ForceStop();
        _timerService.Reset();
        _currentSession = null;
        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;
        SuggestLongBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        CountdownStopRequested?.Invoke();
    }

    public void StartBreak(bool isLongBreak = false)
    {
        SaveNotesToLastSession();

        if (AppSettings.CameraAlertMode == CameraAlertMode.UntilConfirm && _cameraAlert.IsActive)
        {
            _cameraAlert.ForceStop();
        }

        int breakMinutes = isLongBreak ? AppSettings.LongBreakMinutes : AppSettings.ShortBreakMinutes;
        _timerService.StartBreak(breakMinutes);

        _cameraAlert.StartForBreak(AppSettings);

        IsFocusCompleted = false;
        IsBreakCompleted = false;
        IsPendingBreak = false;

        _currentSession = null;

        if (!IsWindowTopmost && AppSettings.DynamicIslandEnabled)
        {
            var breakType = isLongBreak ? "长休息" : "短休息";
            CountdownStartRequested?.Invoke(breakType);
        }
    }

    public void EndBreak()
    {
        _cameraAlert.ForceStop();
        _timerService.Reset();
        IsBreakCompleted = true;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        CountdownStopRequested?.Invoke();
    }

    public void SkipBreak()
    {
        SaveNotesToLastSession();

        _cameraAlert.ForceStop();
        _timerService.Reset();
        IsBreakCompleted = false;
        IsFocusCompleted = false;
        IsPendingBreak = false;
        RemainingTime = FormatTime(AppSettings.WorkMinutes * 60);
        Progress = 100;
        CountdownStopRequested?.Invoke();
    }

    public void StopCameraAlert()
    {
        _cameraAlert.TryStop(AppSettings);
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

    public void SetRating(int stars)
    {
        UserRating = stars;
        if (_lastCompletedSessionId != null)
        {
            _storageService.UpdateSession(_lastCompletedSessionId, session =>
            {
                session.QualityScore = stars;
            });
        }
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
        // 一次加载所有已完成 sessions，避免 CalculateStreak() 再调一次 LoadSessions()
        var allCompleted = _storageService.LoadSessions()
            .Where(s => s.Completed && s.EndTime.HasValue)
            .ToList();
        var sessions = allCompleted
            .Where(s => s.EndTime!.Value.Date == yesterday)
            .ToList();

        if (!sessions.Any()) return null;

        var mainTask = sessions.GroupBy(s => s.TaskName)
            .OrderByDescending(g => g.Sum(s => s.FocusMinutes))
            .FirstOrDefault()?.Key ?? "未分类";

        var uniqueTasks = sessions.Select(s => s.TaskName).Distinct().Count();
        var avgQuality = sessions
            .Where(s => s.QualityScore > 0)
            .Select(s => (double)s.QualityScore)
            .DefaultIfEmpty(0)
            .Average();

        var categorySuggestion = "";
        var allTasks = _storageService.LoadTasks();
        var yesterdayCategories = sessions
            .Join(allTasks, s => s.TaskId, t => t.Id,
                (s, t) => string.IsNullOrEmpty(t.Category) ? t.Name : t.Category)
            .Distinct()
            .ToHashSet();
        var allCategories = allTasks
            .Select(t => string.IsNullOrEmpty(t.Category) ? t.Name : t.Category)
            .Distinct()
            .ToList();
        var missed = allCategories.Where(c => !yesterdayCategories.Contains(c)).ToList();
        if (missed.Count > 0 && missed.Count <= 3)
            categorySuggestion = $"昨天没有学习「{string.Join("」「", missed)}」，今天可以补上进度";

        return new DailyReport
        {
            Date = yesterday,
            CompletedPomodoros = sessions.Count,
            TotalMinutes = sessions.Sum(s => s.FocusMinutes),
            MainTask = mainTask,
            StreakDays = InsightEngine.CalculateStreak(allCompleted),
            AvgQualityScore = Math.Round(avgQuality, 1),
            UniqueTasksCount = uniqueTasks,
            CategorySuggestion = categorySuggestion
        };
    }

    private void PlayNotificationSound(string soundName)
    {
        if (!AppSettings.SoundEnabled) return;

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

    private void ShowSystemNotification(string title, string message)
    {
        if (!AppSettings.SystemNotificationEnabled) return;
        if (IsWindowActive) return;

        NotificationRequested?.Invoke(title, message);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_currentSession != null && !_currentSession.Completed)
        {
            _currentSession.EndTime = DateTime.Now;
            _currentSession.Completed = false;
            _storageService.AddSession(_currentSession);
            _currentSession = null;
        }

        // 取消订阅单例 TimerService 的事件
        _timerService.TimerTick -= TimerService_TimerTick;
        _timerService.TimerCompleted -= TimerService_TimerCompleted;
        _timerService.ModeChanged -= TimerService_ModeChanged;

        _trayUpdateTimer?.Stop();

        CameraAlertController.FireAndForgetAsync(_cameraAlert.StopCameraAsync(), "Dispose 停止摄像头");

        // 注意：_timerService 和 _soundService 是单例，由 DI 容器管理生命周期，不在此 Dispose
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
