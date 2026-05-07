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

public class StatsViewModel : INotifyPropertyChanged
{
    private readonly StorageService _storageService;

    private int _completedPomodoros;
    private int _totalFocusMinutes;
    private ObservableCollection<TaskStatItem> _taskStats = new();

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

    public StatsViewModel(StorageService storageService)
    {
        _storageService = storageService;
    }

    public void Refresh()
    {
        var stats = _storageService.GetTodayStats();

        CompletedPomodoros = stats.CompletedPomodoros;
        TotalFocusMinutes = stats.TotalFocusMinutes;

        var tasks = _storageService.LoadTasks();
        var taskColorMap = tasks.ToDictionary(t => t.Name, t => t.Color);

        var maxCount = stats.TaskStats.Values.DefaultIfEmpty(0).Max();
        if (maxCount == 0) maxCount = 1;

        var items = new ObservableCollection<TaskStatItem>();
        foreach (var kv in stats.TaskStats.OrderByDescending(s => s.Value))
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
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
