using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly ICameraService _cameraService;

    [ObservableProperty]
    private int _workMinutes;

    [ObservableProperty]
    private int _shortBreakMinutes;

    [ObservableProperty]
    private int _longBreakMinutes;

    [ObservableProperty]
    private int _longBreakInterval;

    [ObservableProperty]
    private bool _cameraAlertEnabled;

    [ObservableProperty]
    private CameraAlertMode _cameraAlertMode;

    [ObservableProperty]
    private int _cameraFixedOnSeconds;

    [ObservableProperty]
    private bool _cameraFollowBreakEnabled;

    [ObservableProperty]
    private int _selectedCameraIndex;

    [ObservableProperty]
    private bool _cameraAlertCanManualClose;

    [ObservableProperty]
    private CameraAlertLevel _cameraAlertLevel = CameraAlertLevel.Medium;

    [ObservableProperty]
    private bool _presenceDetectionEnabled = true;

    [ObservableProperty]
    private int _presenceDetectionSeconds = 5;

    [ObservableProperty]
    private ObservableCollection<string> _availableCameras = new();

    [ObservableProperty]
    private bool _focusGuardEnabled = true;

    [ObservableProperty]
    private int _focusGuardIdleSeconds = 180;

    [ObservableProperty]
    private string _focusGuardBlocklistText = string.Empty;

    [ObservableProperty]
    private CameraAlertLevel _focusGuardAlertLevel = CameraAlertLevel.Medium;

    [ObservableProperty]
    private int _focusGuardDebounceHits = 2;

    [ObservableProperty]
    private int _focusGuardMaxAlertsPerSession = 3;

    [ObservableProperty]
    private bool _focusGuardRespectDoNotDisturb = true;

    [ObservableProperty]
    private bool _soundEnabled;

    [ObservableProperty]
    private bool _popupEnabled;

    [ObservableProperty]
    private bool _systemNotificationEnabled;

    [ObservableProperty]
    private bool _trayEnabled;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private string _theme = "system";

    [ObservableProperty]
    private bool _animationEnabled;

    [ObservableProperty]
    private int _dailyGoalMinutes = 120;

    [ObservableProperty]
    private int _weeklyGoalMinutes = 600;

    [ObservableProperty]
    private int _dailyTargetPomodoros = 8;

    [ObservableProperty]
    private DateTime? _examDate;

    [ObservableProperty]
    private string _examName = "考研";

    [ObservableProperty]
    private bool _insightsEnabled = true;

    [ObservableProperty]
    private bool _dailyReportEnabled = true;

    [ObservableProperty]
    private bool _examCountdownEnabled = true;

    [ObservableProperty]
    private bool _dynamicIslandEnabled = true;

    [ObservableProperty]
    private bool _confirmExitWhileFocusing = true;

    [ObservableProperty]
    private int _sessionEndPreNotifySeconds = 30;

    [ObservableProperty]
    private bool _fullscreenBreakEnabled;

    [ObservableProperty]
    private bool _strictModeEnabled;

    // ── 属性边界修正（在 OnChanged 中后置 clamp） ──

    partial void OnWorkMinutesChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 120);
        if (clamped != value) { _workMinutes = clamped; OnPropertyChanged(nameof(WorkMinutes)); }
    }

    partial void OnShortBreakMinutesChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 60);
        if (clamped != value) { _shortBreakMinutes = clamped; OnPropertyChanged(nameof(ShortBreakMinutes)); }
    }

    partial void OnLongBreakMinutesChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 60);
        if (clamped != value) { _longBreakMinutes = clamped; OnPropertyChanged(nameof(LongBreakMinutes)); }
    }

    partial void OnLongBreakIntervalChanged(int value)
    {
        var clamped = Math.Clamp(value, 2, 10);
        if (clamped != value) { _longBreakInterval = clamped; OnPropertyChanged(nameof(LongBreakInterval)); }
    }

    partial void OnCameraFixedOnSecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 300);
        if (clamped != value) { _cameraFixedOnSeconds = clamped; OnPropertyChanged(nameof(CameraFixedOnSeconds)); }
    }

    partial void OnPresenceDetectionSecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 3, 30);
        if (clamped != value) { _presenceDetectionSeconds = clamped; OnPropertyChanged(nameof(PresenceDetectionSeconds)); }
    }

    partial void OnFocusGuardIdleSecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 10, 3600);
        if (clamped != value) { _focusGuardIdleSeconds = clamped; OnPropertyChanged(nameof(FocusGuardIdleSeconds)); }
    }

    partial void OnFocusGuardDebounceHitsChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 10);
        if (clamped != value) { _focusGuardDebounceHits = clamped; OnPropertyChanged(nameof(FocusGuardDebounceHits)); }
    }

    partial void OnFocusGuardMaxAlertsPerSessionChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 20);
        if (clamped != value) { _focusGuardMaxAlertsPerSession = clamped; OnPropertyChanged(nameof(FocusGuardMaxAlertsPerSession)); }
    }

    partial void OnDailyGoalMinutesChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 1440);
        if (clamped != value) { _dailyGoalMinutes = clamped; OnPropertyChanged(nameof(DailyGoalMinutes)); }
    }

    partial void OnWeeklyGoalMinutesChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 10080);
        if (clamped != value) { _weeklyGoalMinutes = clamped; OnPropertyChanged(nameof(WeeklyGoalMinutes)); }
    }

    partial void OnSessionEndPreNotifySecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 300);
        if (clamped != value) { _sessionEndPreNotifySeconds = clamped; OnPropertyChanged(nameof(SessionEndPreNotifySeconds)); }
    }

    partial void OnDailyTargetPomodorosChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 50);
        if (clamped != value) { _dailyTargetPomodoros = clamped; OnPropertyChanged(nameof(DailyTargetPomodoros)); }
    }

    public SettingsViewModel(IStorageService storageService, ICameraService cameraService)
    {
        _storageService = storageService;
        _cameraService = cameraService;

        LoadSettings();
        _ = LoadAvailableCamerasAsync(); // fire-and-forget, error swallowed intentionally
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
        FocusGuardDebounceHits = settings.FocusGuardDebounceHits;
        FocusGuardMaxAlertsPerSession = settings.FocusGuardMaxAlertsPerSession;
        FocusGuardRespectDoNotDisturb = settings.FocusGuardRespectDoNotDisturb;
        ExamDate = settings.ExamDate;
        ExamName = settings.ExamName;
        InsightsEnabled = settings.InsightsEnabled;
        DailyReportEnabled = settings.DailyReportEnabled;
        ExamCountdownEnabled = settings.ExamCountdownEnabled;
        DynamicIslandEnabled = settings.DynamicIslandEnabled;
        ConfirmExitWhileFocusing = settings.ConfirmExitWhileFocusing;
        SessionEndPreNotifySeconds = settings.SessionEndPreNotifySeconds;
        FullscreenBreakEnabled = settings.FullscreenBreakEnabled;
        StrictModeEnabled = settings.StrictModeEnabled;
    }

    private async Task LoadAvailableCamerasAsync()
    {
        var cameras = await _cameraService.GetAvailableCamerasAsync().ConfigureAwait(true);
        AvailableCameras.Clear();
        foreach (var camera in cameras)
        {
            AvailableCameras.Add(camera);
        }
    }

    /// <summary>重新显示首次产品引导（设置页入口）。</summary>
    [RelayCommand]
    private void ReplayOnboarding()
    {
        var latest = _storageService.LoadSettings();
        latest.HasCompletedOnboarding = false;
        _storageService.SaveSettings(latest);

        var owner = Application.Current?.MainWindow;
        var win = new Views.OnboardingWindow(latest) { Owner = owner };
        win.ShowDialog();
        if (win.Completed)
        {
            _storageService.SaveSettings(latest);
            LoadFromSettings(latest);
            // 将完整对象写回，避免只同步了部分字段
            Save();
            MessageBox.Show("引导已完成，场景预设已更新。", "引导", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            // 用户直接关窗：恢复「已完成」，避免下次启动反复弹
            latest.HasCompletedOnboarding = true;
            _storageService.SaveSettings(latest);
        }
    }

    [RelayCommand]
    private void OpenCameraPrivacySettings()
    {
        MainViewModel.OpenWindowsCameraPrivacySettings();
    }

    [RelayCommand]
    private void ApplyLightFocusPreset() => ApplyScenePresetAndSave("light", "轻松");

    [RelayCommand]
    private void ApplyStandardFocusPreset() => ApplyScenePresetAndSave("standard", "标准");

    /// <summary>一键应用：严格模式 + 全屏休息 + 摄像头灯等。</summary>
    [RelayCommand]
    private void ApplyStrictFocusPreset() => ApplyScenePresetAndSave("strict", "严格专注");

    private void ApplyScenePresetAndSave(string scene, string displayName)
    {
        var draft = BuildSettingsFromUi(_storageService.LoadSettings());
        draft.ApplyFocusScenePreset(scene);
        LoadFromSettings(draft);
        Save();

        MessageBox.Show(
            $"已应用「{displayName}」场景预设。\n\n时长、任务与黑名单未改动，可随时在设置中微调。",
            "场景预设",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void LoadFromSettings(Settings settings)
    {
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
        FocusGuardEnabled = settings.FocusGuardEnabled;
        FocusGuardIdleSeconds = settings.FocusGuardIdleSeconds;
        FocusGuardBlocklistText = string.Join(Environment.NewLine, settings.FocusGuardBlocklist ?? new List<string>());
        FocusGuardAlertLevel = settings.FocusGuardAlertLevel;
        FocusGuardDebounceHits = settings.FocusGuardDebounceHits;
        FocusGuardMaxAlertsPerSession = settings.FocusGuardMaxAlertsPerSession;
        FocusGuardRespectDoNotDisturb = settings.FocusGuardRespectDoNotDisturb;
        ConfirmExitWhileFocusing = settings.ConfirmExitWhileFocusing;
        SessionEndPreNotifySeconds = settings.SessionEndPreNotifySeconds;
        FullscreenBreakEnabled = settings.FullscreenBreakEnabled;
        StrictModeEnabled = settings.StrictModeEnabled;
    }

    private Settings BuildSettingsFromUi(Settings latestSettings)
    {
        return new Settings
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
            HasCompletedOnboarding = latestSettings.HasCompletedOnboarding,
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
            FocusGuardDebounceHits = FocusGuardDebounceHits,
            FocusGuardMaxAlertsPerSession = FocusGuardMaxAlertsPerSession,
            FocusGuardRespectDoNotDisturb = FocusGuardRespectDoNotDisturb,
            FocusGuardAlertLevel = FocusGuardAlertLevel,
            ExamDate = ExamDate,
            ExamName = ExamName,
            InsightsEnabled = InsightsEnabled,
            DailyReportEnabled = DailyReportEnabled,
            ExamCountdownEnabled = ExamCountdownEnabled,
            DynamicIslandEnabled = DynamicIslandEnabled,
            ConfirmExitWhileFocusing = ConfirmExitWhileFocusing,
            SessionEndPreNotifySeconds = SessionEndPreNotifySeconds,
            FullscreenBreakEnabled = FullscreenBreakEnabled,
            StrictModeEnabled = StrictModeEnabled,
            Language = latestSettings.Language,
            SchemaVersion = latestSettings.SchemaVersion,
            LastSelectedTaskId = latestSettings.LastSelectedTaskId,
            LastReportShownDate = latestSettings.LastReportShownDate
        };
    }

    /// <summary>供 code-behind 调用（与 SaveCommand 等价）。</summary>
    public void SaveSettings() => Save();

    [RelayCommand]
    private void Save()
    {
        var latestSettings = _storageService.LoadSettings();
        var settings = BuildSettingsFromUi(latestSettings);
        _storageService.SaveSettings(settings);

        UpdateAutoStart();
        ApplyTheme();

        if (!CameraAlertEnabled && !SoundEnabled && !PopupEnabled && !SystemNotificationEnabled)
        {
            MessageBox.Show(Properties.LocalizedStrings.AllAlertsDisabled, "提醒设置", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show(Properties.LocalizedStrings.RegistryAccessDenied, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(Properties.LocalizedStrings.RegistryWriteDenied, "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{Properties.LocalizedStrings.AutoStartFailed}：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Log.Error(ex, "更新自启动失败");
        }
    }

    private void ApplyTheme()
    {
        try
        {
            if (Application.Current is App app)
            {
                app.ApplyTheme(Theme);
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
}
