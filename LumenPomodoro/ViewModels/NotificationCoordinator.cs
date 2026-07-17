using System;
using System.Windows.Threading;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.ViewModels;

/// <summary>
/// 统一声音、系统通知、灵动岛倒计时、托盘更新逻辑。
/// 灵动岛为产品主交互：只要启用即推送，前台淡化策略由 View 层处理。
/// </summary>
public class NotificationCoordinator : IDisposable
{
    private readonly ISoundService _soundService;
    private DispatcherTimer? _trayUpdateTimer;
    private bool _disposed;

    private const int TrayUpdateIntervalSeconds = 5;

    public event Action? TrayMenuNeedsUpdate;
    public event Action<string, string>? NotificationRequested;
    public event Action<string, string>? InAppNotificationRequested;
    public event Action<string>? CountdownStartRequested;
    public event Action<string>? CountdownUpdateRequested;
    public event Action? CountdownStopRequested;

    public NotificationCoordinator(ISoundService soundService)
    {
        _soundService = soundService;
    }

    public void StartTrayTimer()
    {
        _trayUpdateTimer?.Stop();
        _trayUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TrayUpdateIntervalSeconds) };
        _trayUpdateTimer.Tick += (_, _) => TrayMenuNeedsUpdate?.Invoke();
        _trayUpdateTimer.Start();
    }

    public void PlaySound(string soundName, bool soundEnabled)
    {
        if (!soundEnabled) return;
        try
        {
            _soundService.PlaySound(soundName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "播放音效 {Name} 失败", soundName);
        }
    }

    public void ShowInApp(string title, string message, bool popupEnabled)
    {
        if (!popupEnabled) return;
        InAppNotificationRequested?.Invoke(title, message);
    }

    public void ShowSystem(string title, string message, bool systemNotificationEnabled, bool isWindowActive)
    {
        if (!systemNotificationEnabled) return;
        if (isWindowActive) return;
        NotificationRequested?.Invoke(title, message);
    }

    /// <summary>启动灵动岛倒计时（不因窗口置顶而抑制）。</summary>
    public void StartCountdown(string message, bool isWindowTopmost, bool dynamicIslandEnabled)
    {
        // isWindowTopmost 保留参数以兼容调用方；岛展示策略改由 View 按 WhenFocused 处理
        _ = isWindowTopmost;
        if (dynamicIslandEnabled)
            CountdownStartRequested?.Invoke(message);
    }

    public void UpdateCountdown(string remainingTime, bool isWindowTopmost, bool dynamicIslandEnabled)
    {
        _ = isWindowTopmost;
        if (dynamicIslandEnabled)
            CountdownUpdateRequested?.Invoke(remainingTime);
    }

    public void StopCountdown() => CountdownStopRequested?.Invoke();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _trayUpdateTimer?.Stop();
    }
}
