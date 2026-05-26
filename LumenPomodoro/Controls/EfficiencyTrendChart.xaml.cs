using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.Models;

namespace LumenPomodoro.Controls;

public partial class EfficiencyTrendChart : ChartBase
{
    public static readonly DependencyProperty EfficiencyDataProperty =
        DependencyProperty.Register(nameof(EfficiencyData), typeof(List<EfficiencyDataPoint>), typeof(EfficiencyTrendChart),
            new PropertyMetadata(null, OnDataChanged));

    public List<EfficiencyDataPoint>? EfficiencyData
    {
        get => (List<EfficiencyDataPoint>?)GetValue(EfficiencyDataProperty);
        set => SetValue(EfficiencyDataProperty, value);
    }

    public EfficiencyTrendChart()
    {
        InitializeComponent();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EfficiencyTrendChart)d;
        control.InvalidateCache();
        control.Render();
    }

    protected override void Render()
    {
        var data = EfficiencyData;
        if (data == null || data.Count < 2)
        {
            ChartCanvas.Children.Clear();
            return;
        }
        if (SkipIfUnchanged(data)) return;

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
        var textBrush = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray;

        var completionPoints = new List<Point>();
        for (int i = 0; i < data.Count; i++)
        {
            var x = marginLeft + i * stepX;
            var y = marginTop + chartHeight - (data[i].CompletionRate * chartHeight);
            completionPoints.Add(new Point(x, y));
        }

        ChartCanvas.Children.Add(new Polyline
        {
            Points = new PointCollection(completionPoints),
            Stroke = accentBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        });

        var qualityPoints = new List<Point>();
        for (int i = 0; i < data.Count; i++)
        {
            var x = marginLeft + i * stepX;
            var normalizedQuality = (data[i].AvgQualityScore - 1) / 2;
            var y = marginTop + chartHeight - (normalizedQuality * chartHeight);
            qualityPoints.Add(new Point(x, y));
        }

        ChartCanvas.Children.Add(new Polyline
        {
            Points = new PointCollection(qualityPoints),
            Stroke = successBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeDashArray = [4, 2]
        });

        for (int i = 0; i < data.Count; i++)
        {
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

        var legendY = marginTop + chartHeight + 22;

        var completionLegend = new TextBlock { Text = "● 完成率", FontSize = 10, Foreground = accentBrush };
        Canvas.SetLeft(completionLegend, marginLeft);
        Canvas.SetTop(completionLegend, legendY);
        ChartCanvas.Children.Add(completionLegend);

        var qualityLegend = new TextBlock { Text = "● 质量分", FontSize = 10, Foreground = successBrush };
        Canvas.SetLeft(qualityLegend, marginLeft + 60);
        Canvas.SetTop(qualityLegend, legendY);
        ChartCanvas.Children.Add(qualityLegend);
    }
}
