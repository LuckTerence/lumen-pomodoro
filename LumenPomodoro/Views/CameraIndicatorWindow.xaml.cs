using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LumenPomodoro.Views;

public partial class CameraIndicatorWindow : Window
{
    public CameraIndicatorWindow()
    {
        InitializeComponent();
        PositionBottomRight();
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
}
