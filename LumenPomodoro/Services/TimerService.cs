using System.Diagnostics;
using System.Windows.Threading;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

public class TimerService : ITimerService
{
    private const int TimerIntervalMs = 250;

    private readonly DispatcherTimer _timer;
    private readonly TimerEngine _engine;

    public event EventHandler<TimerTickEventArgs>? TimerTick;
    public event EventHandler<TimerCompletedEventArgs>? TimerCompleted;
    public event EventHandler<TimerModeChangedEventArgs>? ModeChanged;

    public TimerMode CurrentMode => _engine.CurrentMode;
    public bool IsRunning => _engine.IsRunning;
    public bool IsPaused => _engine.IsPaused;
    public int RemainingSeconds => _engine.RemainingSeconds;
    public int TotalSeconds => _engine.TotalSeconds;

    public TimerService()
    {
        // 使用250ms检查频率，平衡精度和功耗
        // 实际tick对齐由 TimerEngine 通过 _nextTickTime 控制，避免UI线程繁忙导致的累积误差
        _engine = new TimerEngine();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimerIntervalMs) };
        _timer.Tick += Timer_Tick;
    }

    public void StartFocus(int minutes)
    {
        if (_engine.IsRunning) return;
        var prev = _engine.CurrentMode;
        _engine.StartFocus(minutes, DateTime.UtcNow);
        _timer.Start();

        Log.Information("开始专注 {Minutes} 分钟", minutes);
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(prev, _engine.CurrentMode));
        TimerTick?.Invoke(this, new TimerTickEventArgs(_engine.RemainingSeconds, _engine.TotalSeconds, _engine.CurrentMode));
    }

    public void StartBreak(int minutes)
    {
        if (_engine.IsRunning) return;
        var prev = _engine.CurrentMode;
        _engine.StartBreak(minutes, DateTime.UtcNow);
        _timer.Start();

        Log.Information("开始休息 {Minutes} 分钟", minutes);
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(prev, _engine.CurrentMode));
        TimerTick?.Invoke(this, new TimerTickEventArgs(_engine.RemainingSeconds, _engine.TotalSeconds, _engine.CurrentMode));
    }

    public void Pause()
    {
        var prev = _engine.CurrentMode;
        if (_engine.Pause(DateTime.UtcNow))
        {
            _timer.Stop();
            Log.Debug("计时器暂停，之前模式: {Mode}", prev.ToString());
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(prev, _engine.CurrentMode));
        }
    }

    public void Resume()
    {
        var prev = _engine.CurrentMode;
        if (_engine.Resume(DateTime.UtcNow))
        {
            _timer.Start();
            Log.Debug("计时器恢复，模式: {Mode}", _engine.CurrentMode);
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(prev, _engine.CurrentMode));
        }
    }

    public void Reset()
    {
        var prev = _engine.CurrentMode;
        _engine.Reset(DateTime.UtcNow);
        _timer.Stop();

        Log.Debug("计时器重置，之前模式: {Mode}", prev);
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(prev, TimerMode.Idle));
        TimerTick?.Invoke(this, new TimerTickEventArgs(0, 0, TimerMode.Idle));
    }

    public void Stop()
    {
        var prev = _engine.CurrentMode;
        _engine.Stop(DateTime.UtcNow);
        _timer.Stop();
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(prev, TimerMode.Idle));
    }

    public void CorrectAfterWake()
    {
        var r = _engine.ApplyWakeCorrection(DateTime.UtcNow);
        if (r.ShouldTick)
        {
            TimerTick?.Invoke(this, new TimerTickEventArgs(r.RemainingSeconds, r.TotalSeconds, r.Mode));
        }

        if (r.ShouldComplete)
        {
            Log.Information("休眠唤醒后计时器完成，模式: {Mode}", r.CompletedMode);
            TimerCompleted?.Invoke(this, new TimerCompletedEventArgs(r.CompletedMode));
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(r.CompletedMode, TimerMode.Idle));
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var r = _engine.Advance(DateTime.UtcNow);
        if (r.ShouldTick)
        {
            TimerTick?.Invoke(this, new TimerTickEventArgs(r.RemainingSeconds, r.TotalSeconds, r.Mode));
        }

        if (r.ShouldComplete)
        {
            Log.Information("计时器自然完成，模式: {Mode}", r.CompletedMode);
            TimerCompleted?.Invoke(this, new TimerCompletedEventArgs(r.CompletedMode));
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(r.CompletedMode, TimerMode.Idle));
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
