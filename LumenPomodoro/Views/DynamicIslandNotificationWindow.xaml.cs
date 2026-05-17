using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LumenPomodoro.Views;

public partial class DynamicIslandNotificationWindow : Window
{
    private bool _forceClose;
    private DispatcherTimer? _autoHideTimer;
    private Storyboard? _fadeOutStoryboard;
    private bool _isCountdownMode;

    public DynamicIslandNotificationWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 显示一次性通知（2.5秒后自动消失）
    /// </summary>
    public void ShowNotification(string title, string message)
    {
        StopTimers();
        _isCountdownMode = false;

        TitleBlock.Text = title;
        MessageBlock.Text = message;
        MessageBlock.Visibility = Visibility.Visible;
        CountdownBlock.Visibility = Visibility.Collapsed;

        PositionAndShow();

        // 2.5 秒后淡出
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _autoHideTimer.Tick += (s, e) =>
        {
            _autoHideTimer?.Stop();
            _autoHideTimer = null;
            FadeOutAndHide();
        };
        _autoHideTimer.Start();
    }

    /// <summary>
    /// 启动倒计时模式（持续显示，直到调用 HideCountdown）
    /// </summary>
    public void StartCountdown(string title)
    {
        StopTimers();
        _isCountdownMode = true;

        TitleBlock.Text = title;
        MessageBlock.Visibility = Visibility.Collapsed;
        CountdownBlock.Visibility = Visibility.Visible;
        CountdownBlock.Text = "--:--";

        PositionAndShow();
    }

    /// <summary>
    /// 更新倒计时显示
    /// </summary>
    public void UpdateCountdown(string remainingTime)
    {
        if (_isCountdownMode)
        {
            CountdownBlock.Text = remainingTime;
        }
    }

    /// <summary>
    /// 隐藏倒计时（淡出）
    /// </summary>
    public void HideCountdown()
    {
        if (_isCountdownMode)
        {
            _isCountdownMode = false;
            FadeOutAndHide();
        }
    }

    private void PositionAndShow()
    {
        // 测量真实尺寸
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));

        // 定位：屏幕顶部居中
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + 12;

        // 淡入
        Opacity = 0;
        if (!IsVisible) Show();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void FadeOutAndHide()
    {
        _fadeOutStoryboard = new Storyboard();
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        Storyboard.SetTarget(fadeOut, this);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        _fadeOutStoryboard.Children.Add(fadeOut);
        _fadeOutStoryboard.Completed += (s, e) =>
        {
            _fadeOutStoryboard = null;
            Hide();
        };
        _fadeOutStoryboard.Begin();
    }

    private void StopTimers()
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        _fadeOutStoryboard?.Stop();
        _fadeOutStoryboard = null;
    }

    public void ForceClose()
    {
        StopTimers();
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
