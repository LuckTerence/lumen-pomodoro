using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TrayService _trayService;
    private readonly CameraService _cameraService;
    private readonly StorageService _storageService;

    private bool _isFirstLoad = true;

    public MainWindow()
    {
        InitializeComponent();
        
        _cameraService = new CameraService();
        _storageService = new StorageService();
        _viewModel = new MainViewModel();
        _trayService = new TrayService(_viewModel, _cameraService, _storageService);
        
        DataContext = _viewModel;
        
        _trayService.AttachToWindow(this);
        
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        IsVisibleChanged += MainWindow_IsVisibleChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = _storageService.LoadSettings();
        if (settings.AnimationEnabled)
        {
            ApplyFadeInAnimation();
        }
        _isFirstLoad = false;
    }

    private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && !_isFirstLoad)
        {
            var settings = _storageService.LoadSettings();
            if (settings.AnimationEnabled)
            {
                Opacity = 0;
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.25),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                BeginAnimation(OpacityProperty, fadeIn);
            }
        }
    }

    private void MainWindow_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            var settings = _storageService.LoadSettings();
            if (settings.TrayEnabled && settings.CloseToTray)
            {
                Hide();
            }
        }
    }

    private void ApplyFadeInAnimation()
    {
        var border = (System.Windows.Controls.Border)FindVisualChild<System.Windows.Controls.Border>(this, b => b.Style?.ToString().Contains("GlassPanel") == true);
        if (border == null)
        {
            Opacity = 0;
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };
            BeginAnimation(OpacityProperty, fadeIn);
            return;
        }

        border.Opacity = 0;
        border.RenderTransform = new System.Windows.Media.TranslateTransform(0, 20);

        var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(0.4),
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        var translateYAnim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 20,
            To = 0,
            Duration = TimeSpan.FromSeconds(0.4),
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        border.BeginAnimation(OpacityProperty, opacityAnim);
        border.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateYAnim);
    }

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool> condition) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild && condition(typedChild))
                return typedChild;
            var result = FindVisualChild(child, condition);
            if (result != null) return result;
        }
        return null;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _storageService.LoadSettings();
        if (settings.TrayEnabled && settings.CloseToTray)
        {
            Hide();
            _trayService.ShowNotification("Lumen Pomodoro", "已最小化到托盘");
        }
        else
        {
            Close();
        }
    }

    private void StartFocusButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StartFocus();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PauseFocus();
    }

    private void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResumeFocus();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetFocus();
    }

    private void StartShortBreakButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StartBreak(false);
    }

    private void StartLongBreakButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StartBreak(true);
    }

    private void SkipBreakButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SkipBreak();
    }

    private void EndBreakButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.EndBreak();
    }

    private void StopCameraButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StopCameraAlert();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.ShowDialog();
        
        _viewModel.ReloadSettings();
        _viewModel.RefreshStats();
    }

    private void ManageTasksButton_Click(object sender, RoutedEventArgs e)
    {
        var taskWindow = new TaskManagerWindow();
        taskWindow.ShowDialog();
        
        _viewModel.UpdateTasks(_storageService.LoadTasks());
    }

    private void StatsPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var statsWindow = new StatsWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        statsWindow.ShowDialog();
    }

    public void RefreshTimerOnWake()
    {
        _viewModel.RefreshStats();
    }

    protected override void OnClosed(EventArgs e)
    {
        _ = _cameraService.StopCameraAsync();
        _trayService.Dispose();
        base.OnClosed(e);
    }
}