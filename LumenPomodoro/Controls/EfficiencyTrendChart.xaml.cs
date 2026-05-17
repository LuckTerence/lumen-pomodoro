using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.Models;

namespace LumenPomodoro.Controls;

public partial class EfficiencyTrendChart : UserControl
{
    public static readonly DependencyProperty EfficiencyDataProperty =
        DependencyProperty.Register(nameof(EfficiencyData), typeof(List<EfficiencyDataPoint>), typeof(EfficiencyTrendChart),
            new PropertyMetadata(null, OnDataChanged));

    public List<EfficiencyDataPoint>? EfficiencyData
    {
        get => (List<EfficiencyDataPoint>?)GetValue(EfficiencyDataProperty);
        set => SetValue(EfficiencyDataProperty, value);
    }

    private List<EfficiencyDataPoint>? _lastRenderedData;
    private Size _lastRenderedSize;
    private int _lastThemeHash;

    public EfficiencyTrendChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += ThemeChangedHandler;
        Unloaded += (_, _) => Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= ThemeChangedHandler;
    }

    private void ThemeChangedHandler(Wpf.Ui.Appearance.ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        _lastThemeHash = 0;
        Render();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EfficiencyTrendChart)d;
        control._lastRenderedData = null;
        control.Render();
    }

    private void Render()
    {
        var data = EfficiencyData;
        if (data == null || data.Count < 2 || ActualWidth <= 0)
        {
            ChartCanvas.Children.Clear();
            _lastRenderedData = null;
            return;
        }

        var currentSize = new Size(ActualWidth, ActualHeight);
        var currentThemeHash = Application.Current.TryFindResource("AccentFillColorDefaultBrush")?.GetHashCode() ?? 0;
        if (ReferenceEquals(data, _lastRenderedData) && currentSize == _lastRenderedSize && currentThemeHash == _lastThemeHash && ChartCanvas.Children.Count > 0)
            return;

        _lastRenderedData = data;
        _lastRenderedSize = currentSize;
        _lastThemeHash = currentThemeHash;

        ChartCanvas.Children.Clear();

        var chartHeight = 100.0;
        var marginTop = 10.0;
        var marginLeft = 4.0;
        var marginRight = 4.0;
        var chartWidth = ActualWidth - marginLeft - marginRight;
        var stepX = chartWidth / (data.Count - 1);

        var accentBrush = Application.Current.TryFindResource("AccentFillColorDefaultBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 102, 204));
        var successBrush = Application.Current.TryFindResource("SuccessBrush") as Brush
                           ?? new SolidColorBrush(Color.FromRgb(16, 185, 129));
        var textBrush = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush
                        ?? Brushes.Gray;

        // 绘制完成率折线
        var completionPoints = new List<Point>();
        for (int i = 0; i < data.Count; i++)
        {
            var x = marginLeft + i * stepX;
            var y = marginTop + chartHeight - (data[i].CompletionRate * chartHeight);
            completionPoints.Add(new Point(x, y));
        }

        var completionLine = new Polyline
        {
            Points = new PointCollection(completionPoints),
            Stroke = accentBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        ChartCanvas.Children.Add(completionLine);

        // 绘制质量分折线（归一化到 0-1）
        var qualityPoints = new List<Point>();
        for (int i = 0; i < data.Count; i++)
        {
            var x = marginLeft + i * stepX;
            var normalizedQuality = (data[i].AvgQualityScore - 1) / 2; // 1-3 -> 0-1
            var y = marginTop + chartHeight - (normalizedQuality * chartHeight);
            qualityPoints.Add(new Point(x, y));
        }

        var qualityLine = new Polyline
        {
            Points = new PointCollection(qualityPoints),
            Stroke = successBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeDashArray = [4, 2]
        };
        ChartCanvas.Children.Add(qualityLine);

        // 数据点 + 标签
        for (int i = 0; i < data.Count; i++)
        {
            // 完成率点
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = accentBrush,
                ToolTip = $"{data[i].WeekStart:MM/dd} 周\n完成率: {data[i].CompletionRate:P0}\n平均质量: {data[i].AvgQualityScore:F1} 星"
            };
            Canvas.SetLeft(dot, completionPoints[i].X - 3);
            Canvas.SetTop(dot, completionPoints[i].Y - 3);
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
            Canvas.SetLeft(label, completionPoints[i].X - 12);
            Canvas.SetTop(label, marginTop + chartHeight + 6);
            ChartCanvas.Children.Add(label);
        }

        // 图例
        var legendY = marginTop + chartHeight + 22;
        var legendX = marginLeft;

        var completionLegend = new TextBlock
        {
            Text = "● 完成率",
            FontSize = 10,
            Foreground = accentBrush
        };
        Canvas.SetLeft(completionLegend, legendX);
        Canvas.SetTop(completionLegend, legendY);
        ChartCanvas.Children.Add(completionLegend);

        var qualityLegend = new TextBlock
        {
            Text = "● 质量分",
            FontSize = 10,
            Foreground = successBrush
        };
        Canvas.SetLeft(qualityLegend, legendX + 60);
        Canvas.SetTop(qualityLegend, legendY);
        ChartCanvas.Children.Add(qualityLegend);
    }
}
