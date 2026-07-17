using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LumenPomodoro.Models;

namespace LumenPomodoro.Views;

/// <summary>
/// 灵动岛：Compact / Expanded / Transient 三态。
/// </summary>
public partial class DynamicIslandNotificationWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const double AutoHideSeconds = 2.5;
    private const double ExpandIdleSeconds = 4.0;

    private bool _forceClose;
    private DispatcherTimer? _autoHideTimer;
    private DispatcherTimer? _expandIdleTimer;
    private Storyboard? _activeStoryboard;
    private Storyboard? _pulseStoryboard;
    private DispatcherTimer? _breathingTimer;
    private SolidColorBrush? _pillBrush;

    private bool _isCountdownMode;
    private bool _isExpanded;
    private bool _isTransient;
    private int _remainingSeconds = -1;
    private string _sessionMode = "idle"; // focus | break | paused | idle
    private string _whenFocused = "minimize";
    private bool _mainWindowFocused;

    public event Action? PauseRequested;
    public event Action? ResumeRequested;
    public event Action? SkipBreakRequested;
    public event Action? StartFocusRequested;
    public event Action? OpenMainWindowRequested;

    public DynamicIslandNotificationWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => HideFromWindowSwitcher();
        SizeChanged += (_, _) => CenterAtScreenTop();
    }

    #region Win32

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));

    private static void SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, index, value);
        else SetWindowLong32(hwnd, index, value.ToInt32());
    }

    private void HideFromWindowSwitcher()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    private void ReassertTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    #endregion

    /// <summary>主窗焦点变化 + WhenFocused 策略。</summary>
    public void ApplyFocusPolicy(bool mainWindowFocused, string whenFocused)
    {
        _mainWindowFocused = mainWindowFocused;
        _whenFocused = string.IsNullOrWhiteSpace(whenFocused) ? "minimize" : whenFocused.Trim().ToLowerInvariant();
        ApplyVisualPolicy();
    }

    public void SetSessionMode(TimerMode mode)
    {
        _sessionMode = mode switch
        {
            TimerMode.Focus => "focus",
            TimerMode.Break => "break",
            TimerMode.Paused => "paused",
            _ => "idle"
        };
        RefreshActionButtons();
    }

    /// <summary>Transient 事件（完成 / 预告 / 走神）。</summary>
    public void ShowNotification(string title, string message)
    {
        StopExpandIdle();
        _isTransient = true;
        _isExpanded = false;
        ActionsPanel.Visibility = Visibility.Collapsed;

        TitleBlock.Text = title;
        MessageBlock.Text = message;
        MessageBlock.Visibility = Visibility.Visible;
        CountdownBlock.Visibility = Visibility.Collapsed;

        EnsureBrush();
        _pillBrush!.Color = Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A);
        PillBorder.Background = _pillBrush;

        PositionAndShow();
        PlayExpandAnimation();
        ApplyVisualPolicy(forceShow: true);

        _autoHideTimer?.Stop();
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutoHideSeconds) };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer?.Stop();
            _autoHideTimer = null;
            _isTransient = false;
            if (_isCountdownMode)
            {
                // 回到 Compact 倒计时
                MessageBlock.Visibility = Visibility.Collapsed;
                CountdownBlock.Visibility = Visibility.Visible;
                ApplyVisualPolicy();
            }
            else
            {
                PlayCollapseAnimation();
            }
        };
        _autoHideTimer.Start();
    }

    public void StartCountdown(string title)
    {
        StopAllAnimations();
        _isCountdownMode = true;
        _isTransient = false;
        _isExpanded = false;
        _remainingSeconds = -1;

        TitleBlock.Text = title;
        MessageBlock.Visibility = Visibility.Collapsed;
        CountdownBlock.Visibility = Visibility.Visible;
        CountdownBlock.Text = "--:--";
        ActionsPanel.Visibility = Visibility.Collapsed;

        EnsureBrush();
        _pillBrush!.Color = Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A);
        PillBorder.Background = _pillBrush;

        PositionAndShow();
        PlayExpandAnimation();
        StartBreathingAnimation();
        ApplyVisualPolicy();
    }

    public void UpdateCountdown(string remainingTime)
    {
        if (!_isCountdownMode || _isTransient) return;

        CountdownBlock.Text = remainingTime;
        ReassertTopmost();

        if (TryParseSeconds(remainingTime, out int seconds) && seconds != _remainingSeconds)
        {
            _remainingSeconds = seconds;
            UpdateColorByTime(seconds);
            if (seconds == 60) StartPulseAnimation();
        }
    }

    public void HideCountdown()
    {
        if (!_isCountdownMode && !IsVisible) return;
        _isCountdownMode = false;
        _isExpanded = false;
        _isTransient = false;
        StopAllAnimations();
        PlayCollapseAnimation();
    }

    private void Island_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isTransient) return;
        if (e.OriginalSource is System.Windows.Controls.Button) return;

        if (!_isCountdownMode && _sessionMode == "idle")
        {
            ToggleExpanded();
            return;
        }

        if (_isCountdownMode || _sessionMode is "focus" or "break" or "paused" or "idle")
            ToggleExpanded();
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        if (_isExpanded)
        {
            RefreshActionButtons();
            ActionsPanel.Visibility = Visibility.Visible;
            ResetExpandIdle();
        }
        else
        {
            ActionsPanel.Visibility = Visibility.Collapsed;
            StopExpandIdle();
        }
        PositionAndShow();
    }

    private void RefreshActionButtons()
    {
        PauseResumeButton.Visibility = Visibility.Collapsed;
        SkipBreakButton.Visibility = Visibility.Collapsed;
        StartFocusButton.Visibility = Visibility.Collapsed;

        switch (_sessionMode)
        {
            case "focus":
                PauseResumeButton.Content = "暂停";
                PauseResumeButton.Visibility = Visibility.Visible;
                break;
            case "paused":
                PauseResumeButton.Content = "继续";
                PauseResumeButton.Visibility = Visibility.Visible;
                break;
            case "break":
                SkipBreakButton.Visibility = Visibility.Visible;
                break;
            default:
                StartFocusButton.Visibility = Visibility.Visible;
                break;
        }
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionMode == "paused") ResumeRequested?.Invoke();
        else PauseRequested?.Invoke();
        CollapseExpanded();
    }

    private void SkipBreak_Click(object sender, RoutedEventArgs e)
    {
        SkipBreakRequested?.Invoke();
        CollapseExpanded();
    }

    private void StartFocus_Click(object sender, RoutedEventArgs e)
    {
        StartFocusRequested?.Invoke();
        CollapseExpanded();
    }

    private void OpenMain_Click(object sender, RoutedEventArgs e)
    {
        OpenMainWindowRequested?.Invoke();
        CollapseExpanded();
    }

    private void CollapseExpanded()
    {
        _isExpanded = false;
        ActionsPanel.Visibility = Visibility.Collapsed;
        StopExpandIdle();
    }

    private void ResetExpandIdle()
    {
        StopExpandIdle();
        _expandIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ExpandIdleSeconds) };
        _expandIdleTimer.Tick += (_, _) =>
        {
            CollapseExpanded();
            PositionAndShow();
        };
        _expandIdleTimer.Start();
    }

    private void StopExpandIdle()
    {
        _expandIdleTimer?.Stop();
        _expandIdleTimer = null;
    }

    private void ApplyVisualPolicy(bool forceShow = false)
    {
        if (!IsVisible && !forceShow && !_isCountdownMode && !_isTransient) return;

        // hide when focused
        if (_mainWindowFocused && _whenFocused == "hide" && !_isTransient)
        {
            if (IsVisible) Hide();
            return;
        }

        if (!IsVisible && (_isCountdownMode || _isTransient || forceShow))
            PositionAndShow();

        if (_mainWindowFocused && _whenFocused == "minimize" && !_isTransient && !_isExpanded)
        {
            Opacity = 0.55;
            if (PillBorder.RenderTransform is ScaleTransform st)
            {
                st.ScaleX = 0.92;
                st.ScaleY = 0.92;
            }
        }
        else
        {
            Opacity = 1.0;
            if (PillBorder.RenderTransform is ScaleTransform st)
            {
                st.ScaleX = 1.0;
                st.ScaleY = 1.0;
            }
        }

        ReassertTopmost();
    }

    #region Animation (kept lean)

    private void EnsureBrush()
    {
        _pillBrush ??= new SolidColorBrush(Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A));
    }

    private void PlayExpandAnimation()
    {
        var storyboard = new Storyboard();
        var scaleXAnim = new DoubleAnimationUsingKeyFrames();
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.85, KeyTime.FromPercent(0)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.7))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }
        });
        Storyboard.SetTarget(scaleXAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);

        var scaleYAnim = new DoubleAnimationUsingKeyFrames();
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.85, KeyTime.FromPercent(0)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.7))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }
        });
        Storyboard.SetTarget(scaleYAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);

        var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        Storyboard.SetTarget(opacityAnim, this);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacityAnim);

        _activeStoryboard = storyboard;
        storyboard.Begin();
    }

    private void PlayCollapseAnimation()
    {
        var storyboard = new Storyboard();
        var scaleXAnim = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(200));
        Storyboard.SetTarget(scaleXAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);
        var scaleYAnim = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(200));
        Storyboard.SetTarget(scaleYAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);
        var opacityAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        Storyboard.SetTarget(opacityAnim, this);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacityAnim);
        storyboard.Completed += (_, _) =>
        {
            _activeStoryboard = null;
            Hide();
        };
        _activeStoryboard = storyboard;
        storyboard.Begin();
    }

    private void StartBreathingAnimation()
    {
        StopBreathingAnimation();
        _breathingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        var isHigh = true;
        _breathingTimer.Tick += (_, _) =>
        {
            if (!_isCountdownMode) { _breathingTimer?.Stop(); return; }
            var target = isHigh ? 1.0 : 0.88;
            PillBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromSeconds(1.4))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
            isHigh = !isHigh;
        };
        _breathingTimer.Start();
    }

    private void StopBreathingAnimation()
    {
        _breathingTimer?.Stop();
        _breathingTimer = null;
        PillBorder.BeginAnimation(OpacityProperty, null);
        PillBorder.Opacity = 1.0;
    }

    private void StartPulseAnimation()
    {
        _pulseStoryboard?.Stop();
        var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(2) };
        var scaleXAnim = new DoubleAnimationUsingKeyFrames();
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.02, KeyTime.FromPercent(0.5)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleXAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);
        var scaleYAnim = new DoubleAnimationUsingKeyFrames();
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.02, KeyTime.FromPercent(0.5)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleYAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);
        _pulseStoryboard = storyboard;
        storyboard.Begin();
    }

    private void UpdateColorByTime(int seconds)
    {
        Color target;
        if (seconds > 300)
            target = Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A);
        else if (seconds > 60)
        {
            var p = (300.0 - seconds) / 240.0;
            target = Color.FromArgb(0xE0,
                (byte)(0x1A + (0xF5 - 0x1A) * p),
                (byte)(0x1A + (0x9E - 0x1A) * p),
                (byte)(0x1A + (0x0B - 0x1A) * p));
        }
        else
        {
            var p = (60.0 - seconds) / 60.0;
            target = Color.FromArgb(0xE0,
                (byte)(0xF5 + (0xEF - 0xF5) * p),
                (byte)(0x9E + (0x44 - 0x9E) * p),
                (byte)(0x0B + (0x44 - 0x0B) * p));
        }
        EnsureBrush();
        _pillBrush!.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(target, TimeSpan.FromSeconds(0.8)));
    }

    #endregion

    private void PositionAndShow()
    {
        if (PillBorder.RenderTransform is not ScaleTransform)
        {
            PillBorder.RenderTransform = new ScaleTransform(1, 1);
            PillBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));
        UpdateLayout();
        CenterAtScreenTop();
        if (!IsVisible) Show();
        ReassertTopmost();
    }

    private void CenterAtScreenTop()
    {
        var width = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
        Left = SystemParameters.VirtualScreenLeft + (SystemParameters.PrimaryScreenWidth - width) / 2;
        Top = SystemParameters.WorkArea.Top + 12;
    }

    private void StopAllAnimations()
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        StopExpandIdle();
        _activeStoryboard?.Stop();
        _activeStoryboard = null;
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
        StopBreathingAnimation();
        if (PillBorder.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }
        PillBorder.Opacity = 1;
    }

    private static bool TryParseSeconds(string timeStr, out int seconds)
    {
        seconds = 0;
        var parts = timeStr.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int mins) &&
            int.TryParse(parts[1], out int secs))
        {
            seconds = mins * 60 + secs;
            return true;
        }
        return false;
    }

    public void ForceClose()
    {
        StopAllAnimations();
        _forceClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
