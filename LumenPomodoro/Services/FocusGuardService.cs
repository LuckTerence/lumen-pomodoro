using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LumenPomodoro.Interop;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

/// <summary>
/// 基于前台窗口 + 键鼠空闲的防走神检测。
/// 周期性轮询：空闲超阈值判为"离开"，前台进程名/窗口标题命中黑名单判为"分心"。
/// 连续命中达到防抖次数后触发一次 <see cref="DistractionDetected"/>（每会话有上限），
/// 恢复专注后触发 <see cref="FocusRegained"/>。
/// </summary>
public sealed class FocusGuardService : IFocusGuardService
{
    private readonly object _lock = new();
    private readonly FocusGuardEngine _engine = new();
    private Timer? _timer;
    private string _ownProcessName = string.Empty;

    private string[] _blocklist = Array.Empty<string>();
    private double _idleThresholdSeconds;
    private int _pollMs;
    private bool _respectDoNotDisturb = true;

    /// <summary>测试用：覆盖原生 Evaluate，返回分心原因或 null。</summary>
    internal Func<string?>? EvaluateOverride { get; set; }

    /// <summary>测试用：覆盖系统勿扰检测。</summary>
    internal Func<bool>? DoNotDisturbOverride { get; set; }

    public bool IsRunning { get; private set; }

    /// <summary>当前会话已发出的告警次数（便于诊断与测试）。</summary>
    public int AlertCount
    {
        get { lock (_lock) return _engine.AlertCount; }
    }

    public event Action<string>? DistractionDetected;
    public event Action? FocusRegained;

    public void Start(Settings settings, bool resetSessionCounters = true)
    {
        if (settings == null || !settings.FocusGuardEnabled) return;

        // 若已在跑：停止旧 Timer 后按新参数重启（避免双重 Timer；暂停后恢复走 resetSessionCounters=false）
        Timer? previousTimer = null;
        lock (_lock)
        {
            if (IsRunning)
            {
                IsRunning = false;
                previousTimer = _timer;
                _timer = null;
            }

            _blocklist = (settings.FocusGuardBlocklist ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToArray();
            _idleThresholdSeconds = Math.Max(1, settings.FocusGuardIdleSeconds);
            _pollMs = Math.Max(1, settings.FocusGuardPollSeconds) * 1000;

            _engine.Configure(
                settings.FocusGuardDebounceHits,
                settings.FocusGuardMaxAlertsPerSession);
            _respectDoNotDisturb = settings.FocusGuardRespectDoNotDisturb;
            if (resetSessionCounters)
                _engine.ResetSession();
            else
                _engine.ResetRunningState();

            try
            {
                using var p = System.Diagnostics.Process.GetCurrentProcess();
                _ownProcessName = p.ProcessName;
            }
            catch
            {
                _ownProcessName = string.Empty;
            }

            IsRunning = true;
            _timer = new Timer(OnTick, null, _pollMs, _pollMs);
            Log.Information(
                "[FocusGuard] 启动，空闲阈值={Idle}s，轮询={Poll}ms，防抖={Debounce}，上限={Max}，遵从勿扰={Dnd}，黑名单={Count} 项，重置会话={Reset}",
                _idleThresholdSeconds, _pollMs, _engine.DebounceHits, _engine.MaxAlertsPerSession,
                _respectDoNotDisturb, _blocklist.Length, resetSessionCounters);
        }

        previousTimer?.Dispose();
    }

    public void Stop()
    {
        Timer? toDispose;
        lock (_lock)
        {
            if (!IsRunning) return;
            IsRunning = false;
            toDispose = _timer;
            _timer = null;
            _engine.ResetRunningState();
        }

        toDispose?.Dispose();
        Log.Information("[FocusGuard] 停止");
    }

    private void OnTick(object? state)
    {
        lock (_lock)
        {
            if (!IsRunning) return;
        }

        string? reason = EvaluateOverride?.Invoke() ?? Evaluate();
        ProcessEvaluation(reason);
    }

    /// <summary>供单测直接注入判定结果，不经 Timer。</summary>
    internal void ProcessEvaluation(string? reason)
    {
        bool suppress = false;
        lock (_lock)
        {
            if (!IsRunning) return;
            if (_respectDoNotDisturb)
            {
                try
                {
                    suppress = DoNotDisturbOverride?.Invoke()
                               ?? SystemAttentionState.IsDoNotDisturbActive();
                }
                catch
                {
                    suppress = false;
                }
            }
        }

        FocusGuardTickResult result;
        lock (_lock)
        {
            if (!IsRunning) return;
            result = _engine.Tick(reason, suppressNotification: suppress);
        }

        if (result.FireDistraction && result.Reason != null)
            DistractionDetected?.Invoke(result.Reason);
        if (result.FireRegained)
            FocusRegained?.Invoke();
    }

    /// <summary>判定当前是否分心；返回原因文案，专注时返回 null。</summary>
    private string? Evaluate()
    {
        double idle = InputMonitorNative.GetIdleSeconds();
        if (idle >= _idleThresholdSeconds)
        {
            return $"检测到你已 {Math.Round(idle / 60.0, 1)} 分钟无操作，可能已离开。";
        }

        var (processName, windowTitle) = InputMonitorNative.GetForegroundInfo();

        // 本应用窗口不算分心
        if (!string.IsNullOrEmpty(_ownProcessName) &&
            string.Equals(processName, _ownProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (var entry in _blocklist)
        {
            if ((!string.IsNullOrEmpty(processName) &&
                 processName.Contains(entry, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(windowTitle) &&
                 windowTitle.Contains(entry, StringComparison.OrdinalIgnoreCase)))
            {
                var label = !string.IsNullOrEmpty(windowTitle) ? windowTitle : processName;
                return $"检测到你正在使用「{label}」，注意力可能已分散。";
            }
        }

        return null;
    }

    public void Dispose() => Stop();
}
