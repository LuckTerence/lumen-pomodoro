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

    private List<HourlyDataPoint>? _lastRenderedData;
    private Size _lastRenderedSize;
    private int _lastThemeHash;

    public HourlyDistributionChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += ThemeChangedHandler;
        Unloaded += (_, _) => Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= ThemeChangedHandler;
    }

    private void ThemeChangedHandler(Wpf.Ui.Appearance.ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        _lastThemeHash = 0; // 强制重绘
        Render();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HourlyDistributionChart)d;
        control._lastRenderedData = null; // 强制重绘
        control.Render();
    }

    private void Render()
    {
        var data = HourlyData;
        if (data == null || data.Count == 0 || ActualWidth <= 0)
        {
            ChartCanvas.Children.Clear();
            _lastRenderedData = null;
            return;
        }

        // 数据、尺寸、主题未变时跳过重绘
        var currentSize = new Size(ActualWidth, ActualHeight);
        var currentThemeHash = Application.Current.TryFindResource("AccentFillColorDefaultBrush")?.GetHashCode() ?? 0;
        if (ReferenceEquals(data, _lastRenderedData) && currentSize == _lastRenderedSize && currentThemeHash == _lastThemeHash && ChartCanvas.Children.Count > 0)
            return;

        _lastRenderedData = data;
        _lastRenderedSize = currentSize;
        _lastThemeHash = currentThemeHash;

        ChartCanvas.Children.Clear();

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
                    FontFamily = FindResource("InterRegular") as FontFamily,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(label, x - 2);
                Canvas.SetTop(label, barAreaHeight + 4);
                ChartCanvas.Children.Add(label);
            }
        }
    }
}
