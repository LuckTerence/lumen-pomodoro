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
        new ColorOption { Name = "数学", Color = (Color)ColorConverter.ConvertFromString("#3B82F6")! },
        new ColorOption { Name = "英语", Color = (Color)ColorConverter.ConvertFromString("#10B981")! },
        new ColorOption { Name = "政治", Color = (Color)ColorConverter.ConvertFromString("#EF4444")! },
        new ColorOption { Name = "专业课", Color = (Color)ColorConverter.ConvertFromString("#8B5CF6")! },
        new ColorOption { Name = "其他", Color = (Color)ColorConverter.ConvertFromString("#6B7280")! },
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
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var ellipseFactory = new FrameworkElementFactory(typeof(Ellipse));
        ellipseFactory.SetValue(Ellipse.WidthProperty, 12.0);
        ellipseFactory.SetValue(Ellipse.HeightProperty, 12.0);
        ellipseFactory.SetValue(Ellipse.MarginProperty, new Thickness(0, 0, 6, 0));
        ellipseFactory.SetValue(Ellipse.VerticalAlignmentProperty, VerticalAlignment.Center);
        ellipseFactory.SetBinding(Ellipse.FillProperty, new System.Windows.Data.Binding("Color") { Converter = new ColorToBrushConverter() });
        factory.AppendChild(ellipseFactory);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        factory.AppendChild(textFactory);

        template.VisualTree = factory;
        return template;
    }

    private void TasksPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (ColorComboBox.SelectedIndex < 0)
            ColorComboBox.SelectedIndex = 0;
    }

    public void Refresh()
    {
        _viewModel.LoadTasks();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.NewTaskName = NewTaskNameBox.Text;
        if (CategoryComboBox.SelectedItem is string category)
        {
            _viewModel.SelectedCategory = category;
        }
        if (ColorComboBox.SelectedItem is ColorOption colorOption)
        {
            _viewModel.SelectedColor = $"#{colorOption.Color.R:X2}{colorOption.Color.G:X2}{colorOption.Color.B:X2}";
        }
        _viewModel.AddTask();
        NewTaskNameBox.Text = string.Empty;
        CategoryComboBox.SelectedIndex = 0;
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
