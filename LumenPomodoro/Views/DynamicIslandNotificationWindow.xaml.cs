using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using LumenPomodoro.Models;

namespace LumenPomodoro.Views;

/// <summary>
/// 灵动岛：Compact / Expanded / Transient；支持岛上选任务与精修动效。
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
    private const double AutoHideSeconds = 2.6;
    private const double ExpandIdleSeconds = 5.0;

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
    private string _sessionMode = "idle";
    private string _whenFocused = "minimize";
    private bool _mainWindowFocused;
    private string? _selectedTaskId;
    private string? _selectedTaskName;
    private string _selectedTaskColor = "#3B82F6";
    private List<IslandTaskChip> _tasks = new();

    public event Action? PauseRequested;
    public event Action? ResumeRequested;
    public event Action? SkipBreakRequested;
    public event Action? StartFocusRequested;
    public event Action? OpenMainWindowRequested;
    public event Action<string>? TaskSelected;

    public DynamicIslandNotificationWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => HideFromWindowSwitcher();
        SizeChanged += (_, _) => CenterAtScreenTop();
    }

    public readonly record struct IslandTaskChip(string Id, string Name, string Color);

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
        RebuildTaskChips();
    }

    public void SetTasks(IEnumerable<IslandTaskChip> tasks, string? selectedId)
    {
        _tasks = tasks.Take(8).ToList();
        _selectedTaskId = selectedId;
        var sel = _tasks.FirstOrDefault(t => t.Id == selectedId);
        if (sel.Id != null)
        {
            _selectedTaskName = sel.Name;
            _selectedTaskColor = string.IsNullOrWhiteSpace(sel.Color) ? "#3B82F6" : sel.Color;
        }
        UpdateTaskDot();
        RebuildTaskChips();
        if (_isCountdownMode && !_isTransient)
            ApplyCompactTitle();
    }

    /// <summary>Idle 待命岛：可点开选任务并开始。</summary>
    public void ShowIdleReady(string taskLabel, string readyTime)
    {
        StopAllAnimations(keepBreathing: false);
        _isCountdownMode = true; // 复用显示链路，便于策略与展开
        _isTransient = false;
        _isExpanded = false;
        _remainingSeconds = -1;
        _sessionMode = "idle";

        ApplyCompactTitle(taskLabel);
        MessageBlock.Visibility = Visibility.Collapsed;
        CountdownBlock.Visibility = Visibility.Visible;
        CountdownBlock.Text = readyTime;
        ActionsPanel.Visibility = Visibility.Collapsed;
        TasksScroller.Visibility = Visibility.Collapsed;

        EnsureBrush();
        _pillBrush!.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _pillBrush.Color = Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A);
        PillBorder.Background = _pillBrush;

        PositionAndShow();
        PlayAppearAnimation();
        ApplyVisualPolicy();
        RefreshActionButtons();
    }

    public void ShowNotification(string title, string message)
    {
        StopExpandIdle();
        _isTransient = true;
        _isExpanded = false;
        ActionsPanel.Visibility = Visibility.Collapsed;
        TasksScroller.Visibility = Visibility.Collapsed;

        TitleBlock.Text = title;
        MessageBlock.Text = message;
        MessageBlock.Visibility = Visibility.Visible;
        CountdownBlock.Visibility = Visibility.Collapsed;
        TaskDot.Visibility = Visibility.Collapsed;

        EnsureBrush();
        _pillBrush!.BeginAnimation(SolidColorBrush.ColorProperty, null);
        // Transient 略亮一档
        AnimatePillColor(Color.FromArgb(0xF0, 0x22, 0x22, 0x2A));

        PositionAndShow();
        PlayTransientPopAnimation();
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
                MessageBlock.Visibility = Visibility.Collapsed;
                CountdownBlock.Visibility = Visibility.Visible;
                ApplyCompactTitle();
                UpdateTaskDot();
                PlayMorphToCompact();
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
        StopAllAnimations(keepBreathing: false);
        _isCountdownMode = true;
        _isTransient = false;
        _isExpanded = false;
        _remainingSeconds = -1;

        ApplyCompactTitle(title);
        MessageBlock.Visibility = Visibility.Collapsed;
        CountdownBlock.Visibility = Visibility.Visible;
        CountdownBlock.Text = "--:--";
        ActionsPanel.Visibility = Visibility.Collapsed;
        TasksScroller.Visibility = Visibility.Collapsed;

        EnsureBrush();
        _pillBrush!.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _pillBrush.Color = Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A);
        PillBorder.Background = _pillBrush;

        PositionAndShow();
        PlayAppearAnimation();
        StartBreathingAnimation();
        ApplyVisualPolicy();
        RefreshActionButtons();
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
            if (seconds is > 0 and <= 10)
                PlayTickNudge();
        }
    }

    public void HideCountdown()
    {
        if (!_isCountdownMode && !IsVisible) return;
        _isCountdownMode = false;
        _isExpanded = false;
        _isTransient = false;
        StopAllAnimations(keepBreathing: false);
        PlayCollapseAnimation();
    }

    private void ApplyCompactTitle(string? overrideTitle = null)
    {
        if (!string.IsNullOrEmpty(overrideTitle) && _sessionMode is not ("idle" or "focus" or "break" or "paused"))
        {
            TitleBlock.Text = overrideTitle;
            return;
        }

        var modeLabel = _sessionMode switch
        {
            "focus" => "专注中",
            "break" => "休息中",
            "paused" => "已暂停",
            "idle" => "准备",
            _ => overrideTitle ?? "Lumen"
        };

        if (!string.IsNullOrWhiteSpace(_selectedTaskName) && _sessionMode is "idle" or "focus" or "paused")
            TitleBlock.Text = $"{modeLabel} · {_selectedTaskName}";
        else
            TitleBlock.Text = string.IsNullOrEmpty(overrideTitle) ? modeLabel : overrideTitle!;
    }

    private void UpdateTaskDot()
    {
        if (string.IsNullOrWhiteSpace(_selectedTaskName) || _isTransient)
        {
            TaskDot.Visibility = Visibility.Collapsed;
            return;
        }

        TaskDot.Visibility = Visibility.Visible;
        TaskDot.Fill = ParseBrush(_selectedTaskColor);
    }

    private void RebuildTaskChips()
    {
        TasksPanel.Children.Clear();
        if (_tasks.Count == 0) return;

        var canSelect = _sessionMode is "idle";
        foreach (var task in _tasks)
        {
            var isSelected = task.Id == _selectedTaskId;
            var btn = new Button
            {
                Content = BuildChipContent(task, isSelected),
                Tag = task.Id,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = canSelect ? Cursors.Hand : Cursors.Arrow,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Foreground = Brushes.White,
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(0x55, 0x3B, 0x82, 0xF6))
                    : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                IsEnabled = canSelect || isSelected,
                Opacity = canSelect || isSelected ? 1.0 : 0.55,
                ToolTip = canSelect ? $"选择「{task.Name}」" : task.Name
            };
            btn.Click += TaskChip_Click;
            TasksPanel.Children.Add(btn);
        }
    }

    private static object BuildChipContent(IslandTaskChip task, bool selected)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = ParseBrush(task.Color),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = task.Name,
            Foreground = Brushes.White,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 72,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        return sp;
    }

    private void TaskChip_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_sessionMode != "idle") return;
        if (sender is not Button { Tag: string id }) return;

        _selectedTaskId = id;
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task.Id != null)
        {
            _selectedTaskName = task.Name;
            _selectedTaskColor = task.Color;
        }
        TaskSelected?.Invoke(id);
        ApplyCompactTitle();
        UpdateTaskDot();
        RebuildTaskChips();
        ResetExpandIdle();
        PlayChipSelectPulse();
        PositionAndShow();
    }

    private void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isTransient) return;
        if (e.OriginalSource is Button or Ellipse) return;
        // 点在任务条上不收起
        if (e.OriginalSource is DependencyObject d && IsUnder(d, TasksPanel))
            return;

        ToggleExpanded();
    }

    private static bool IsUnder(DependencyObject? source, DependencyObject ancestor)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, ancestor)) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        if (_isExpanded)
        {
            RefreshActionButtons();
            ActionsPanel.Visibility = Visibility.Visible;
            TasksScroller.Visibility = _tasks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RebuildTaskChips();
            ResetExpandIdle();
            PlayMorphExpand();
        }
        else
        {
            ActionsPanel.Visibility = Visibility.Collapsed;
            TasksScroller.Visibility = Visibility.Collapsed;
            StopExpandIdle();
            PlayMorphToCompact();
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
        e.Handled = true;
        if (_sessionMode == "paused") ResumeRequested?.Invoke();
        else PauseRequested?.Invoke();
        CollapseExpanded();
    }

    private void SkipBreak_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SkipBreakRequested?.Invoke();
        CollapseExpanded();
    }

    private void StartFocus_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        StartFocusRequested?.Invoke();
        CollapseExpanded();
    }

    private void OpenMain_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenMainWindowRequested?.Invoke();
        CollapseExpanded();
    }

    private void CollapseExpanded()
    {
        if (!_isExpanded) return;
        _isExpanded = false;
        ActionsPanel.Visibility = Visibility.Collapsed;
        TasksScroller.Visibility = Visibility.Collapsed;
        StopExpandIdle();
        PlayMorphToCompact();
        PositionAndShow();
    }

    private void ResetExpandIdle()
    {
        StopExpandIdle();
        _expandIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ExpandIdleSeconds) };
        _expandIdleTimer.Tick += (_, _) => CollapseExpanded();
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

        if (_mainWindowFocused && _whenFocused == "hide" && !_isTransient)
        {
            if (IsVisible) Hide();
            return;
        }

        if (!IsVisible && (_isCountdownMode || _isTransient || forceShow))
            PositionAndShow();

        if (_mainWindowFocused && _whenFocused == "minimize" && !_isTransient && !_isExpanded)
        {
            AnimateWindowOpacity(0.55);
            AnimateScale(0.92);
        }
        else
        {
            AnimateWindowOpacity(1.0);
            if (!_isExpanded) AnimateScale(1.0);
        }

        ReassertTopmost();
    }

    #region Animations

    private void EnsureBrush()
    {
        _pillBrush ??= new SolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A));
        PillBorder.Background = _pillBrush;
    }

    private void AnimatePillColor(Color target)
    {
        EnsureBrush();
        _pillBrush!.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(target, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void AnimateWindowOpacity(double to)
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void AnimateScale(double to)
    {
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 };
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
    }

    private void PlayAppearAnimation()
    {
        PillScale.ScaleX = 0.72;
        PillScale.ScaleY = 0.72;
        PillTranslate.Y = -8;
        Opacity = 0;

        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
        var dur = TimeSpan.FromMilliseconds(420);
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        PillTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, dur) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
    }

    private void PlayTransientPopAnimation()
    {
        PillScale.ScaleX = 0.6;
        PillScale.ScaleY = 0.6;
        Opacity = 0;
        var ease = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 6 };
        var dur = TimeSpan.FromMilliseconds(520);
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
    }

    private void PlayMorphExpand()
    {
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 };
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1.0, 1.04, TimeSpan.FromMilliseconds(180)) { AutoReverse = true, EasingFunction = ease });
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1.0, 1.04, TimeSpan.FromMilliseconds(180)) { AutoReverse = true, EasingFunction = ease });
        AnimateWindowOpacity(1);
    }

    private void PlayMorphToCompact()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
    }

    private void PlayChipSelectPulse()
    {
        var anim = new DoubleAnimation(1.0, 1.06, TimeSpan.FromMilliseconds(120))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim.Clone());
    }

    private void PlayTickNudge()
    {
        // 最后 10 秒轻微下沉回弹
        PillTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, 2, TimeSpan.FromMilliseconds(90))
            {
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void PlayCollapseAnimation()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var scaleX = new DoubleAnimation(0.75, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(0.75, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
        var opacity = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
        var ty = new DoubleAnimation(-10, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };

        opacity.Completed += (_, _) => Hide();
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        PillTranslate.BeginAnimation(TranslateTransform.YProperty, ty);
        BeginAnimation(OpacityProperty, opacity);
    }

    private void StartBreathingAnimation()
    {
        StopBreathingAnimation();
        _breathingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.8) };
        var isHigh = true;
        _breathingTimer.Tick += (_, _) =>
        {
            if (!_isCountdownMode || _isExpanded || _isTransient)
            {
                return;
            }
            var target = isHigh ? 1.0 : 0.9;
            PillBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromSeconds(1.35))
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
        var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(1.6) };
        var scaleXAnim = new DoubleAnimationUsingKeyFrames();
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.03, KeyTime.FromPercent(0.5))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        scaleXAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleXAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);

        var scaleYAnim = new DoubleAnimationUsingKeyFrames();
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.03, KeyTime.FromPercent(0.5))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        scaleYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleYAnim, PillBorder);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);

        _pulseStoryboard = storyboard;
        storyboard.Begin();
    }

    private void UpdateColorByTime(int seconds)
    {
        Color target;
        if (seconds > 300)
            target = Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A);
        else if (seconds > 60)
        {
            var p = (300.0 - seconds) / 240.0;
            target = Color.FromArgb(0xF0,
                (byte)(0x1A + (0xF5 - 0x1A) * p),
                (byte)(0x1A + (0x9E - 0x1A) * p),
                (byte)(0x1A + (0x0B - 0x1A) * p));
        }
        else
        {
            var p = (60.0 - seconds) / 60.0;
            target = Color.FromArgb(0xF0,
                (byte)(0xF5 + (0xEF - 0xF5) * p),
                (byte)(0x9E + (0x44 - 0x9E) * p),
                (byte)(0x0B + (0x44 - 0x0B) * p));
        }
        AnimatePillColor(target);
    }

    #endregion

    private void PositionAndShow()
    {
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
        Top = SystemParameters.WorkArea.Top + 10;
    }

    private void StopAllAnimations(bool keepBreathing)
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        StopExpandIdle();
        _activeStoryboard?.Stop();
        _activeStoryboard = null;
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
        if (!keepBreathing) StopBreathingAnimation();
        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        PillTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PillScale.ScaleX = 1;
        PillScale.ScaleY = 1;
        PillTranslate.Y = 0;
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

    private static Brush ParseBrush(string? hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            return (Brush)new BrushConverter().ConvertFrom(hex)!;
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        }
    }

    public void ForceClose()
    {
        StopAllAnimations(keepBreathing: false);
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
