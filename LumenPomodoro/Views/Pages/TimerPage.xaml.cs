using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class TimerPage : Page
{
    private readonly MainViewModel _viewModel;
    private Storyboard? _breathingStoryboard;
    private Storyboard? _cameraBreathingStoryboard;
    private Storyboard? _pausedPulseStoryboard;

    public event Action? RequestTasksPage;
    public event Action? RequestStatsPage;

    public TimerPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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

        Storyboard.SetTarget(opacityAnim, TimerTextBlock);
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
        TimerTextBlock.Opacity = 1.0;
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

        Storyboard.SetTarget(scaleXAnim, TimerTextBlock);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        Storyboard.SetTarget(scaleYAnim, TimerTextBlock);
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

    private void TaskName_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RequestTasksPage?.Invoke();
    }

    private void StatsSummary_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RequestStatsPage?.Invoke();
    }

    private void StartFocusButton_Click(object sender, RoutedEventArgs e) => _viewModel.StartFocus();
    private void PauseButton_Click(object sender, RoutedEventArgs e) => _viewModel.PauseFocus();
    private void ResumeButton_Click(object sender, RoutedEventArgs e) => _viewModel.ResumeFocus();
    private void ResetButton_Click(object sender, RoutedEventArgs e) => _viewModel.ResetFocus();
    private void StartShortBreakButton_Click(object sender, RoutedEventArgs e) => _viewModel.StartBreak(false);
    private void StartLongBreakButton_Click(object sender, RoutedEventArgs e) => _viewModel.StartBreak(true);
    private void SkipBreakButton_Click(object sender, RoutedEventArgs e) => _viewModel.SkipBreak();
    private void EndBreakButton_Click(object sender, RoutedEventArgs e) => _viewModel.EndBreak();
    private void StopCameraButton_Click(object sender, RoutedEventArgs e) => _viewModel.StopCameraAlert();
    private void AdjustTimeUp_Click(object sender, RoutedEventArgs e) => _viewModel.AdjustWorkMinutes(5);
    private void AdjustTimeDown_Click(object sender, RoutedEventArgs e) => _viewModel.AdjustWorkMinutes(-5);
}
