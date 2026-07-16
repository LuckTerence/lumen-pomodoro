namespace LumenPomodoro.Services;

/// <summary>
/// FocusGuard 纯状态机：防抖进入分心、每会话告警上限、恢复专注。
/// 与平台 API 解耦，便于单测（对齐 docs/focus-guard-stretchly-alignment.md）。
/// </summary>
public sealed class FocusGuardEngine
{
    public const int DefaultDebounceHits = 2;
    public const int DefaultMaxAlertsPerSession = 3;

    private int _debounceHits = DefaultDebounceHits;
    private int _maxAlertsPerSession = DefaultMaxAlertsPerSession;
    private int _consecutiveHits;
    private int _alertCount;
    private bool _isDistracted;

    public int DebounceHits => _debounceHits;
    public int MaxAlertsPerSession => _maxAlertsPerSession;
    public int ConsecutiveHits => _consecutiveHits;
    public int AlertCount => _alertCount;
    public bool IsDistracted => _isDistracted;

    /// <summary>配置防抖与每会话最大告警次数；非法值会被夹紧。</summary>
    public void Configure(int debounceHits = DefaultDebounceHits, int maxAlertsPerSession = DefaultMaxAlertsPerSession)
    {
        _debounceHits = Math.Clamp(debounceHits, 1, 10);
        _maxAlertsPerSession = Math.Clamp(maxAlertsPerSession, 1, 20);
    }

    /// <summary>新专注会话开始时重置全部计数。</summary>
    public void ResetSession()
    {
        _consecutiveHits = 0;
        _alertCount = 0;
        _isDistracted = false;
    }

    /// <summary>停止监控时清除运行态；不跨会话保留 alertCount（由下次 Start 的 ResetSession 负责）。</summary>
    public void ResetRunningState()
    {
        _consecutiveHits = 0;
        _isDistracted = false;
    }

    /// <summary>
    /// 处理一次 poll 结果。
    /// </summary>
    /// <param name="reason">非 null 表示本 tick 判定为分心/离开。</param>
    /// <param name="suppressNotification">
    /// true：系统勿扰等场景下仍更新分心状态，但不增加告警计数、不触发通知。
    /// </param>
    /// <returns>是否应触发 DistractionDetected、是否应触发 FocusRegained，以及告警原因。</returns>
    public FocusGuardTickResult Tick(string? reason, bool suppressNotification = false)
    {
        if (reason != null)
        {
            _consecutiveHits++;
            if (_consecutiveHits >= _debounceHits && !_isDistracted)
            {
                _isDistracted = true;
                if (suppressNotification)
                {
                    // 勿扰：标记分心但不消耗 MaxAlerts 配额
                    return FocusGuardTickResult.None;
                }

                if (_alertCount < _maxAlertsPerSession)
                {
                    _alertCount++;
                    return FocusGuardTickResult.Distraction(reason);
                }

                // 已达上限：仍标记分心，避免每 tick 重试，但不发通知
                return FocusGuardTickResult.None;
            }

            return FocusGuardTickResult.None;
        }

        _consecutiveHits = 0;
        if (_isDistracted)
        {
            _isDistracted = false;
            // 恢复专注不重置 alertCount，防止反复进出刷满通知
            return FocusGuardTickResult.Regained();
        }

        return FocusGuardTickResult.None;
    }
}

public readonly struct FocusGuardTickResult
{
    public bool FireDistraction { get; }
    public bool FireRegained { get; }
    public string? Reason { get; }

    private FocusGuardTickResult(bool fireDistraction, bool fireRegained, string? reason)
    {
        FireDistraction = fireDistraction;
        FireRegained = fireRegained;
        Reason = reason;
    }

    public static FocusGuardTickResult None => new(false, false, null);

    public static FocusGuardTickResult Distraction(string reason) => new(true, false, reason);

    public static FocusGuardTickResult Regained() => new(false, true, null);
}
