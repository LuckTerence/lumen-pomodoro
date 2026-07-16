using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public class ColorOption
{
    public string Name { get; set; } = string.Empty;
    public Color Color { get; set; }
}

public partial class TasksPage : Page
{
    private readonly TasksViewModel _viewModel;
    private static readonly ColorOption[] _colorOptions = new[]
    {
        new ColorOption { Name = "蓝色", Color = (Color)ColorConverter.ConvertFromString("#3B82F6")! },
        new ColorOption { Name = "绿色", Color = (Color)ColorConverter.ConvertFromString("#10B981")! },
        new ColorOption { Name = "红色", Color = (Color)ColorConverter.ConvertFromString("#EF4444")! },
        new ColorOption { Name = "紫色", Color = (Color)ColorConverter.ConvertFromString("#8B5CF6")! },
        new ColorOption { Name = "灰色", Color = (Color)ColorConverter.ConvertFromString("#6B7280")! },
        new ColorOption { Name = "橙色", Color = (Color)ColorConverter.ConvertFromString("#F59E0B")! },
        new ColorOption { Name = "深橙", Color = (Color)ColorConverter.ConvertFromString("#F97316")! },
        new ColorOption { Name = "青色", Color = (Color)ColorConverter.ConvertFromString("#06B6D4")! },
    };

    public TasksPage(TasksViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += TasksPage_Loaded;
        ColorComboBox.ItemTemplate = CreateColorTemplate();
        ColorComboBox.ItemsSource = _colorOptions;
    }

    private static DataTemplate CreateColorTemplate()
    {
        var template = new DataTemplate();
        var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
        stackPanel.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);

        var ellipse = new FrameworkElementFactory(typeof(Ellipse));
        ellipse.SetValue(Ellipse.WidthProperty, 14.0);
        ellipse.SetValue(Ellipse.HeightProperty, 14.0);
        ellipse.SetValue(Ellipse.VerticalAlignmentProperty, VerticalAlignment.Center);
        ellipse.SetValue(Ellipse.StrokeThicknessProperty, 0.0);
        ellipse.SetValue(Ellipse.MarginProperty, new Thickness(0, 0, 8, 0));
        ellipse.SetBinding(Ellipse.FillProperty, new System.Windows.Data.Binding("Color") { Converter = new ColorToBrushConverter() });

        var textBlock = new FrameworkElementFactory(typeof(TextBlock));
        textBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        textBlock.SetValue(TextBlock.FontSizeProperty, 13.0);
        textBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));

        stackPanel.AppendChild(ellipse);
        stackPanel.AppendChild(textBlock);
        template.VisualTree = stackPanel;
        return template;
    }

    private void TasksPage_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadTasks();
        if (ColorComboBox.SelectedIndex < 0)
            ColorComboBox.SelectedIndex = 0;
    }

    public void Refresh()
    {
        _viewModel.LoadTasks();
    }

    private void SelectTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var taskId = element.Tag?.ToString();
        if (taskId == null) return;
        _viewModel.SelectTask(taskId);
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.NewTaskName = NewTaskNameBox.Text;
        if (ColorComboBox.SelectedItem is ColorOption colorOption)
        {
            _viewModel.SelectedColor = $"#{colorOption.Color.R:X2}{colorOption.Color.G:X2}{colorOption.Color.B:X2}";
        }
        _viewModel.AddTask();
        NewTaskNameBox.Text = string.Empty;
        ColorComboBox.SelectedIndex = 0;
    }

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var taskId = element.Tag?.ToString();
        if (taskId == null) return;

        var task = _viewModel.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        var dialog = new System.Windows.Window
        {
            Title = "编辑任务",
            Width = 320,
            Height = 160,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            ResizeMode = System.Windows.ResizeMode.NoResize,
            WindowStyle = System.Windows.WindowStyle.ToolWindow
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "请输入新的任务名称：" });
        var textBox = new System.Windows.Controls.TextBox { Text = task.Name, Margin = new Thickness(0, 8, 0, 16) };
        panel.Children.Add(textBox);

        var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var okButton = new System.Windows.Controls.Button { Content = "确定", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelButton = new System.Windows.Controls.Button { Content = "取消", Width = 80, IsCancel = true };
        okButton.Click += (s, _) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        textBox.Focus();
        textBox.SelectAll();

        if (dialog.ShowDialog() == true)
        {
            _viewModel.FinishEdit(taskId, textBox.Text);
        }
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

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = MessageBox.Show(owner,
            "将恢复所有考试科目默认预设任务。\n你的自建任务会保留，不会被删除。\n\n确定要恢复吗？",
            "恢复默认任务",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.RestoreDefaults();
            MessageBox.Show(owner, "默认任务已恢复", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

public class ColorToBrushConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is Color color)
            return new SolidColorBrush(color);
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
