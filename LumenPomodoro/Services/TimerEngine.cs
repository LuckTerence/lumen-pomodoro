using LumenPomodoro.Models;

namespace LumenPomodoro.Services;

/// <summary>
/// 纯倒计时状态机：承载全部计时状态与时间推进算法，不依赖 DispatcherTimer。
/// 当前时间通过参数注入，便于在单元测试中以虚拟时钟驱动。线程安全。
/// 原 TimerService 中的时间计算逻辑已整体迁移至此，TimerService 仅负责用
/// DispatcherTimer 周期性调用 <see cref="Advance"/> 并把结果转成事件。
/// </summary>
public sealed class TimerEngine
{
    private const int TickSeconds = 1;
    /// <summary>唤醒补偿：忽略 2 秒内的偏差（系统延迟误判）</summary>
    private const double WakeCompensationMinSeconds = 2;
    /// <summary>唤醒补偿：超过 24 小时视为时钟调整而非真实睡眠</summary>
    private const double WakeCompensationMaxSeconds = 86400;
    /// <summary>每 tick 最多补偿次数，防止长时间卡顿一次性扣除过多秒数</summary>
    private const int MaxTickCompensationCount = 10;

    private readonly object _lock = new();
    private int _remainingSeconds;
    private int _totalSeconds;
    private TimerMode _currentMode = TimerMode.Idle;
    private TimerMode _modeBeforePause = TimerMode.Idle;
    private bool _isPaused;
    private bool _isRunning;
    private DateTime _lastTickTime;
    private DateTime _nextTickTime;

    public TimerMode CurrentMode { get { lock (_lock) return _currentMode; } }
    public bool IsRunning { get { lock (_lock) return _isRunning; } }
    public bool IsPaused { get { lock (_lock) return _isPaused; } }
    public int RemainingSeconds { get { lock (_lock) return _remainingSeconds; } }
    public int TotalSeconds { get { lock (_lock) return _totalSeconds; } }

    public void StartFocus(int minutes, DateTime now)
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _totalSeconds = minutes * 60;
            _remainingSeconds = _totalSeconds;
            _currentMode = TimerMode.Focus;
            _isRunning = true;
            _isPaused = false;
            _lastTickTime = now;
            _nextTickTime = now.AddSeconds(TickSeconds);
        }
    }

    public void StartBreak(int minutes, DateTime now)
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _totalSeconds = minutes * 60;
            _remainingSeconds = _totalSeconds;
            _currentMode = TimerMode.Break;
            _isRunning = true;
            _isPaused = false;
            _lastTickTime = now;
            _nextTickTime = now.AddSeconds(TickSeconds);
        }
    }

    /// <summary>暂停。返回是否真的发生了暂停（已运行且未暂停）。</summary>
    public bool Pause(DateTime now)
    {
        lock (_lock)
        {
            if (_isRunning && !_isPaused)
            {
                _isPaused = true;
                _modeBeforePause = _currentMode;
                _currentMode = TimerMode.Paused;
                _lastTickTime = now;
                _nextTickTime = now.AddSeconds(TickSeconds);
                return true;
            }
            return false;
        }
    }

    /// <summary>恢复。返回是否真的发生了恢复（已暂停且运行中）。</summary>
    public bool Resume(DateTime now)
    {
        lock (_lock)
        {
            if (_isRunning && _isPaused)
            {
                _isPaused = false;
                _currentMode = _modeBeforePause;
                _lastTickTime = now;
                _nextTickTime = now.AddSeconds(TickSeconds);
                return true;
            }
            return false;
        }
    }

    public void Reset(DateTime now)
    {
        lock (_lock)
        {
            _isRunning = false;
            _isPaused = false;
            _remainingSeconds = 0;
            _totalSeconds = 0;
            _currentMode = TimerMode.Idle;
            _nextTickTime = now;
        }
    }

    public void Stop(DateTime now)
    {
        lock (_lock)
        {
            _isRunning = false;
            _isPaused = false;
            _currentMode = TimerMode.Idle;
            _nextTickTime = now;
        }
    }

    /// <summary>
    /// 推进计时。应由驱动层（如 DispatcherTimer）以真实或虚拟时间周期性调用。
    /// </summary>
    public TimerAdvanceResult Advance(DateTime now)
    {
        lock (_lock)
        {
            if (_isPaused)
                return TimerAdvanceResult.NoTick(_remainingSeconds, _totalSeconds, _currentMode);

            if (now < _nextTickTime)
                return TimerAdvanceResult.NoTick(_remainingSeconds, _totalSeconds, _currentMode);

            int ticksToProcess = 0;
            while (_nextTickTime <= now && ticksToProcess < MaxTickCompensationCount)
            {
                ticksToProcess++;
                _nextTickTime = _nextTickTime.AddSeconds(TickSeconds);
            }

            _remainingSeconds = Math.Max(0, _remainingSeconds - ticksToProcess);
            _lastTickTime = now;
            TimerMode mode = _currentMode;

            if (_remainingSeconds <= 0)
            {
                _isRunning = false;
                _currentMode = TimerMode.Idle;
                return TimerAdvanceResult.Complete(mode, mode);
            }

            return TimerAdvanceResult.Tick(_remainingSeconds, _totalSeconds, mode);
        }
    }

    /// <summary>
    /// 系统唤醒（从休眠/待机恢复）后补偿错过的秒数。
    /// </summary>
    public TimerAdvanceResult ApplyWakeCorrection(DateTime now)
    {
        lock (_lock)
        {
            if (!_isRunning || _isPaused)
                return TimerAdvanceResult.NoTick(_remainingSeconds, _totalSeconds, _currentMode);

            var elapsed = (now - _lastTickTime).TotalSeconds;
            if (elapsed < WakeCompensationMinSeconds || elapsed > WakeCompensationMaxSeconds)
                return TimerAdvanceResult.NoTick(_remainingSeconds, _totalSeconds, _currentMode);

            _remainingSeconds = Math.Max(0, _remainingSeconds - (int)elapsed);
            _lastTickTime = now;
            _nextTickTime = now.AddSeconds(TickSeconds);

            if (_remainingSeconds <= 0)
            {
                _isRunning = false;
                var completedMode = _currentMode;
                _currentMode = TimerMode.Idle;
                return TimerAdvanceResult.CompleteWithTick(completedMode, completedMode);
            }

            return TimerAdvanceResult.Tick(_remainingSeconds, _totalSeconds, _currentMode);
        }
    }
}

/// <summary>Advance / ApplyWakeCorrection 的结果，描述本次推进应触发哪些事件。</summary>
public sealed class TimerAdvanceResult
{
    public int RemainingSeconds { get; }
    public int TotalSeconds { get; }
    public TimerMode Mode { get; }
    public bool ShouldTick { get; }
    public bool ShouldComplete { get; }
    public TimerMode CompletedMode { get; }

    private TimerAdvanceResult(int remainingSeconds, int totalSeconds, TimerMode mode, bool shouldTick, bool shouldComplete, TimerMode completedMode)
    {
        RemainingSeconds = remainingSeconds;
        TotalSeconds = totalSeconds;
        Mode = mode;
        ShouldTick = shouldTick;
        ShouldComplete = shouldComplete;
        CompletedMode = completedMode;
    }

    public static TimerAdvanceResult NoTick(int remainingSeconds, int totalSeconds, TimerMode mode)
        => new(remainingSeconds, totalSeconds, mode, false, false, TimerMode.Idle);

    public static TimerAdvanceResult Tick(int remainingSeconds, int totalSeconds, TimerMode mode)
        => new(remainingSeconds, totalSeconds, mode, true, false, TimerMode.Idle);

    /// <summary>正常 tick 自然完成：不渲染最终 00:00，直接完成（与原 Timer_Tick 行为一致）。</summary>
    public static TimerAdvanceResult Complete(TimerMode mode, TimerMode completedMode)
        => new(0, 0, mode, false, true, completedMode);

    /// <summary>唤醒后完成：先渲染一次 tick 再完成（与原 CorrectAfterWake 行为一致）。</summary>
    public static TimerAdvanceResult CompleteWithTick(TimerMode mode, TimerMode completedMode)
        => new(0, 0, mode, true, true, completedMode);
}
