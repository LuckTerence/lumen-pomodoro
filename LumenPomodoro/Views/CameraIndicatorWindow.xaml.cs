using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LumenPomodoro.Views;

public partial class CameraIndicatorWindow : Window
{
    private bool _forceClose;

    public CameraIndicatorWindow()
    {
        InitializeComponent();
        PositionBottomRight();
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 48;
    }

    public void ShowIndicator(Color color)
    {
        IndicatorDot.Fill = new SolidColorBrush(color);
        if (IndicatorDot.Effect is DropShadowEffect shadow)
            shadow.Color = color;
        Show();
    }

    public void HideIndicator()
    {
        Hide();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
