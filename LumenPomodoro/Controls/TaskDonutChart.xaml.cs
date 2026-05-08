using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.Models;

namespace LumenPomodoro.Controls;

public partial class TaskDonutChart : UserControl
{
    public static readonly DependencyProperty TaskSlicesProperty =
        DependencyProperty.Register(nameof(TaskSlices), typeof(List<TaskSlice>), typeof(TaskDonutChart),
            new PropertyMetadata(null, OnDataChanged));

    public List<TaskSlice>? TaskSlices
    {
        get => (List<TaskSlice>?)GetValue(TaskSlicesProperty);
        set => SetValue(TaskSlicesProperty, value);
    }

    public TaskDonutChart()
    {
        InitializeComponent();
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += ThemeChangedHandler;
        Unloaded += (_, _) => Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= ThemeChangedHandler;
    }

    private void ThemeChangedHandler(Wpf.Ui.Appearance.ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        Render();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TaskDonutChart)d).Render();
    }

    private void Render()
    {
        DonutCanvas.Children.Clear();
        LegendPanel.Children.Clear();
        var slices = TaskSlices;
        if (slices == null || slices.Count == 0) return;

        var total = slices.Sum(s => s.PomodoroCount);
        var center = 60.0;
        var radius = 42.0;
        var thickness = 18.0;

        // 中心文字（Measure 居中）
        var totalText = new TextBlock
        {
            Text = total.ToString(),
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            FontFamily = (FontFamily)Application.Current.TryFindResource("InterSemiBold")!,
            Foreground = Application.Current.TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.White
        };
        totalText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(totalText, center - totalText.DesiredSize.Width / 2);
        Canvas.SetTop(totalText, center - totalText.DesiredSize.Height / 2 - 6);
        DonutCanvas.Children.Add(totalText);

        var subText = new TextBlock
        {
            Text = "总计",
            FontSize = 10,
            Foreground = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray,
            FontFamily = (FontFamily)Application.Current.TryFindResource("InterRegular")!
        };
        subText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(subText, center - subText.DesiredSize.Width / 2);
        Canvas.SetTop(subText, center - subText.DesiredSize.Height / 2 + 12);
        DonutCanvas.Children.Add(subText);

        // 环形切片
        double cumulativeAngle = -90; // 从 12 点钟方向开始
        foreach (var slice in slices)
        {
            var sweepAngle = slice.Percentage / 100.0 * 360.0;
            if (sweepAngle < 1.0) sweepAngle = 1.0; // 最小 1 度

            var color = ParseColor(slice.TaskColor);
            var geometry = CreateArcGeometry(center, radius, cumulativeAngle, sweepAngle);
            var path = new Path
            {
                Data = geometry,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            DonutCanvas.Children.Add(path);

            cumulativeAngle += sweepAngle;

            // 图例项
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{slice.TaskName}  {slice.PomodoroCount} ({slice.Percentage:F0}%)",
                FontSize = 12,
                FontFamily = (FontFamily)Application.Current.TryFindResource("InterRegular")!,
                Foreground = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            });
            LegendPanel.Children.Add(row);
        }
    }

    private static Geometry CreateArcGeometry(double center, double radius, double startAngleDeg, double sweepAngleDeg)
    {
        if (sweepAngleDeg <= 0) return Geometry.Empty;

        if (sweepAngleDeg >= 359.9)
        {
            var fig = new PathFigure { StartPoint = new Point(center, center - radius) };
            fig.Segments.Add(new ArcSegment
            {
                Point = new Point(center - 0.001, center - radius),
                Size = new Size(radius, radius),
                IsLargeArc = true,
                SweepDirection = SweepDirection.Clockwise
            });
            return new PathGeometry { Figures = { fig } };
        }

        var startRad = startAngleDeg * Math.PI / 180.0;
        var endRad = (startAngleDeg + sweepAngleDeg) * Math.PI / 180.0;

        var startX = center + radius * Math.Cos(startRad);
        var startY = center + radius * Math.Sin(startRad);
        var endX = center + radius * Math.Cos(endRad);
        var endY = center + radius * Math.Sin(endRad);

        var figure = new PathFigure { StartPoint = new Point(startX, startY) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            IsLargeArc = sweepAngleDeg > 180,
            SweepDirection = SweepDirection.Clockwise
        });
        return new PathGeometry { Figures = { figure } };
    }

    private static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || !hex.StartsWith('#')) return Color.FromRgb(59, 130, 246);
        try
        {
            var r = Convert.ToByte(hex.Substring(1, 2), 16);
            var g = Convert.ToByte(hex.Substring(3, 2), 16);
            var b = Convert.ToByte(hex.Substring(5, 2), 16);
            return Color.FromRgb(r, g, b);
        }
        catch
        {
            return Color.FromRgb(59, 130, 246);
        }
    }
}
