using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;

namespace LumenPomodoro.ViewModels;

public partial class TasksViewModel : ObservableObject
{
    private readonly IStorageService _storageService;

    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks = new();

    [ObservableProperty]
    private string _newTaskName = string.Empty;

    [ObservableProperty]
    private string _newTaskCategory = string.Empty;

    [ObservableProperty]
    private string _selectedColor = "#3B82F6";

    [ObservableProperty]
    private string? _editingTaskId;

    public static readonly string[] AvailableCategories = ["数学", "英语", "政治", "专业课", "其他", "行测", "申论", "教资", "法考", "CPA", "编程"];

    public event Action? TasksChanged;
    public event Action<TaskItem>? TaskSelected;

    public TasksViewModel(IStorageService storageService)
    {
        _storageService = storageService;
    }

    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;

        var task = new TaskItem
        {
            Name = NewTaskName.Trim(),
            Category = NewTaskCategory?.Trim() ?? string.Empty,
            Color = SelectedColor,
            CreatedAt = DateTime.Now
        };

        Tasks.Add(task);
        SaveAndNotify();
        NewTaskName = string.Empty;
        NewTaskCategory = string.Empty;
    }

    [RelayCommand]
    private void Delete(string taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        Tasks.Remove(task);
        SaveAndNotify();
    }

    [RelayCommand]
    private void Select(string taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
            TaskSelected?.Invoke(task);
    }

    public void LoadTasks()
    {
        var tasks = _storageService.LoadTasks();
        Tasks = new ObservableCollection<TaskItem>(tasks);
    }

    public void StartEdit(TaskItem task)
    {
        EditingTaskId = task.Id;
    }

    public void FinishEdit(string taskId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            EditingTaskId = null;
            return;
        }

        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.Name = newName.Trim();
            SaveAndNotify();
        }

        EditingTaskId = null;
    }

    public void RestoreDefaults()
    {
        _storageService.RestoreDefaultTasks();
        LoadTasks();
    }

    partial void OnSelectedColorChanged(string value)
    {
        var validated = ValidateColor(value);
        if (validated != _selectedColor)
        {
            _selectedColor = validated;
            OnPropertyChanged(nameof(SelectedColor));
        }
    }

    private static string ValidateColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return "#6B7280";
        if (!color.StartsWith("#")) color = "#" + color;
        if (color.Length == 4)
        {
            color = $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}";
        }
        if (color.Length != 7) return "#6B7280";
        return color.ToUpperInvariant();
    }

    private void SaveAndNotify()
    {
        _storageService.SaveTasks(Tasks.ToList());
        TasksChanged?.Invoke();
    }
}
