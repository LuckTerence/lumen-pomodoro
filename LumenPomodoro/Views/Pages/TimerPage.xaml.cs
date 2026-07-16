using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class TimerPage : Page
{
    private readonly MainViewModel _viewModel;
    private Storyboard? _breathingStoryboard;
    private Storyboard? _cameraBreathingStoryboard;
    private Storyboard? _pausedPulseStoryboard;
    private Storyboard? _completionStoryboard;

    public event Action? RequestTasksPage;
    public event Action? RequestStatsPage;

    public static readonly RoutedCommand ToggleCommand = new();
    public static readonly RoutedCommand ResetCommand = new();

    public TimerPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += (_, _) => UpdateStepLabel();

        CommandBindings.Add(new CommandBinding(ToggleCommand, Toggle_Executed));
        CommandBindings.Add(new CommandBinding(ResetCommand, Reset_Executed));
    }

    private void UpdateStepLabel()
    {
        if (StepLabel != null)
            StepLabel.Text = $"{_viewModel.AppSettings.WorkMinutes} 分钟";
    }

    private void Toggle_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        switch (_viewModel.CurrentStatus)
        {
            case TimerMode.Idle:
                _viewModel.StartFocus();
                break;
            case TimerMode.Focus:
            case TimerMode.Break:
                _viewModel.PauseFocus();
                break;
            case TimerMode.Paused:
                _viewModel.ResumeFocus();
                break;
        }
    }

    private void Reset_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _viewModel.ResetFocus();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsFocusCompleted):
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewModel.IsFocusCompleted)
                    {
                        StopBreathingAnimation();
                        PlayCompletionAnimation();
                        StartBreathingAnimation();
                        PlayArcProgressPulse();
                        StartBreakButton?.Focus();
                    }
                    else
                    {
                        StopCompletionAnimation();
                        StopBreathingAnimation();
                    }
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
            case nameof(MainViewModel.UserRating):
                Dispatcher.BeginInvoke(() => UpdateStarsUi());
                break;
            case nameof(MainViewModel.CurrentStatus):
                Dispatcher.BeginInvoke(() =>
                {
                    FadeInActivePanel();
                    if (_viewModel.CurrentStatus == TimerMode.Paused)
                        StartPausedPulseAnimation();
                    else
                        StopPausedPulseAnimation();
                });
                break;
        }
    }

    private void PlayCompletionAnimation()
    {
        if (!_viewModel.AppSettings.AnimationEnabled) return;

        StopCompletionAnimation();

        var scaleAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(400) };
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.15, KeyTime.FromPercent(0.4))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });

        Storyboard.SetTarget(scaleAnim, TimerTextBlock);
        Storyboard.SetTargetProperty(scaleAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

        var scaleYAnim = scaleAnim.Clone();
        Storyboard.SetTarget(scaleYAnim, TimerTextBlock);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

        _completionStoryboard = new Storyboard();
        _completionStoryboard.Children.Add(scaleAnim);
        _completionStoryboard.Children.Add(scaleYAnim);
        _completionStoryboard.Begin();
    }

    private void StopCompletionAnimation()
    {
        if (_completionStoryboard == null) return;
        _completionStoryboard.Stop();
        _completionStoryboard = null;
    }

    /// <summary>专注完成时 ArcProgress 做一次柔和高亮脉冲</summary>
    private void PlayArcProgressPulse()
    {
        if (!_viewModel.AppSettings.AnimationEnabled || ProgressRing == null) return;

        var pulseAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(600) };
        pulseAnim.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromPercent(0)));    // 原粗细
        pulseAnim.KeyFrames.Add(new EasingDoubleKeyFrame(10, KeyTime.FromPercent(0.25))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        pulseAnim.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromPercent(1.0))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });

        ProgressRing.BeginAnimation(Controls.ArcProgress.StrokeThicknessProperty, pulseAnim);
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

    private void UpdateStarsUi()
    {
        var rating = _viewModel.UserRating;
        foreach (var star in new[] { Star1, Star2, Star3, Star4, Star5 })
        {
            if (star == null) continue;
            int n = int.Parse(star.Tag.ToString()!);
            star.Content = n <= rating ? "★" : "☆";
        }
    }

    private void FadeInActivePanel()
    {
        if (!_viewModel.AppSettings.AnimationEnabled)
        {
            foreach (var p in new[] { IdlePanel, FocusPanel, PausedPanel, BreakPanel, CompletedPanel })
                p.Opacity = p.Visibility == Visibility.Visible ? 1.0 : 0.0;
            return;
        }

        var panels = new[] { IdlePanel, FocusPanel, PausedPanel, BreakPanel, CompletedPanel };
        foreach (var panel in panels)
        {
            if (panel.Visibility == Visibility.Visible)
            {
                var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                panel.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

                var translate = new TranslateTransform(0, 8);
                panel.RenderTransform = translate;
                var translateAnim = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(300))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                translate.BeginAnimation(TranslateTransform.YProperty, translateAnim);
            }
            else { panel.Opacity = 0; }
        }

        if (StateLabelBlock != null)
        {
            var labelAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            StateLabelBlock.BeginAnimation(UIElement.OpacityProperty, labelAnim);
        }
    }

    private void TaskName_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RequestTasksPage?.Invoke();
    }

    private void StatsSummary_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RequestStatsPage?.Invoke();
    }

    private void Star_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag.ToString(), out var stars))
        {
            _viewModel.SetRating(stars);

            if (_viewModel.AppSettings.AnimationEnabled && btn.RenderTransform is ScaleTransform st)
            {
                var bounceAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(400) };
                bounceAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
                bounceAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.35, KeyTime.FromPercent(0.3))
                { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } });
                bounceAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
                st.BeginAnimation(ScaleTransform.ScaleXProperty, bounceAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, bounceAnim);
            }
        }
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
    private void AdjustTimeUp_Click(object sender, RoutedEventArgs e) { _viewModel.AdjustWorkMinutes(5); UpdateStepLabel(); }
    private void AdjustTimeDown_Click(object sender, RoutedEventArgs e) { _viewModel.AdjustWorkMinutes(-5); UpdateStepLabel(); }

    private void PresetStandard_Click(object sender, RoutedEventArgs e) { _viewModel.ApplyPreset(PomodoroPreset.Standard); UpdateStepLabel(); }
    private void PresetDeep_Click(object sender, RoutedEventArgs e) { _viewModel.ApplyPreset(PomodoroPreset.DeepWork); UpdateStepLabel(); }
    private void PresetSprint_Click(object sender, RoutedEventArgs e) { _viewModel.ApplyPreset(PomodoroPreset.Sprint); UpdateStepLabel(); }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new Views.ShortcutHelpWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // 数字键 1-9 快速选择任务
        if (e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            int index = e.Key - Key.D1;
            if (index < _viewModel.Tasks.Count)
            {
                _viewModel.SelectedTask = _viewModel.Tasks[index];
                e.Handled = true;
                return;
            }
        }

        // 数字键 1-9（小键盘）
        if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
        {
            int index = e.Key - Key.NumPad1;
            if (index < _viewModel.Tasks.Count)
            {
                _viewModel.SelectedTask = _viewModel.Tasks[index];
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }
}
