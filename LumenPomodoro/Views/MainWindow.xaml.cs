using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TrayService _trayService;
    private readonly CameraService _cameraService;
    private readonly StorageService _storageService;

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
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = _storageService.LoadSettings();
        if (settings.AnimationEnabled)
        {
            ApplyFadeInAnimation();
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
        Opacity = 0;
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(0.3)
        };
        BeginAnimation(OpacityProperty, fadeIn);
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
        
        _viewModel.RefreshStats();
    }

    private void ManageTasksButton_Click(object sender, RoutedEventArgs e)
    {
        var taskWindow = new TaskManagerWindow();
        taskWindow.ShowDialog();
        
        _viewModel.UpdateTasks(_storageService.LoadTasks());
    }

    protected override void OnClosed(EventArgs e)
    {
        _ = _cameraService.StopCameraAsync();
        _trayService.Dispose();
        base.OnClosed(e);
    }
}