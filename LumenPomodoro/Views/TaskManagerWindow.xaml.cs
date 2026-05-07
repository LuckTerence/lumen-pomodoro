using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Views;

public partial class TaskManagerWindow : Window
{
    private readonly StorageService _storageService;
    private List<TaskItem> _tasks = new();

    public TaskManagerWindow(StorageService storageService)
    {
        InitializeComponent();
        _storageService = storageService;
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

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var taskId = button?.Tag as string;
        
        if (string.IsNullOrEmpty(taskId)) return;
        
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        var dialog = new Window
        {
            Title = "编辑任务",
            Width = 320,
            Height = 160,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var border = new System.Windows.Controls.Border
        {
            Background = (System.Windows.Media.Brush)FindResource("GlassBackgroundBrush"),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(24)
        };

        var panel = new System.Windows.Controls.StackPanel();

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "请输入新的任务名称：",
            FontSize = 14,
            Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(label);

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = task.Name,
            FontSize = 14,
            Background = (System.Windows.Media.Brush)FindResource("ControlBackgroundBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush"),
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(0)
        };
        textBox.Loaded += (s, args) => textBox.SelectAll();
        panel.Children.Add(textBox);

        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelBtn = new System.Windows.Controls.Button
        {
            Content = "取消",
            Background = (System.Windows.Media.Brush)FindResource("ControlBackgroundBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush"),
            FontSize = 14,
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelBtn.Click += (s, args) => { dialog.DialogResult = false; dialog.Close(); };
        btnPanel.Children.Add(cancelBtn);

        var saveBtn = new System.Windows.Controls.Button
        {
            Content = "保存",
            Background = (System.Windows.Media.Brush)FindResource("PrimaryBrush"),
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14,
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        saveBtn.Click += (s, args) => { dialog.DialogResult = true; dialog.Close(); };
        btnPanel.Children.Add(saveBtn);
        panel.Children.Add(btnPanel);

        border.Child = panel;
        dialog.Content = border;

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text) && textBox.Text != task.Name)
        {
            task.Name = textBox.Text.Trim();
            _storageService.SaveTasks(_tasks);
            
            TaskListBox.ItemsSource = null;
            TaskListBox.ItemsSource = _tasks;
        }
    }

    private string GetCategoryColor(string category)
    {
        return TaskCategories.GetCategoryColor(category);
    }
}