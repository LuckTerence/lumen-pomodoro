using LumenPomodoro.Models;

namespace LumenPomodoro.Services;

public class TimerService
{
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new object();
    private int _remainingSeconds;
    private int _totalSeconds;
    private TimerMode _currentMode;
    private TimerMode _modeBeforePause;
    private bool _isPaused;
    private bool _isRunning;
    
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
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += Timer_Elapsed;
        _timer.AutoReset = true;
        _currentMode = TimerMode.Idle;
    }

    public void StartFocus(int minutes)
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _totalSeconds = minutes * 60;
            _remainingSeconds = _totalSeconds;
            _currentMode = TimerMode.Focus;
            _isRunning = true;
            _isPaused = false;
            _timer.Start();
        }
        
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(TimerMode.Idle, _currentMode));
        TimerTick?.Invoke(this, new TimerTickEventArgs(_remainingSeconds, _totalSeconds, _currentMode));
    }

    public void StartBreak(int minutes)
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _totalSeconds = minutes * 60;
            _remainingSeconds = _totalSeconds;
            _currentMode = TimerMode.Break;
            _isRunning = true;
            _isPaused = false;
            _timer.Start();
        }
        
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(TimerMode.Idle, _currentMode));
        TimerTick?.Invoke(this, new TimerTickEventArgs(_remainingSeconds, _totalSeconds, _currentMode));
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_isRunning && !_isPaused)
            {
                _isPaused = true;
                TimerMode oldMode = _currentMode;
                _modeBeforePause = _currentMode;
                _currentMode = TimerMode.Paused;
                _timer.Stop();
                ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(oldMode, TimerMode.Paused));
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_isRunning && _isPaused)
            {
                _isPaused = false;
                TimerMode restoredMode = _modeBeforePause;
                _currentMode = restoredMode;
                _timer.Start();
                ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(TimerMode.Paused, restoredMode));
            }
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
        }
        
        ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(oldMode, TimerMode.Idle));
        TimerTick?.Invoke(this, new TimerTickEventArgs(0, 0, TimerMode.Idle));
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer.Stop();
            _isRunning = false;
            _isPaused = false;
        }
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        bool shouldComplete = false;
        TimerMode completedMode = TimerMode.Idle;
        int remaining, total;
        TimerMode mode;
        
        lock (_lock)
        {
            if (_isPaused) return;
            
            _remainingSeconds--;
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
            TimerCompleted?.Invoke(this, new TimerCompletedEventArgs(completedMode));
            ModeChanged?.Invoke(this, new TimerModeChangedEventArgs(completedMode, TimerMode.Idle));
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

public enum TimerMode
{
    Idle,
    Focus,
    Break,
    Paused
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
