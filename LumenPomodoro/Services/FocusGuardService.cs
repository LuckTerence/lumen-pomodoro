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
/// 连续命中达到防抖次数后触发一次 <see cref="DistractionDetected"/>，恢复专注后触发 <see cref="FocusRegained"/>。
/// </summary>
public sealed class FocusGuardService : IFocusGuardService
{
    private const int DebounceHits = 1;

    private readonly object _lock = new();
    private Timer? _timer;
    private string _ownProcessName = string.Empty;

    private string[] _blocklist = Array.Empty<string>();
    private double _idleThresholdSeconds;
    private int _pollMs;

    private int _consecutiveHits;
    private bool _isDistracted;

    public bool IsRunning { get; private set; }

    public event Action<string>? DistractionDetected;
    public event Action? FocusRegained;

    public void Start(Settings settings)
    {
        if (settings == null || !settings.FocusGuardEnabled) return;

        lock (_lock)
        {
            if (IsRunning) return;

            _blocklist = (settings.FocusGuardBlocklist ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToArray();
            _idleThresholdSeconds = Math.Max(1, settings.FocusGuardIdleSeconds);
            _pollMs = Math.Max(1, settings.FocusGuardPollSeconds) * 1000;

            try
            {
                using var p = System.Diagnostics.Process.GetCurrentProcess();
                _ownProcessName = p.ProcessName;
            }
            catch
            {
                _ownProcessName = string.Empty;
            }

            _consecutiveHits = 0;
            _isDistracted = false;
            IsRunning = true;

            _timer = new Timer(OnTick, null, _pollMs, _pollMs);
            Log.Information("[FocusGuard] 启动，空闲阈值={Idle}s，轮询={Poll}ms，黑名单={Count} 项",
                _idleThresholdSeconds, _pollMs, _blocklist.Length);
        }
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
            _consecutiveHits = 0;
            _isDistracted = false;
        }

        toDispose?.Dispose();
        Log.Information("[FocusGuard] 停止");
    }

    private void OnTick(object? state)
    {
        // 进入临界区前判定是否仍在运行，避免 Stop 后的残留回调继续触发事件。
        lock (_lock)
        {
            if (!IsRunning) return;
        }

        string? reason = Evaluate();

        if (reason != null)
        {
            bool fire = false;
            lock (_lock)
            {
                if (!IsRunning) return;
                _consecutiveHits++;
                if (_consecutiveHits >= DebounceHits && !_isDistracted)
                {
                    _isDistracted = true;
                    fire = true;
                }
            }
            if (fire) DistractionDetected?.Invoke(reason);
        }
        else
        {
            bool regained = false;
            lock (_lock)
            {
                if (!IsRunning) return;
                _consecutiveHits = 0;
                if (_isDistracted)
                {
                    _isDistracted = false;
                    regained = true;
                }
            }
            if (regained) FocusRegained?.Invoke();
        }
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
