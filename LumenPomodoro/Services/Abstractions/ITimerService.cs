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

    event EventHandler<TimerTickEventArgs> TimerTick;
    event EventHandler<TimerCompletedEventArgs> TimerCompleted;
    event EventHandler<TimerModeChangedEventArgs> ModeChanged;
}
