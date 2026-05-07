using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public class TasksViewModel : INotifyPropertyChanged
{
    private readonly StorageService _storageService;

    private ObservableCollection<TaskItem> _tasks = new();
    private string _newTaskName = string.Empty;
    private string _selectedCategory = TaskCategories.Categories.First();
    private string? _editingTaskId;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? TasksChanged;

    public ObservableCollection<TaskItem> Tasks
    {
        get => _tasks;
        set { if (!ReferenceEquals(_tasks, value)) { _tasks = value; OnPropertyChanged(); } }
    }

    public string NewTaskName
    {
        get => _newTaskName;
        set { if (_newTaskName != value) { _newTaskName = value; OnPropertyChanged(); } }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (_selectedCategory != value) { _selectedCategory = value; OnPropertyChanged(); } }
    }

    public string[] Categories => TaskCategories.Categories;

    public string? EditingTaskId
    {
        get => _editingTaskId;
        set { if (_editingTaskId != value) { _editingTaskId = value; OnPropertyChanged(); } }
    }

    public TasksViewModel(StorageService storageService)
    {
        _storageService = storageService;
    }

    public void LoadTasks()
    {
        var tasks = _storageService.LoadTasks();
        Tasks = new ObservableCollection<TaskItem>(tasks);
    }

    public void AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;

        var task = new TaskItem
        {
            Name = NewTaskName.Trim(),
            Category = SelectedCategory,
            Color = TaskCategories.GetCategoryColor(SelectedCategory),
            CreatedAt = DateTime.Now
        };

        Tasks.Add(task);
        SaveAndNotify();
        NewTaskName = string.Empty;
    }

    public void DeleteTask(string taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        Tasks.Remove(task);
        SaveAndNotify();
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

    private void SaveAndNotify()
    {
        _storageService.SaveTasks(Tasks.ToList());
        TasksChanged?.Invoke();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
