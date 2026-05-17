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

    public DynamicIslandNotificationWindow()
    {
        InitializeComponent();
    }

    public void ShowNotification(string title, string message)
    {
        // 取消正在进行的通知
        StopTimers();

        TitleBlock.Text = title;
        MessageBlock.Text = message;

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
