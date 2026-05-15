using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface ITimerService : IDisposable
{
    TimerMode CurrentMode { get; }
    bool IsRunning { get; }
    bool IsPaused { get; }
    int RemainingSeconds { get; }
    int TotalSeconds { get; }
    
    void StartFocus(int minutes);
    void StartBreak(int minutes);
    void Pause();
    void Resume();
    void Reset();
    void Stop();
    void CorrectAfterWake();
    
    event EventHandler<TimerTickEventArgs>? TimerTick;
    event EventHandler<TimerCompletedEventArgs>? TimerCompleted;
    event EventHandler<ModeChangedEventArgs>? ModeChanged;
}

public class TimerTickEventArgs : EventArgs
{
    public int RemainingSeconds { get; }
    public int TotalSeconds { get; }
    public TimerTickEventArgs(int remainingSeconds, int totalSeconds)
    {
        RemainingSeconds = remainingSeconds;
        TotalSeconds = totalSeconds;
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

public class ModeChangedEventArgs : EventArgs
{
    public TimerMode NewMode { get; }
    public ModeChangedEventArgs(TimerMode newMode)
    {
        NewMode = newMode;
    }
}
