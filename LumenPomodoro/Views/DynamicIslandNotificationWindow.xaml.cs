using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LumenPomodoro.Views;

public partial class DynamicIslandNotificationWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private bool _forceClose;
    private DispatcherTimer? _autoHideTimer;
    private Storyboard? _activeStoryboard;
    private Storyboard? _pulseStoryboard;
    private bool _isCountdownMode;
    private int _remainingSeconds;
    private DispatcherTimer? _breathingTimer;

    // 苹果风格颜色 — 不可冻结，因为需要动画 ColorProperty
    private SolidColorBrush? _pillBrush;

    public DynamicIslandNotificationWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => HideFromWindowSwitcher();
        SizeChanged += (_, _) => CenterAtScreenTop();
    }

    private const double AutoHideSeconds = 2.5;

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

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, index)
            : new IntPtr(GetWindowLong32(hwnd, index));
    }

    private static void SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, index, value);
        else
            SetWindowLong32(hwnd, index, value.ToInt32());
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

    /// <summary>
    /// 重新断言置顶。WPF 的 Topmost 在遇到其他置顶窗口或全屏应用后可能被抢占，
    /// 因此在显示及倒计时刷新时主动用 SetWindowPos(HWND_TOPMOST) 强制置于顶层。
    /// </summary>
    private void ReassertTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// 显示一次性通知（苹果风格展开动画）
    /// </summary>
    public void ShowNotification(string title, string message)
    {
        StopAllAnimations();
        _isCountdownMode = false;

        TitleBlock.Text = title;
        MessageBlock.Text = message;
        MessageBlock.Visibility = Visibility.Visible;
        CountdownBlock.Visibility = Visibility.Collapsed;

        // 重置颜色
        _pillBrush ??= new SolidColorBrush(Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A));
        PillBorder.Background = _pillBrush;
        _pillBrush.Color = Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A);

        PositionAndShow();
        PlayExpandAnimation();

        // 2.5 秒后淡出
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutoHideSeconds) };
        _autoHideTimer.Tick += (s, e) =>
        {
            if (_autoHideTimer == null) return; // 防止双重触发
            _autoHideTimer.Stop();
            _autoHideTimer = null;
            PlayCollapseAnimation();
        };
        _autoHideTimer.Start();
    }

    /// <summary>
    /// 启动倒计时模式（苹果风格呼吸灯 + 颜色渐变）
    /// </summary>
    public void StartCountdown(string title)
    {
        StopAllAnimations();
        _isCountdownMode = true;
        _remainingSeconds = -1;

        TitleBlock.Text = title;
        MessageBlock.Visibility = Visibility.Collapsed;
        CountdownBlock.Visibility = Visibility.Visible;
        CountdownBlock.Text = "--:--";

        // 重置颜色
        _pillBrush ??= new SolidColorBrush(Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A));
        PillBorder.Background = _pillBrush;
        _pillBrush.Color = Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A);

        PositionAndShow();
        PlayExpandAnimation();
        StartBreathingAnimation();
    }

    /// <summary>
    /// 更新倒计时显示（带颜色渐变和脉冲动画）
    /// </summary>
    public void UpdateCountdown(string remainingTime)
    {
        if (!_isCountdownMode) return;

        CountdownBlock.Text = remainingTime;

        // 倒计时期间持续保持置顶，避免被其他置顶窗口抢占
        ReassertTopmost();

        // 解析剩余秒数
        if (TryParseSeconds(remainingTime, out int seconds))
        {
            if (seconds != _remainingSeconds)
            {
                _remainingSeconds = seconds;
                UpdateColorByTime(seconds);

                // 最后 60 秒启动脉冲动画
                if (seconds == 60)
                {
                    StartPulseAnimation();
                }
            }
        }
    }

    /// <summary>
    /// 隐藏倒计时（苹果风格收起动画）
    /// </summary>
    public void HideCountdown()
    {
        if (_isCountdownMode)
        {
            _isCountdownMode = false;
            StopAllAnimations();
            PlayCollapseAnimation();
        }
    }

    #region 动画方法

    /// <summary>
    /// 苹果风格展开动画 - 从中心展开，弹性效果
    /// </summary>
    private void PlayExpandAnimation()
    {
        var storyboard = new Storyboard();

        // ScaleX: 0.8 → 1.0（弹性）
        var scaleXAnim = new DoubleAnimationUsingKeyFrames();
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.8, KeyTime.FromPercent(0)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.6))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        });
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleXAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);

        // ScaleY: 0.8 → 1.0（弹性）
        var scaleYAnim = new DoubleAnimationUsingKeyFrames();
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.8, KeyTime.FromPercent(0)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.6))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        });
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleYAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);

        // Opacity: 0 → 1
        var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        Storyboard.SetTarget(opacityAnim, this);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacityAnim);

        storyboard.Duration = TimeSpan.FromMilliseconds(400);
        _activeStoryboard = storyboard;
        storyboard.Begin();
    }

    /// <summary>
    /// 苹果风格收起动画 - 向中心收缩，弹性效果
    /// </summary>
    private void PlayCollapseAnimation()
    {
        var storyboard = new Storyboard();

        // ScaleX: 1.0 → 0.8
        var scaleXAnim = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(scaleXAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);

        // ScaleY: 1.0 → 0.8
        var scaleYAnim = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(scaleYAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);

        // Opacity: 1 → 0
        var opacityAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
        {
            BeginTime = TimeSpan.FromMilliseconds(100)
        };
        Storyboard.SetTarget(opacityAnim, this);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacityAnim);

        storyboard.Duration = TimeSpan.FromMilliseconds(350);
        storyboard.Completed += (s, e) =>
        {
            _activeStoryboard = null;
            Hide();
        };
        _activeStoryboard = storyboard;
        storyboard.Begin();
    }

    /// <summary>
    /// 苹果风格呼吸灯效果 - 背景透明度周期变化
    /// </summary>
    private void StartBreathingAnimation()
    {
        StopBreathingAnimation();

        _breathingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        bool isHigh = true;

        _breathingTimer.Tick += (s, e) =>
        {
            if (!_isCountdownMode)
            {
                _breathingTimer?.Stop();
                return;
            }

            var targetOpacity = isHigh ? 1.0 : 0.85;
            var anim = new DoubleAnimation(targetOpacity, TimeSpan.FromSeconds(1.5))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            PillBorder.BeginAnimation(OpacityProperty, anim);
            isHigh = !isHigh;
        };

        _breathingTimer.Start();
    }

    /// <summary>
    /// 停止呼吸灯动画
    /// </summary>
    private void StopBreathingAnimation()
    {
        _breathingTimer?.Stop();
        _breathingTimer = null;
        PillBorder.Opacity = 1.0;
    }

    /// <summary>
    /// 苹果风格脉冲动画 - 最后 60 秒轻微缩放
    /// </summary>
    private void StartPulseAnimation()
    {
        var storyboard = new Storyboard()
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(2)
        };

        // ScaleX: 1.0 → 1.015 → 1.0
        var scaleXAnim = new DoubleAnimationUsingKeyFrames();
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.015, KeyTime.FromPercent(0.5))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        Storyboard.SetTarget(scaleXAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);

        // ScaleY: 1.0 → 1.015 → 1.0
        var scaleYAnim = new DoubleAnimationUsingKeyFrames();
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.015, KeyTime.FromPercent(0.5))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        Storyboard.SetTarget(scaleYAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);

        _pulseStoryboard = storyboard;
        storyboard.Begin();
    }

    /// <summary>
    /// 根据剩余时间更新背景颜色（苹果风格渐变）
    /// </summary>
    private void UpdateColorByTime(int seconds)
    {
        Color targetColor;

        if (seconds > 300) // > 5 分钟：深灰
        {
            targetColor = Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A);
        }
        else if (seconds > 60) // 1-5 分钟：渐变到橙色
        {
            var progress = (300.0 - seconds) / 240.0; // 0 → 1
            var r = (byte)(0x1A + (0xF5 - 0x1A) * progress);
            var g = (byte)(0x1A + (0x9E - 0x1A) * progress);
            var b = (byte)(0x1A + (0x0B - 0x1A) * progress);
            targetColor = Color.FromArgb(0xE0, r, g, b);
        }
        else // ≤ 60 秒：渐变到红色
        {
            var progress = (60.0 - seconds) / 60.0; // 0 → 1
            var r = (byte)(0xF5 + (0xEF - 0xF5) * progress);
            var g = (byte)(0x9E + (0x44 - 0x9E) * progress);
            var b = (byte)(0x0B + (0x44 - 0x0B) * progress);
            targetColor = Color.FromArgb(0xE0, r, g, b);
        }

        // 平滑过渡颜色 — 直接在 SolidColorBrush 上动画 ColorProperty
        var colorAnim = new ColorAnimation(targetColor, TimeSpan.FromSeconds(1))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        _pillBrush?.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
    }

    #endregion

    #region 辅助方法

    private void PositionAndShow()
    {
        // 确保 RenderTransform 存在
        if (PillBorder.RenderTransform == null || PillBorder.RenderTransform is not ScaleTransform)
        {
            PillBorder.RenderTransform = new ScaleTransform(1, 1);
            PillBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        // 测量真实尺寸
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));
        UpdateLayout();

        // 定位：屏幕顶部居中
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

        _activeStoryboard?.Stop();
        _activeStoryboard = null;

        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;

        StopBreathingAnimation();

        // 重置变换
        if (PillBorder.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = 1.0;
            scale.ScaleY = 1.0;
        }
        PillBorder.Opacity = 1.0;
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

    #endregion
}
