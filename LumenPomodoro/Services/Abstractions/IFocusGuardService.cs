using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

/// <summary>
/// 防走神检测：在专注阶段监控前台窗口与键鼠空闲，判定分心/离开。
/// </summary>
public interface IFocusGuardService : IDisposable
{
    bool IsRunning { get; }

    /// <summary>检测到分心或离开时触发，参数为可读原因（用于通知文案）。</summary>
    event Action<string>? DistractionDetected;

    /// <summary>从分心状态恢复专注时触发。</summary>
    event Action? FocusRegained;

    /// <summary>
    /// 按当前设置启动监控。设置未启用时不做任何事。
    /// </summary>
    /// <param name="settings">当前设置。</param>
    /// <param name="resetSessionCounters">
    /// true：新专注会话，重置防抖与告警计数；
    /// false：暂停后恢复，保留本会话已告警次数。
    /// </param>
    void Start(Settings settings, bool resetSessionCounters = true);

    void Stop();
}
