using System.Diagnostics;
using System.Windows.Threading;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

public class TimerService : ITimerService
{
    private readonly DispatcherTimer _timer;
    private readonly object _lock = new object();
    private int _remainingSeconds;
    private int _totalSeconds;
    private TimerMode _currentMode;
    private TimerMode _modeBeforePause;
    private bool _isPaused;
    private bool _isRunning;
    private DateTime _lastTickTime;
    private DateTime _nextTickTime; // 下一个tick的预期时间，用于精度补偿

    public event EventHandler<TimerTickEventArgs>? TimerTick;
    public event EventHandler<TimerCompletedEventArgs>? TimerCompleted;
    public event EventHandler<TimerModeChangedEventArgs>? ModeChanged;

    public TimerMode CurrentMode => _currentMode;
    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;
    public int RemainingSeconds => _remainingSeconds;
    public int TotalSeconds => _totalSeconds;

    public TimerService()
    {
        // 使用250ms检查频率，平衡精度和功耗
        // 实际tick对齐通过_nextTickTime控制，避免UI线程繁忙导致的累积误差
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += Timer_Tick;
        _currentMode = TimerMode.Idle;
        _lastTickTime = DateTime.UtcNow;
        _nextTickTime = DateTime.UtcNow;
    }

    public void StartFocus(int minutes)
    {
        int remaining, total;
        lock (_lock)
        {
            if (_isRunning) return;
            _totalSeconds = minutes * 60;
            _remainingSeconds = _totalSeconds;
            _currentMode = TimerMode.Focus;
            _isRunning = true;
            _isPaused = false;
            _lastTickTime = DateTime.UtcNow;
            _nextTickTime = _lastTickTime.AddSeconds(1); // 设置第一个tick时间
            _timer.Start();
            remaining = _remainingSeconds;
            total = _totalSeconds;
        }

        Log.Information("开始专注 {Minutes} 分钟", minutes);
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(TimerMode.Idle, TimerMode.Focus));
        TimerTick?.Invoke(this, new TimerTickEventArgs(remaining, total, TimerMode.Focus));
    }

    public void StartBreak(int minutes)
    {
        int remaining, total;
        lock (_lock)
        {
            if (_isRunning) return;
            _totalSeconds = minutes * 60;
            _remainingSeconds = _totalSeconds;
            _currentMode = TimerMode.Break;
            _isRunning = true;
            _isPaused = false;
            _lastTickTime = DateTime.UtcNow;
            _nextTickTime = _lastTickTime.AddSeconds(1);
            _timer.Start();
            remaining = _remainingSeconds;
            total = _totalSeconds;
        }

        Log.Information("开始休息 {Minutes} 分钟", minutes);
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(TimerMode.Idle, TimerMode.Break));
        TimerTick?.Invoke(this, new TimerTickEventArgs(remaining, total, TimerMode.Break));
    }

    public void Pause()
    {
        TimerMode oldMode = TimerMode.Idle;
        bool shouldInvoke = false;
        lock (_lock)
        {
            if (_isRunning && !_isPaused)
            {
                _isPaused = true;
                oldMode = _currentMode;
                _modeBeforePause = _currentMode;
                _currentMode = TimerMode.Paused;
                _timer.Stop();
                shouldInvoke = true;
            }
        }

        if (shouldInvoke)
        {
            Log.Debug("计时器暂停，之前模式: {Mode}", oldMode);
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(oldMode, TimerMode.Paused));
        }
    }

    public void Resume()
    {
        TimerMode restoredMode = TimerMode.Idle;
        bool shouldInvoke = false;
        lock (_lock)
        {
            if (_isRunning && _isPaused)
            {
                _isPaused = false;
                restoredMode = _modeBeforePause;
                _currentMode = restoredMode;
                _lastTickTime = DateTime.UtcNow;
                _nextTickTime = _lastTickTime.AddSeconds(1);
                _timer.Start();
                shouldInvoke = true;
            }
        }

        if (shouldInvoke)
        {
            Log.Debug("计时器恢复，模式: {Mode}", restoredMode);
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(TimerMode.Paused, restoredMode));
        }
    }

    public void Reset()
    {
        TimerMode oldMode;
        lock (_lock)
        {
            _timer.Stop();
            _isRunning = false;
            _isPaused = false;
            _remainingSeconds = 0;
            _totalSeconds = 0;
            oldMode = _currentMode;
            _currentMode = TimerMode.Idle;
            _nextTickTime = DateTime.UtcNow;
        }

        Log.Debug("计时器重置，之前模式: {Mode}", oldMode);
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(oldMode, TimerMode.Idle));
        TimerTick?.Invoke(this, new TimerTickEventArgs(0, 0, TimerMode.Idle));
    }

    public void Stop()
    {
        TimerMode oldMode;
        lock (_lock)
        {
            oldMode = _currentMode;
            _timer.Stop();
            _isRunning = false;
            _isPaused = false;
            _nextTickTime = DateTime.UtcNow;
        }
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(oldMode, TimerMode.Idle));
    }

    public void CorrectAfterWake()
    {
        int remaining, total;
        TimerMode mode;
        bool shouldComplete = false;
        TimerMode completedMode = TimerMode.Idle;

        lock (_lock)
        {
            if (!_isRunning || _isPaused) return;

            var elapsed = (DateTime.UtcNow - _lastTickTime).TotalSeconds;
            // 小于2秒忽略（避免系统延迟误判）
            // 大于24小时忽略（可能是时钟调整，而非实际睡眠）
            if (elapsed < 2 || elapsed > 86400) return;

            // 扣除所有错过的秒数（系统休眠期间计时器停止运行）
            // elapsed可能包含小数部分，转换为int会截断，但误差小于1秒可接受
            _remainingSeconds = Math.Max(0, _remainingSeconds - (int)elapsed);
            _lastTickTime = DateTime.UtcNow;
            _nextTickTime = _lastTickTime.AddSeconds(1);
            remaining = _remainingSeconds;
            total = _totalSeconds;
            mode = _currentMode;

            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                _isRunning = false;
                completedMode = _currentMode;
                _currentMode = TimerMode.Idle;
                shouldComplete = true;
            }
        }

        TimerTick?.Invoke(this, new TimerTickEventArgs(remaining, total, mode));

        if (shouldComplete)
        {
            Log.Information("休眠唤醒后计时器完成，模式: {Mode}", completedMode);
            TimerCompleted?.Invoke(this, new TimerCompletedEventArgs(completedMode));
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(completedMode, TimerMode.Idle));
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        bool shouldComplete = false;
        TimerMode completedMode = TimerMode.Idle;
        int remaining, total;
        TimerMode mode;

        lock (_lock)
        {
            if (_isPaused) return;

            var now = DateTime.UtcNow;
            // 如果还没到下一个tick时间，直接返回
            if (now < _nextTickTime) return;

            // 计算需要触发的tick次数（通常为1，补偿延迟时可能>1）
            int ticksToProcess = 0;
            while (_nextTickTime <= now && ticksToProcess < 10) // 最多补偿10个tick，防止长时间卡顿一次性扣除过多
            {
                ticksToProcess++;
                _nextTickTime = _nextTickTime.AddSeconds(1);
            }

            _remainingSeconds = Math.Max(0, _remainingSeconds - ticksToProcess);
            _lastTickTime = now;
            remaining = _remainingSeconds;
            total = _totalSeconds;
            mode = _currentMode;

            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                _isRunning = false;
                completedMode = _currentMode;
                _currentMode = TimerMode.Idle;
                shouldComplete = true;
            }
        }

        if (!shouldComplete)
        {
            TimerTick?.Invoke(this, new TimerTickEventArgs(remaining, total, mode));
        }

        if (shouldComplete)
        {
            Log.Information("计时器自然完成，模式: {Mode}", completedMode);
            TimerCompleted?.Invoke(this, new TimerCompletedEventArgs(completedMode));
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(completedMode, TimerMode.Idle));
        }
    }

    public void Dispose()
    {
        _timer.Tick -= Timer_Tick;
        _timer.Stop();
        TimerTick = null;
        TimerCompleted = null;
        ModeChanged = null;
    }
}

public class TimerTickEventArgs : EventArgs
{
    public int RemainingSeconds { get; }
    public int TotalSeconds { get; }
    public TimerMode Mode { get; }

    public TimerTickEventArgs(int remainingSeconds, int totalSeconds, TimerMode mode)
    {
        RemainingSeconds = remainingSeconds;
        TotalSeconds = totalSeconds;
        Mode = mode;
    }
}

public class TimerCompletedEventArgs : EventArgs
{
    public TimerMode CompletedMode { get; }

    public TimerCompletedEventArgs(TimerMode completedMode)
    {
        CompletedMode = completedMode;
    }
}

public class TimerModeChangedEventArgs : EventArgs
{
    public TimerMode OldMode { get; }
    public TimerMode NewMode { get; }

    public TimerModeChangedEventArgs(TimerMode oldMode, TimerMode newMode)
    {
        OldMode = oldMode;
        NewMode = newMode;
    }
}
