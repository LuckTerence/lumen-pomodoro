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
    private Storyboard? _breathingStoryboard;
    private Storyboard? _cameraBreathingStoryboard;
    private Storyboard? _pausedPulseStoryboard;

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

            _viewModel.NotificationRequested += (title, message) =>
            {
                Dispatcher.BeginInvoke(() => _trayService.ShowNotification(title, message));
            };
        }

        Loaded += MainWindow_Loaded;
        IsVisibleChanged += MainWindow_IsVisibleChanged;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsFocusCompleted):
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewModel.IsFocusCompleted)
                        StartBreathingAnimation();
                    else
                        StopBreathingAnimation();
                });
                break;
            case nameof(MainViewModel.IsCameraAlertActive):
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewModel.IsCameraAlertActive)
                        StartCameraBreathingAnimation();
                    else
                        StopCameraBreathingAnimation();
                });
                break;
            case nameof(MainViewModel.CurrentStatus):
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewModel.CurrentStatus == TimerMode.Paused)
                        StartPausedPulseAnimation();
                    else
                        StopPausedPulseAnimation();
                });
                break;
        }
    }

    private void StartBreathingAnimation()
    {
        if (!_viewModel.AppSettings.AnimationEnabled) return;
        StopBreathingAnimation();

        var opacityAnim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(3)
        };
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromPercent(0.5)));
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        Storyboard.SetTarget(opacityAnim, TimerText);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(TextBlock.OpacityProperty));

        _breathingStoryboard = new Storyboard();
        _breathingStoryboard.Children.Add(opacityAnim);
        _breathingStoryboard.Begin();
    }

    private void StopBreathingAnimation()
    {
        if (_breathingStoryboard == null) return;
        _breathingStoryboard.Stop();
        _breathingStoryboard = null;
        TimerText.Opacity = 1.0;
    }

    private void StartCameraBreathingAnimation()
    {
        if (!_viewModel.AppSettings.AnimationEnabled) return;
        StopCameraBreathingAnimation();

        var opacityAnim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(2)
        };
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.3, KeyTime.FromPercent(0.5)));
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        Storyboard.SetTarget(opacityAnim, CameraAlertDot);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(System.Windows.Shapes.Ellipse.OpacityProperty));

        _cameraBreathingStoryboard = new Storyboard();
        _cameraBreathingStoryboard.Children.Add(opacityAnim);
        _cameraBreathingStoryboard.Begin();
    }

    private void StopCameraBreathingAnimation()
    {
        if (_cameraBreathingStoryboard == null) return;
        _cameraBreathingStoryboard.Stop();
        _cameraBreathingStoryboard = null;
    }

    private void StartPausedPulseAnimation()
    {
        if (!_viewModel.AppSettings.AnimationEnabled) return;
        StopPausedPulseAnimation();

        var scaleXAnim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(4)
        };
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromPercent(0.5)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        var scaleYAnim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(4)
        };
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromPercent(0.5)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        Storyboard.SetTarget(scaleXAnim, TimerText);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        Storyboard.SetTarget(scaleYAnim, TimerText);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

        _pausedPulseStoryboard = new Storyboard();
        _pausedPulseStoryboard.Children.Add(scaleXAnim);
        _pausedPulseStoryboard.Children.Add(scaleYAnim);
        _pausedPulseStoryboard.Begin();
    }

    private void StopPausedPulseAnimation()
    {
        if (_pausedPulseStoryboard == null) return;
        _pausedPulseStoryboard.Stop();
        _pausedPulseStoryboard = null;
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
        Opacity = 0;
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(0.3),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);
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
            Height = 740;
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

    private void TestCameraButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SettingsVM?.TestCameraAlert();
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

    public void HandleWake()
    {
        _viewModel.RefreshTimerOnWake();
    }

    private void AdjustTimeUp_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AdjustWorkMinutes(5);
    }

    private void AdjustTimeDown_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AdjustWorkMinutes(-5);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        _trayService?.Dispose();
        base.OnClosed(e);
    }
}
