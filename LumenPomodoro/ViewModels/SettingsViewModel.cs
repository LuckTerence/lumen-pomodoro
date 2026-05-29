using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly ICameraService _cameraService;

    private int _workMinutes;
    private int _shortBreakMinutes;
    private int _longBreakMinutes;
    private int _longBreakInterval;

    private bool _cameraAlertEnabled;
    private CameraAlertMode _cameraAlertMode;
    private int _cameraFixedOnSeconds;
    private bool _cameraFollowBreakEnabled;
    private int _selectedCameraIndex;
    private bool _cameraAlertCanManualClose;
    private CameraAlertLevel _cameraAlertLevel = CameraAlertLevel.Medium;
    private bool _presenceDetectionEnabled = true;
    private int _presenceDetectionSeconds = 5;
    private ObservableCollection<string> _availableCameras;

    private bool _focusGuardEnabled = true;
    private int _focusGuardIdleSeconds = 180;
    private string _focusGuardBlocklistText = string.Empty;
    private CameraAlertLevel _focusGuardAlertLevel = CameraAlertLevel.Medium;

    private bool _soundEnabled;
    private bool _popupEnabled;
    private bool _systemNotificationEnabled;

    private bool _trayEnabled;
    private bool _closeToTray;
    private bool _autoStartEnabled;

    private string _theme = "system";
    private bool _animationEnabled;
    private int _dailyGoalMinutes = 120;
    private int _weeklyGoalMinutes = 600;
    private int _dailyTargetPomodoros = 8;
    private DateTime? _examDate;
    private string _examName = "考研";

    private bool _insightsEnabled = true;
    private bool _dailyReportEnabled = true;
    private bool _examCountdownEnabled = true;
    private bool _dynamicIslandEnabled = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int WorkMinutes
    {
        get => _workMinutes;
        set { var v = Math.Clamp(value, 1, 120); if (_workMinutes != v) { _workMinutes = v; OnPropertyChanged(); } }
    }

    public int ShortBreakMinutes
    {
        get => _shortBreakMinutes;
        set { var v = Math.Clamp(value, 1, 60); if (_shortBreakMinutes != v) { _shortBreakMinutes = v; OnPropertyChanged(); } }
    }

    public int LongBreakMinutes
    {
        get => _longBreakMinutes;
        set { var v = Math.Clamp(value, 1, 60); if (_longBreakMinutes != v) { _longBreakMinutes = v; OnPropertyChanged(); } }
    }

    public int LongBreakInterval
    {
        get => _longBreakInterval;
        set { var v = Math.Clamp(value, 2, 10); if (_longBreakInterval != v) { _longBreakInterval = v; OnPropertyChanged(); } }
    }

    public bool CameraAlertEnabled
    {
        get => _cameraAlertEnabled;
        set { if (_cameraAlertEnabled != value) { _cameraAlertEnabled = value; OnPropertyChanged(); } }
    }

    public CameraAlertMode CameraAlertMode
    {
        get => _cameraAlertMode;
        set { if (_cameraAlertMode != value) { _cameraAlertMode = value; OnPropertyChanged(); } }
    }

    public int CameraFixedOnSeconds
    {
        get => _cameraFixedOnSeconds;
        set { var v = Math.Clamp(value, 1, 300); if (_cameraFixedOnSeconds != v) { _cameraFixedOnSeconds = v; OnPropertyChanged(); } }
    }

    public bool CameraFollowBreakEnabled
    {
        get => _cameraFollowBreakEnabled;
        set { if (_cameraFollowBreakEnabled != value) { _cameraFollowBreakEnabled = value; OnPropertyChanged(); } }
    }

    public bool CameraAlertCanManualClose
    {
        get => _cameraAlertCanManualClose;
        set { if (_cameraAlertCanManualClose != value) { _cameraAlertCanManualClose = value; OnPropertyChanged(); } }
    }

    public CameraAlertLevel CameraAlertLevel
    {
        get => _cameraAlertLevel;
        set { if (_cameraAlertLevel != value) { _cameraAlertLevel = value; OnPropertyChanged(); } }
    }

    public int SelectedCameraIndex
    {
        get => _selectedCameraIndex;
        set { if (_selectedCameraIndex != value) { _selectedCameraIndex = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<string> AvailableCameras
    {
        get => _availableCameras;
        set { if (!ReferenceEquals(_availableCameras, value)) { _availableCameras = value; OnPropertyChanged(); } }
    }

    public bool SoundEnabled
    {
        get => _soundEnabled;
        set { if (_soundEnabled != value) { _soundEnabled = value; OnPropertyChanged(); } }
    }

    public bool PopupEnabled
    {
        get => _popupEnabled;
        set { if (_popupEnabled != value) { _popupEnabled = value; OnPropertyChanged(); } }
    }

    public bool SystemNotificationEnabled
    {
        get => _systemNotificationEnabled;
        set { if (_systemNotificationEnabled != value) { _systemNotificationEnabled = value; OnPropertyChanged(); } }
    }

    public bool TrayEnabled
    {
        get => _trayEnabled;
        set { if (_trayEnabled != value) { _trayEnabled = value; OnPropertyChanged(); } }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set { if (_closeToTray != value) { _closeToTray = value; OnPropertyChanged(); } }
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set { if (_autoStartEnabled != value) { _autoStartEnabled = value; OnPropertyChanged(); } }
    }

    public string Theme
    {
        get => _theme;
        set { if (_theme != value) { _theme = value; OnPropertyChanged(); } }
    }

    public bool AnimationEnabled
    {
        get => _animationEnabled;
        set { if (_animationEnabled != value) { _animationEnabled = value; OnPropertyChanged(); } }
    }

    public int DailyGoalMinutes
    {
        get => _dailyGoalMinutes;
        set { var v = Math.Clamp(value, 0, 1440); if (_dailyGoalMinutes != v) { _dailyGoalMinutes = v; OnPropertyChanged(); } }
    }

    public int WeeklyGoalMinutes
    {
        get => _weeklyGoalMinutes;
        set { var v = Math.Clamp(value, 0, 10080); if (_weeklyGoalMinutes != v) { _weeklyGoalMinutes = v; OnPropertyChanged(); } }
    }

    public int DailyTargetPomodoros
    {
        get => _dailyTargetPomodoros;
        set { var v = Math.Clamp(value, 0, 50); if (_dailyTargetPomodoros != v) { _dailyTargetPomodoros = v; OnPropertyChanged(); } }
    }

    public bool PresenceDetectionEnabled
    {
        get => _presenceDetectionEnabled;
        set { if (_presenceDetectionEnabled != value) { _presenceDetectionEnabled = value; OnPropertyChanged(); } }
    }

    public int PresenceDetectionSeconds
    {
        get => _presenceDetectionSeconds;
        set { var v = Math.Clamp(value, 3, 30); if (_presenceDetectionSeconds != v) { _presenceDetectionSeconds = v; OnPropertyChanged(); } }
    }

    public bool FocusGuardEnabled
    {
        get => _focusGuardEnabled;
        set { if (_focusGuardEnabled != value) { _focusGuardEnabled = value; OnPropertyChanged(); } }
    }

    public int FocusGuardIdleSeconds
    {
        get => _focusGuardIdleSeconds;
        set { var v = Math.Clamp(value, 10, 3600); if (_focusGuardIdleSeconds != v) { _focusGuardIdleSeconds = v; OnPropertyChanged(); } }
    }

    /// <summary>黑名单编辑文本，每行一个关键词（进程名或窗口标题片段）。</summary>
    public string FocusGuardBlocklistText
    {
        get => _focusGuardBlocklistText;
        set { if (_focusGuardBlocklistText != value) { _focusGuardBlocklistText = value; OnPropertyChanged(); } }
    }

    public CameraAlertLevel FocusGuardAlertLevel
    {
        get => _focusGuardAlertLevel;
        set { if (_focusGuardAlertLevel != value) { _focusGuardAlertLevel = value; OnPropertyChanged(); } }
    }

    public DateTime? ExamDate
    {
        get => _examDate;
        set { if (_examDate != value) { _examDate = value; OnPropertyChanged(); } }
    }

    public string ExamName
    {
        get => _examName;
        set { if (_examName != value) { _examName = value; OnPropertyChanged(); } }
    }

    public bool InsightsEnabled
    {
        get => _insightsEnabled;
        set { if (_insightsEnabled != value) { _insightsEnabled = value; OnPropertyChanged(); } }
    }

    public bool DailyReportEnabled
    {
        get => _dailyReportEnabled;
        set { if (_dailyReportEnabled != value) { _dailyReportEnabled = value; OnPropertyChanged(); } }
    }

    public bool ExamCountdownEnabled
    {
        get => _examCountdownEnabled;
        set { if (_examCountdownEnabled != value) { _examCountdownEnabled = value; OnPropertyChanged(); } }
    }

    public bool DynamicIslandEnabled
    {
        get => _dynamicIslandEnabled;
        set { if (_dynamicIslandEnabled != value) { _dynamicIslandEnabled = value; OnPropertyChanged(); } }
    }

    public SettingsViewModel(IStorageService storageService, ICameraService cameraService)
    {
        _storageService = storageService;
        _cameraService = cameraService;
        _availableCameras = new ObservableCollection<string>();

        LoadSettings();
        LoadAvailableCamerasAsync().ConfigureAwait(false);
    }

    private void LoadSettings()
    {
        var settings = _storageService.LoadSettings();

        WorkMinutes = settings.WorkMinutes;
        ShortBreakMinutes = settings.ShortBreakMinutes;
        LongBreakMinutes = settings.LongBreakMinutes;
        LongBreakInterval = settings.LongBreakInterval;

        CameraAlertEnabled = settings.CameraAlertEnabled;
        CameraAlertMode = settings.CameraAlertMode;
        CameraFixedOnSeconds = settings.CameraFixedOnSeconds;
        CameraFollowBreakEnabled = settings.CameraFollowBreakEnabled;
        CameraAlertCanManualClose = settings.CameraAlertCanManualClose;
        CameraAlertLevel = settings.CameraAlertLevel;
        SelectedCameraIndex = settings.CameraIndex;

        SoundEnabled = settings.SoundEnabled;
        PopupEnabled = settings.PopupEnabled;
        SystemNotificationEnabled = settings.SystemNotificationEnabled;

        TrayEnabled = settings.TrayEnabled;
        CloseToTray = settings.CloseToTray;
        AutoStartEnabled = settings.AutoStartEnabled;

        Theme = settings.Theme;
        AnimationEnabled = settings.AnimationEnabled;
        DailyGoalMinutes = settings.DailyGoalMinutes;
        WeeklyGoalMinutes = settings.WeeklyGoalMinutes;
        DailyTargetPomodoros = settings.DailyTargetPomodoros;
        PresenceDetectionEnabled = settings.PresenceDetectionEnabled;
        PresenceDetectionSeconds = settings.PresenceDetectionSeconds;
        FocusGuardEnabled = settings.FocusGuardEnabled;
        FocusGuardIdleSeconds = settings.FocusGuardIdleSeconds;
        FocusGuardBlocklistText = string.Join(Environment.NewLine, settings.FocusGuardBlocklist ?? new List<string>());
        FocusGuardAlertLevel = settings.FocusGuardAlertLevel;
        ExamDate = settings.ExamDate;
        ExamName = settings.ExamName;
        InsightsEnabled = settings.InsightsEnabled;
        DailyReportEnabled = settings.DailyReportEnabled;
        ExamCountdownEnabled = settings.ExamCountdownEnabled;
        DynamicIslandEnabled = settings.DynamicIslandEnabled;
    }

    private async Task LoadAvailableCamerasAsync()
    {
        var cameras = await _cameraService.GetAvailableCamerasAsync().ConfigureAwait(false);
        AvailableCameras.Clear();
        foreach (var camera in cameras)
        {
            AvailableCameras.Add(camera);
        }
    }

    public void SaveSettings()
    {
        var latestSettings = _storageService.LoadSettings();

        var settings = new Settings
        {
            WorkMinutes = WorkMinutes,
            ShortBreakMinutes = ShortBreakMinutes,
            LongBreakMinutes = LongBreakMinutes,
            LongBreakInterval = LongBreakInterval,

            CameraAlertEnabled = CameraAlertEnabled,
            CameraAlertMode = CameraAlertMode,
            CameraFixedOnSeconds = CameraFixedOnSeconds,
            CameraFollowBreakEnabled = CameraFollowBreakEnabled,
            CameraIndex = SelectedCameraIndex,
            CameraAlertCanManualClose = CameraAlertCanManualClose,
            CameraAlertLevel = CameraAlertLevel,
            HasShownCameraPrivacyNotice = latestSettings.HasShownCameraPrivacyNotice,

            SoundEnabled = SoundEnabled,
            PopupEnabled = PopupEnabled,
            SystemNotificationEnabled = SystemNotificationEnabled,

            TrayEnabled = TrayEnabled,
            CloseToTray = CloseToTray,
            AutoStartEnabled = AutoStartEnabled,

            Theme = Theme,
            AnimationEnabled = AnimationEnabled,
            DailyGoalMinutes = DailyGoalMinutes,
            WeeklyGoalMinutes = WeeklyGoalMinutes,
            DailyTargetPomodoros = DailyTargetPomodoros,
            PresenceDetectionEnabled = PresenceDetectionEnabled,
            PresenceDetectionSeconds = PresenceDetectionSeconds,
            FocusGuardEnabled = FocusGuardEnabled,
            FocusGuardIdleSeconds = FocusGuardIdleSeconds,
            FocusGuardBlocklist = ParseBlocklist(FocusGuardBlocklistText),
            FocusGuardPollSeconds = latestSettings.FocusGuardPollSeconds,
            FocusGuardAlertLevel = FocusGuardAlertLevel,
            ExamDate = ExamDate,
            ExamName = ExamName,
            InsightsEnabled = InsightsEnabled,
            DailyReportEnabled = DailyReportEnabled,
            ExamCountdownEnabled = ExamCountdownEnabled,
            DynamicIslandEnabled = DynamicIslandEnabled
        };

        _storageService.SaveSettings(settings);

        UpdateAutoStart();

        if (!CameraAlertEnabled && !SoundEnabled && !PopupEnabled && !SystemNotificationEnabled)
        {
            MessageBox.Show("警告：所有提醒方式已关闭，到点可能无法感知！", "提醒设置", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }


    private static List<string> ParseBlocklist(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        return text
            .Split(new[] { '\r', '\n', ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void UpdateAutoStart()
    {
        try
        {
            var startupPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(startupPath, true))
            {
                if (key == null)
                {
                    MessageBox.Show("无法访问系统自启动注册表，请以管理员权限运行。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (AutoStartEnabled)
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("LumenPomodoro", $"\"{exePath}\"");
                    }
                }
                else
                {
                    try { key.DeleteValue("LumenPomodoro", false); } catch (Exception ex) { Log.Debug(ex, "删除注册表自启动项失败"); }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("无权修改注册表，请以管理员权限运行。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"设置开机自启失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Log.Error(ex, "更新自启动失败");
        }
    }

    private void ApplyTheme(string theme)
    {
        try
        {
            if (Application.Current is App app)
            {
                app.ApplyTheme(theme);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "应用主题失败");
        }
    }

    public void Dispose()
    {
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
