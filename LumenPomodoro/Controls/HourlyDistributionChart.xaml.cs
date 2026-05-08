using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.Models;

namespace LumenPomodoro.Controls;

public partial class HourlyDistributionChart : UserControl
{
    public static readonly DependencyProperty HourlyDataProperty =
        DependencyProperty.Register(nameof(HourlyData), typeof(List<HourlyDataPoint>), typeof(HourlyDistributionChart),
            new PropertyMetadata(null, OnDataChanged));

    public List<HourlyDataPoint>? HourlyData
    {
        get => (List<HourlyDataPoint>?)GetValue(HourlyDataProperty);
        set => SetValue(HourlyDataProperty, value);
    }

    public HourlyDistributionChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HourlyDistributionChart)d).Render();
    }

    private void Render()
    {
        ChartCanvas.Children.Clear();
        var data = HourlyData;
        if (data == null || data.Count == 0 || ActualWidth <= 0) return;

        var maxMinutes = data.Max(d => d.TotalMinutes);
        if (maxMinutes == 0) maxMinutes = 1;

        var barAreaHeight = 110.0;
        var marginLeft = 4.0;
        var marginRight = 4.0;
        var barAreaWidth = ActualWidth - marginLeft - marginRight;
        var barWidth = barAreaWidth / 24 - 2;

        var accentBrush = Application.Current.TryFindResource("AccentFillColorDefaultBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 102, 204));
        var textBrush = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush
                        ?? Brushes.Gray;
        var accentColor = ((SolidColorBrush)accentBrush).Color;

        for (int h = 0; h < 24; h++)
        {
            var point = data[h];
            var barHeight = (double)point.TotalMinutes / maxMinutes * barAreaHeight;
            var x = marginLeft + h * (barAreaWidth / 24) + 1;

            var bar = new Rectangle
            {
                Width = Math.Max(1, barWidth),
                Height = Math.Max(0, barHeight),
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(accentColor) { Opacity = 0.3 + 0.7 * barHeight / barAreaHeight },
                Cursor = Cursors.Hand,
                ToolTip = $"{h}:00\n{point.TotalMinutes} 分钟 · {point.SessionCount} 次"
            };
            bar.MouseEnter += (_, _) => bar.Opacity = 0.7;
            bar.MouseLeave += (_, _) => bar.Opacity = 1.0;
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, barAreaHeight - barHeight);
            ChartCanvas.Children.Add(bar);

            // 每3小时标注
            if (h % 3 == 0)
            {
                var label = new TextBlock
                {
                    Text = $"{h}",
                    FontSize = 10,
                    Foreground = textBrush,
                    FontFamily = (FontFamily)Application.Current.TryFindResource("InterRegular")!,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(label, x - 2);
                Canvas.SetTop(label, barAreaHeight + 4);
                ChartCanvas.Children.Add(label);
            }
        }
    }
}
