using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TrayService? _trayService;

    private bool _isFirstLoad = true;

    public MainWindow()
    {
        InitializeComponent();

        var storageService = ((App)Application.Current).StorageService;
        _viewModel = new MainViewModel(storageService);

        DataContext = _viewModel;

        if (_viewModel.AppSettings.TrayEnabled)
        {
            _trayService = new TrayService(_viewModel, _viewModel.CameraService, _viewModel.StorageService);
            _trayService.AttachToWindow(this);

            _viewModel.TrayMenuNeedsUpdate += () =>
            {
                Dispatcher.BeginInvoke(() => _trayService.UpdateMenuState());
            };
        }

        Loaded += MainWindow_Loaded;
        IsVisibleChanged += MainWindow_IsVisibleChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.AppSettings.AnimationEnabled)
        {
            ApplyFadeInAnimation();
        }
        _isFirstLoad = false;
    }

    private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && !_isFirstLoad)
        {
            if (_viewModel.AppSettings.AnimationEnabled)
            {
                Opacity = 0;
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.25),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(OpacityProperty, fadeIn);
            }
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !HasInteractiveParent(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private static bool HasInteractiveParent(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase || source is TextBox || source is ComboBox || source is ProgressBar || source is ToggleButton)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void ApplyFadeInAnimation()
    {
        var border = (Border?)FindName("MainBorder");
        if (border == null)
        {
            Opacity = 0;
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };
            BeginAnimation(OpacityProperty, fadeIn);
            return;
        }

        border.Opacity = 0;
        border.RenderTransform = new TranslateTransform(0, 20);

        var opacityAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(0.4),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var translateYAnim = new DoubleAnimation
        {
            From = 20,
            To = 0,
            Duration = TimeSpan.FromSeconds(0.4),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        border.BeginAnimation(OpacityProperty, opacityAnim);
        border.RenderTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _viewModel.AppSettings;
        if (settings.TrayEnabled && settings.CloseToTray && _trayService != null)
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
        _viewModel.ToggleSettings();

        if (_viewModel.IsSettingsVisible)
        {
            TimerView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            Height = 680;
        }
        else
        {
            SettingsView.Visibility = Visibility.Collapsed;
            TimerView.Visibility = Visibility.Visible;
            Height = 520;
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveAndCloseSettings();
        SettingsView.Visibility = Visibility.Collapsed;
        TimerView.Visibility = Visibility.Visible;
        Height = 520;
    }

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseSettings(discard: true);
        SettingsView.Visibility = Visibility.Collapsed;
        TimerView.Visibility = Visibility.Visible;
        Height = 520;
    }

    private void ManageTasksButton_Click(object sender, RoutedEventArgs e)
    {
        var taskWindow = new TaskManagerWindow(_viewModel.StorageService);
        taskWindow.ShowDialog();

        _viewModel.UpdateTasks(_viewModel.StorageService.LoadTasks());
    }

    private void StatsPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var statsWindow = new StatsWindow(_viewModel.StorageService)
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
        _viewModel.Dispose();
        _trayService?.Dispose();
        base.OnClosed(e);
    }
}
