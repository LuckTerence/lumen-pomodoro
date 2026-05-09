using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.Models;

namespace LumenPomodoro.Controls;

public partial class WeeklyTrendChart : UserControl
{
    public static readonly DependencyProperty WeeklyDataProperty =
        DependencyProperty.Register(nameof(WeeklyData), typeof(List<WeeklyDataPoint>), typeof(WeeklyTrendChart),
            new PropertyMetadata(null, OnDataChanged));

    public List<WeeklyDataPoint>? WeeklyData
    {
        get => (List<WeeklyDataPoint>?)GetValue(WeeklyDataProperty);
        set => SetValue(WeeklyDataProperty, value);
    }

    public WeeklyTrendChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += ThemeChangedHandler;
        Unloaded += (_, _) => Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= ThemeChangedHandler;
    }

    private void ThemeChangedHandler(Wpf.Ui.Appearance.ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        Render();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((WeeklyTrendChart)d).Render();
    }

    private void Render()
    {
        ChartCanvas.Children.Clear();
        var data = WeeklyData;
        if (data == null || data.Count < 2 || ActualWidth <= 0) return;

        var maxMinutes = data.Max(d => d.TotalMinutes);
        if (maxMinutes == 0) maxMinutes = 1;

        var chartHeight = 100.0;
        var marginTop = 10.0;
        var marginLeft = 4.0;
        var marginRight = 4.0;
        var chartWidth = ActualWidth - marginLeft - marginRight;
        var stepX = chartWidth / (data.Count - 1);

        var accentBrush = Application.Current.TryFindResource("AccentFillColorDefaultBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 102, 204));
        var textBrush = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush
                        ?? Brushes.Gray;
        var accentColor = ((SolidColorBrush)accentBrush).Color;

        // 计算数据点
        var points = new List<Point>();
        for (int i = 0; i < data.Count; i++)
        {
            var x = marginLeft + i * stepX;
            var y = marginTop + chartHeight - ((double)data[i].TotalMinutes / maxMinutes * chartHeight);
            points.Add(new Point(x, y));
        }

        // 填充区域
        var fillPoints = new PointCollection(points);
        fillPoints.Add(new Point(points[^1].X, marginTop + chartHeight));
        fillPoints.Add(new Point(points[0].X, marginTop + chartHeight));
        var fillPolygon = new Polygon
        {
            Points = fillPoints,
            Fill = new SolidColorBrush(accentColor) { Opacity = 0.15 }
        };
        ChartCanvas.Children.Add(fillPolygon);

        // 折线
        var linePoints = new PointCollection(points);
        var polyline = new Polyline
        {
            Points = linePoints,
            Stroke = accentBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        ChartCanvas.Children.Add(polyline);

        // 数据点 + 标签
        for (int i = 0; i < points.Count; i++)
        {
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = accentBrush,
                ToolTip = $"{data[i].WeekStart:MM/dd} 周\n{data[i].TotalMinutes} 分钟 · {data[i].CompletedPomodoros} 个番茄"
            };
            Canvas.SetLeft(dot, points[i].X - 3);
            Canvas.SetTop(dot, points[i].Y - 3);
            ChartCanvas.Children.Add(dot);

            // 周标签
            var label = new TextBlock
            {
                Text = data[i].WeekStart.ToString("M/d"),
                FontSize = 9,
                Foreground = textBrush,
                FontFamily = FindResource("InterRegular") as FontFamily,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(label, points[i].X - 12);
            Canvas.SetTop(label, marginTop + chartHeight + 6);
            ChartCanvas.Children.Add(label);
        }

        // 平均线
        var avgMinutes = data.Average(d => d.TotalMinutes);
        var avgY = marginTop + chartHeight - (avgMinutes / maxMinutes * chartHeight);
        var avgLine = new Line
        {
            X1 = marginLeft,
            X2 = marginLeft + chartWidth,
            Y1 = avgY,
            Y2 = avgY,
            Stroke = textBrush,
            StrokeThickness = 1,
            StrokeDashArray = [4, 4],
            Opacity = 0.4
        };
        ChartCanvas.Children.Add(avgLine);

        var avgLabel = new TextBlock
        {
            Text = $"均值 {(int)avgMinutes}分",
            FontSize = 9,
            Foreground = textBrush,
            Opacity = 0.6
        };
        Canvas.SetLeft(avgLabel, marginLeft);
        Canvas.SetTop(avgLabel, avgY - 14);
        ChartCanvas.Children.Add(avgLabel);
    }
}
