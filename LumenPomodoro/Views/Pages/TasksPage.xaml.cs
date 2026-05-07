using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class TasksPage : Page
{
    private readonly TasksViewModel _viewModel;

    public TasksPage(TasksViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void Refresh()
    {
        _viewModel.LoadTasks();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.NewTaskName = NewTaskNameBox.Text;
        if (CategoryComboBox.SelectedItem is ComboBoxItem item)
        {
            _viewModel.SelectedCategory = item.Content?.ToString() ?? "其他";
        }
        _viewModel.AddTask();
        NewTaskNameBox.Text = string.Empty;
        CategoryComboBox.SelectedIndex = 0;
    }

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var taskId = element.Tag?.ToString();
        if (taskId == null) return;

        var task = _viewModel.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        var newName = Microsoft.VisualBasic.Interaction.InputBox(
            "编辑任务名称", "编辑任务", task.Name);

        _viewModel.FinishEdit(taskId, newName);
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var taskId = element.Tag?.ToString();
        if (taskId == null) return;

        var result = MessageBox.Show("确定删除此任务？", "删除任务", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteTask(taskId);
        }
    }
}
