using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Views;

public partial class TaskManagerWindow : Window
{
    private readonly StorageService _storageService;
    private List<TaskItem> _tasks;

    public TaskManagerWindow()
    {
        InitializeComponent();
        _storageService = new StorageService();
        LoadTasks();
    }

    private void LoadTasks()
    {
        _tasks = _storageService.LoadTasks();
        TaskListBox.ItemsSource = _tasks;
        CategoryComboBox.SelectedIndex = 0;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var taskName = NewTaskNameTextBox.Text.Trim();
        var category = CategoryComboBox.SelectedItem as ComboBoxItem;
        
        if (string.IsNullOrEmpty(taskName))
        {
            MessageBox.Show("请输入任务名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var newTask = new TaskItem
        {
            Name = taskName,
            Category = category?.Content.ToString() ?? "其他",
            Color = GetCategoryColor(category?.Content.ToString() ?? "其他"),
            CreatedAt = DateTime.Now
        };
        
        _tasks.Add(newTask);
        _storageService.SaveTasks(_tasks);
        
        TaskListBox.ItemsSource = null;
        TaskListBox.ItemsSource = _tasks;
        
        NewTaskNameTextBox.Text = string.Empty;
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var taskId = button?.Tag as string;
        
        if (string.IsNullOrEmpty(taskId)) return;
        
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            var result = MessageBox.Show($"确定要删除任务「{task.Name}」吗？", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _tasks.Remove(task);
                _storageService.SaveTasks(_tasks);
                
                TaskListBox.ItemsSource = null;
                TaskListBox.ItemsSource = _tasks;
            }
        }
    }

    private string GetCategoryColor(string category)
    {
        return category switch
        {
            "数学" => "#3B82F6",
            "英语" => "#10B981",
            "政治" => "#EF4444",
            "专业课" => "#8B5CF6",
            _ => "#6B7280"
        };
    }
}