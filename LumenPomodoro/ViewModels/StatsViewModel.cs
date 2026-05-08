using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public class TaskStatItem
{
    public string TaskName { get; set; } = string.Empty;
    public string Color { get; set; } = "#6B7280";
    public int Count { get; set; }
    public double BarRatio { get; set; }
}

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
    private ObservableCollection<TaskStatItem> _taskStats = new();
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

    public ObservableCollection<TaskStatItem> TaskStats
    {
        get => _taskStats;
        set { if (!ReferenceEquals(_taskStats, value)) { _taskStats = value; OnPropertyChanged(); } }
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
        var tasks = _storageService.LoadTasks();
        var taskColorMap = tasks.ToDictionary(t => t.Name, t => t.Color);

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

        var taskCounts = new Dictionary<string, int>();
        foreach (var session in filteredSessions)
        {
            if (!taskCounts.ContainsKey(session.TaskName))
                taskCounts[session.TaskName] = 0;
            taskCounts[session.TaskName]++;
        }

        var maxCount = taskCounts.Values.DefaultIfEmpty(0).Max();
        if (maxCount == 0) maxCount = 1;

        var items = new ObservableCollection<TaskStatItem>();
        foreach (var kv in taskCounts.OrderByDescending(s => s.Value))
        {
            items.Add(new TaskStatItem
            {
                TaskName = kv.Key,
                Color = taskColorMap.GetValueOrDefault(kv.Key, "#6B7280"),
                Count = kv.Value,
                BarRatio = (double)kv.Value / maxCount
            });
        }

        TaskStats = items;

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
