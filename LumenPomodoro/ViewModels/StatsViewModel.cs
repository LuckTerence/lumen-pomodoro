using System.ComponentModel;
using System.Runtime.CompilerServices;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public enum StatsPeriod
{
    Day,
    Week
}

public class StatsViewModel : INotifyPropertyChanged
{
    private readonly StorageService _storageService;

    private int _completedPomodoros;
    private int _totalFocusMinutes;
    private DateTime _currentDate = DateTime.Today;
    private StatsPeriod _currentPeriod = StatsPeriod.Day;
    private string _statsDateLabel = "今日统计";
    private bool _canGoNext;
    private bool _isDayPeriod = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int CompletedPomodoros
    {
        get => _completedPomodoros;
        set { if (_completedPomodoros != value) { _completedPomodoros = value; OnPropertyChanged(); } }
    }

    public int TotalFocusMinutes
    {
        get => _totalFocusMinutes;
        set { if (_totalFocusMinutes != value) { _totalFocusMinutes = value; OnPropertyChanged(); } }
    }

    public string StatsDateLabel
    {
        get => _statsDateLabel;
        set { if (_statsDateLabel != value) { _statsDateLabel = value; OnPropertyChanged(); } }
    }

    public bool CanGoNext
    {
        get => _canGoNext;
        set { if (_canGoNext != value) { _canGoNext = value; OnPropertyChanged(); } }
    }

    public bool IsDayPeriod
    {
        get => _isDayPeriod;
        set
        {
            if (_isDayPeriod != value)
            {
                _isDayPeriod = value;
                _currentPeriod = value ? StatsPeriod.Day : StatsPeriod.Week;
                OnPropertyChanged();
                LoadStatsForCurrentPeriod();
            }
        }
    }

    public StatsViewModel(StorageService storageService)
    {
        _storageService = storageService;
    }

    public void Refresh()
    {
        LoadStatsForCurrentPeriod();
    }

    public void ShiftDate(int direction)
    {
        if (_currentPeriod == StatsPeriod.Day)
        {
            var newDate = _currentDate.AddDays(direction);
            if (newDate > DateTime.Today) return;
            _currentDate = newDate;
        }
        else
        {
            var newDate = _currentDate.AddDays(direction * 7);
            var maxDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            if (newDate > maxDate) return;
            _currentDate = newDate;
        }
        LoadStatsForCurrentPeriod();
    }

    private void LoadStatsForCurrentPeriod()
    {
        var sessions = _storageService.LoadSessions();

        List<FocusSession> filteredSessions;

        if (_currentPeriod == StatsPeriod.Day)
        {
            filteredSessions = sessions
                .Where(s => s.Completed && s.EndTime.HasValue && s.EndTime.Value.Date == _currentDate.Date)
                .ToList();

            StatsDateLabel = _currentDate.Date == DateTime.Today
                ? "今日统计"
                : _currentDate.ToString("M月d日");
        }
        else
        {
            var weekStart = _currentDate.AddDays(-(int)_currentDate.DayOfWeek);
            var weekEnd = weekStart.AddDays(6);
            filteredSessions = sessions
                .Where(s => s.Completed && s.EndTime.HasValue && s.EndTime.Value.Date >= weekStart.Date && s.EndTime.Value.Date <= weekEnd.Date)
                .ToList();

            StatsDateLabel = $"{weekStart:M月d日}-{weekEnd:M月d日}";
        }

        CompletedPomodoros = filteredSessions.Count;
        TotalFocusMinutes = filteredSessions.Sum(s => s.FocusMinutes);

        if (_currentPeriod == StatsPeriod.Day)
            CanGoNext = _currentDate.Date < DateTime.Today;
        else
        {
            var maxDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            CanGoNext = _currentDate < maxDate;
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
