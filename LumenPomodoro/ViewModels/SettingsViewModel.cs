using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly StorageService _storageService;
    private readonly CameraService _cameraService;
    
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
    private bool _hasShownCameraPrivacyNotice;
    private ObservableCollection<string> _availableCameras;
    
    private bool _soundEnabled;
    private bool _popupEnabled;
    private bool _systemNotificationEnabled;
    
    private bool _trayEnabled;
    private bool _closeToTray;
    private bool _autoStartEnabled;
    
    private string _theme;
    private bool _animationEnabled;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public int WorkMinutes
    {
        get => _workMinutes;
        set { _workMinutes = Math.Clamp(value, 1, 120); OnPropertyChanged(); }
    }

    public int ShortBreakMinutes
    {
        get => _shortBreakMinutes;
        set { _shortBreakMinutes = Math.Clamp(value, 1, 60); OnPropertyChanged(); }
    }

    public int LongBreakMinutes
    {
        get => _longBreakMinutes;
        set { _longBreakMinutes = Math.Clamp(value, 1, 60); OnPropertyChanged(); }
    }

    public int LongBreakInterval
    {
        get => _longBreakInterval;
        set { _longBreakInterval = Math.Clamp(value, 2, 10); OnPropertyChanged(); }
    }

    public bool CameraAlertEnabled
    {
        get => _cameraAlertEnabled;
        set { _cameraAlertEnabled = value; OnPropertyChanged(); }
    }

    public CameraAlertMode CameraAlertMode
    {
        get => _cameraAlertMode;
        set { _cameraAlertMode = value; OnPropertyChanged(); }
    }

    public int CameraFixedOnSeconds
    {
        get => _cameraFixedOnSeconds;
        set { _cameraFixedOnSeconds = Math.Clamp(value, 1, 300); OnPropertyChanged(); }
    }

    public bool CameraFollowBreakEnabled
    {
        get => _cameraFollowBreakEnabled;
        set { _cameraFollowBreakEnabled = value; OnPropertyChanged(); }
    }

    public bool CameraAlertCanManualClose
    {
        get => _cameraAlertCanManualClose;
        set { _cameraAlertCanManualClose = value; OnPropertyChanged(); }
    }

    public int SelectedCameraIndex
    {
        get => _selectedCameraIndex;
        set { _selectedCameraIndex = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> AvailableCameras
    {
        get => _availableCameras;
        set { _availableCameras = value; OnPropertyChanged(); }
    }

    public bool SoundEnabled
    {
        get => _soundEnabled;
        set { _soundEnabled = value; OnPropertyChanged(); }
    }

    public bool PopupEnabled
    {
        get => _popupEnabled;
        set { _popupEnabled = value; OnPropertyChanged(); }
    }

    public bool SystemNotificationEnabled
    {
        get => _systemNotificationEnabled;
        set { _systemNotificationEnabled = value; OnPropertyChanged(); }
    }

    public bool TrayEnabled
    {
        get => _trayEnabled;
        set { _trayEnabled = value; OnPropertyChanged(); }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set { _closeToTray = value; OnPropertyChanged(); }
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set { _autoStartEnabled = value; OnPropertyChanged(); UpdateAutoStart(); }
    }

    public string Theme
    {
        get => _theme;
        set { _theme = value; OnPropertyChanged(); ApplyTheme(value); }
    }

    public bool AnimationEnabled
    {
        get => _animationEnabled;
        set { _animationEnabled = value; OnPropertyChanged(); }
    }

    public SettingsViewModel(StorageService storageService, CameraService cameraService)
    {
        _storageService = storageService;
        _cameraService = cameraService;
        _availableCameras = new ObservableCollection<string>();
        
        LoadSettings();
        LoadAvailableCameras();
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
        _hasShownCameraPrivacyNotice = settings.HasShownCameraPrivacyNotice;
        SelectedCameraIndex = settings.CameraIndex;
        
        SoundEnabled = settings.SoundEnabled;
        PopupEnabled = settings.PopupEnabled;
        SystemNotificationEnabled = settings.SystemNotificationEnabled;
        
        TrayEnabled = settings.TrayEnabled;
        CloseToTray = settings.CloseToTray;
        AutoStartEnabled = settings.AutoStartEnabled;
        
        Theme = settings.Theme;
        AnimationEnabled = settings.AnimationEnabled;
    }

    private void LoadAvailableCameras()
    {
        var cameras = _cameraService.GetAvailableCameras();
        AvailableCameras.Clear();
        foreach (var camera in cameras)
        {
            AvailableCameras.Add(camera);
        }
    }

    public void SaveSettings()
    {
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
            HasShownCameraPrivacyNotice = _hasShownCameraPrivacyNotice,
            
            SoundEnabled = SoundEnabled,
            PopupEnabled = PopupEnabled,
            SystemNotificationEnabled = SystemNotificationEnabled,
            
            TrayEnabled = TrayEnabled,
            CloseToTray = CloseToTray,
            AutoStartEnabled = AutoStartEnabled,
            
            Theme = Theme,
            AnimationEnabled = AnimationEnabled
        };
        
        _storageService.SaveSettings(settings);
        
        if (!CameraAlertEnabled && !SoundEnabled && !PopupEnabled && !SystemNotificationEnabled)
        {
            MessageBox.Show("警告：所有提醒方式已关闭，到点可能无法感知！", "提醒设置", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void TestCameraAlert()
    {
        if (!CameraAlertEnabled)
        {
            MessageBox.Show("摄像头提醒已关闭", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        try
        {
            _ = _cameraService.StartCameraForDurationAsync(5);
            MessageBox.Show("摄像头测试中，5秒后自动关闭", "测试摄像头", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"摄像头测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Cleanup()
    {
        if (_cameraService.IsRunning)
        {
            _ = _cameraService.StopCameraAsync();
        }
    }

    private void UpdateAutoStart()
    {
        try
        {
            var startupPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(startupPath, true))
            {
                if (AutoStartEnabled)
                {
                    key?.SetValue("LumenPomodoro", System.Windows.Forms.Application.ExecutablePath);
                }
                else
                {
                    key?.DeleteValue("LumenPomodoro", false);
                }
            }
        }
        catch { }
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
        catch
        {
            // Theme application failed
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}