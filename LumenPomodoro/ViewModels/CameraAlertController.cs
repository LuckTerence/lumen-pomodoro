using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LumenPomodoro.Models;
using LumenPomodoro.Properties;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.Views;
using Serilog;

namespace LumenPomodoro.ViewModels;

/// <summary>
/// 管理相机提醒的生命周期：启动/停止摄像头、指示灯窗口、存在检测响应。
/// </summary>
public class CameraAlertController
{
    private readonly ICameraService _cameraService;
    private CameraIndicatorWindow? _indicatorWindow;
    private int _consecutivePresenceLostAlerts;
    private const int MaxPresenceLostAlerts = 3;

    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorOccurred;
    public event Action<string, string>? SystemNotificationRequested;
    public event Action<Window>? WindowActivationRequested;

    public bool IsActive { get; private set; }
    public string Status { get; private set; } = string.Empty;

    public CameraAlertController(ICameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public void Initialize(Settings settings)
    {
        _cameraService.Initialize(settings.CameraIndex,
            status => Application.Current.Dispatcher.BeginInvoke(() => UpdateStatus(status)),
            error => Application.Current.Dispatcher.BeginInvoke(() => HandleError(error)),
            OnPresenceLost,
            OnPresenceRegained);
    }

    public void Start(Settings settings)
    {
        if (!settings.CameraAlertEnabled) return;

        if (!settings.HasShownCameraPrivacyNotice)
        {
            var result = MessageBox.Show(
                "本软件仅在番茄钟结束或休息阶段根据你的设置调用摄像头，用于触发摄像头指示灯提醒。\n\n软件不会拍照、录像、保存或上传摄像头画面。\n\n是否同意启用摄像头提醒？",
                "摄像头隐私声明",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.No)
            {
                settings.CameraAlertEnabled = false;
                return;
            }

            settings.HasShownCameraPrivacyNotice = true;
        }

        try
        {
            var cameraStarted = false;
            switch (settings.CameraAlertMode)
            {
                case CameraAlertMode.FixedDuration:
                    FireAndForgetAsync(Task.Run(() => _cameraService.StartCameraForDurationAsync(settings.CameraFixedOnSeconds)),
                        "摄像头固定时长提醒",
                        ex => HandleError($"摄像头启动失败: {ex.Message}"));
                    cameraStarted = true;
                    break;
                case CameraAlertMode.UntilConfirm:
                    FireAndForgetAsync(Task.Run(() => _cameraService.StartCameraAsync()),
                        "摄像头直到确认提醒",
                        ex => HandleError($"摄像头启动失败: {ex.Message}"));
                    cameraStarted = true;
                    break;
                case CameraAlertMode.FollowBreak:
                    // Focus 结束时不做 FollowBreak
                    break;
            }

            if (cameraStarted)
            {
                ShowIndicator(Color.FromRgb(0xF5, 0x9E, 0x0B));
                ApplyAlertLevel(settings);
            }
        }
        catch (Exception ex)
        {
            HandleError($"摄像头启动失败: {ex.Message}");
        }
    }

    public void StartForBreak(Settings settings)
    {
        _consecutivePresenceLostAlerts = 0;

        if (settings.CameraAlertMode == CameraAlertMode.FollowBreak &&
            settings.CameraAlertEnabled &&
            settings.CameraFollowBreakEnabled)
        {
            FireAndForgetAsync(Task.Run(() => _cameraService.StartCameraAsync()),
                "启动摄像头(跟随休息)",
                ex => HandleError($"摄像头启动失败: {ex.Message}"));
            ShowIndicator(Color.FromRgb(0x10, 0xB9, 0x81));
        }
    }

    public bool TryStop(Settings settings)
    {
        if (!settings.EffectiveCameraAlertCanManualClose)
        {
            var msg = settings.StrictModeEnabled
                ? Properties.LocalizedStrings.StrictMode_CameraCloseBlocked
                : Properties.LocalizedStrings.CameraManualCloseNotAllowed;
            MessageBox.Show(msg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        ForceStop();
        return true;
    }

    public void ForceStop()
    {
        _consecutivePresenceLostAlerts = 0;
        FireAndForgetAsync(Task.Run(() => _cameraService.StopCameraAsync()), "停止摄像头");
        HideIndicator();
    }

    private void UpdateStatus(string status)
    {
        Status = status;
        IsActive = _cameraService.IsRunning;
        StatusChanged?.Invoke(status);
        if (!_cameraService.IsRunning)
            HideIndicator();
    }

    private void HandleError(string error)
    {
        Status = error;
        IsActive = false;
        ErrorOccurred?.Invoke(error);
    }

    private void OnPresenceLost()
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            _consecutivePresenceLostAlerts++;
            if (_consecutivePresenceLostAlerts > MaxPresenceLostAlerts) return;

            SystemNotificationRequested?.Invoke(Properties.LocalizedStrings.DistractionAlert, Properties.LocalizedStrings.DistractionMessage);
        });
    }

    public void TriggerSeverePresenceAlert()
    {
        if (Application.Current?.MainWindow is not Window mainWindow) return;
        WindowActivationRequested?.Invoke(mainWindow);
    }

    private void OnPresenceRegained()
    {
        _consecutivePresenceLostAlerts = 0;
    }

    private void ShowIndicator(Color color)
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _indicatorWindow ??= new CameraIndicatorWindow();
            _indicatorWindow.ShowIndicator(color);
        });
    }

    private void HideIndicator()
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _indicatorWindow?.HideIndicator();
        });
    }

    private static void ApplyAlertLevel(Settings settings)
    {
        // Light/Medium：灯 + 指示窗；Severe 或严格模式：额外主窗置顶
        var severe = settings.CameraAlertLevel == CameraAlertLevel.Severe || settings.StrictModeEnabled;
        if (!severe) return;

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
    }

    public async Task StopCameraAsync()
    {
        try
        {
            await _cameraService.StopCameraAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "停止摄像头异常");
        }
    }

    internal static async Task FireAndForgetAsync(Task task, string operationName, Action<Exception>? onError = null)
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
}
