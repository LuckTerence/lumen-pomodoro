using System;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;

namespace LumenPomodoro.ViewModels;

/// <summary>
/// 封装与 ITimerService 的交互、tick 更新、会话生命周期、唤醒补偿响应。
/// </summary>
public class TimerController : IDisposable
{
    private readonly ITimerService _timerService;
    private FocusSession? _currentSession;
    private bool _disposed;

    public event Action<int, int>? TickUpdated;
    public event Action<FocusSession>? FocusCompleted;
    public event Action? BreakCompleted;
    public event Action<TimerMode>? ModeChanged;

    public TimerMode CurrentMode => _timerService.CurrentMode;

    public TimerController(ITimerService timerService)
    {
        _timerService = timerService;
        _timerService.TimerTick += OnTimerTick;
        _timerService.TimerCompleted += OnTimerCompleted;
        _timerService.ModeChanged += OnTimerModeChanged;
    }

    public FocusSession StartFocus(TaskItem selectedTask, Settings settings)
    {
        var session = new FocusSession
        {
            TaskId = selectedTask.Id,
            TaskName = selectedTask.Name,
            StartTime = DateTime.Now,
            FocusMinutes = settings.WorkMinutes
        };
        _currentSession = session;
        settings.LastSelectedTaskId = selectedTask.Id;
        _timerService.StartFocus(settings.WorkMinutes);
        return session;
    }

    public void PauseFocus() => _timerService.Pause();
    public void ResumeFocus() => _timerService.Resume();

    public void ResetFocus()
    {
        _timerService.Reset();
        _currentSession = null;
    }

    public FocusSession? CompleteAndClearSession()
    {
        if (_currentSession != null && !_currentSession.Completed)
        {
            _currentSession.EndTime = DateTime.Now;
            _currentSession.Completed = true;
        }
        var session = _currentSession;
        _currentSession = null;
        return session;
    }

    public FocusSession? AbandonIncompleteSession()
    {
        if (_currentSession != null && !_currentSession.Completed)
        {
            _currentSession.EndTime = DateTime.Now;
            _currentSession.Completed = false;
        }
        var session = _currentSession;
        _currentSession = null;
        return session;
    }

    public void StartBreak(bool isLongBreak, Settings settings)
    {
        int breakMinutes = isLongBreak ? settings.LongBreakMinutes : settings.ShortBreakMinutes;
        _timerService.StartBreak(breakMinutes);
    }

    public void EndBreak() => _timerService.Reset();
    public void SkipBreak() => _timerService.Reset();
    public void CorrectAfterWake() => _timerService.CorrectAfterWake();

    public int AdjustWorkMinutes(int delta, Settings settings)
    {
        var raw = settings.WorkMinutes + delta;
        var rounded = (int)(Math.Round(raw / 5.0) * 5);
        var newVal = Math.Clamp(rounded, 1, 120);
        if (newVal == settings.WorkMinutes) return settings.WorkMinutes;
        settings.WorkMinutes = newVal;
        return newVal;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timerService.TimerTick -= OnTimerTick;
        _timerService.TimerCompleted -= OnTimerCompleted;
        _timerService.ModeChanged -= OnTimerModeChanged;
    }

    private void OnTimerTick(object? sender, TimerTickEventArgs e)
    {
        TickUpdated?.Invoke(e.RemainingSeconds, e.TotalSeconds);
    }

    private void OnTimerCompleted(object? sender, TimerCompletedEventArgs e)
    {
        if (e.CompletedMode == TimerMode.Focus)
        {
            var session = CompleteAndClearSession();
            if (session != null)
                FocusCompleted?.Invoke(session);
        }
        else if (e.CompletedMode == TimerMode.Break)
        {
            BreakCompleted?.Invoke();
        }
    }

    private void OnTimerModeChanged(object? sender, TimerModeChangedEventArgs e)
    {
        ModeChanged?.Invoke(e.NewMode);
    }
}
