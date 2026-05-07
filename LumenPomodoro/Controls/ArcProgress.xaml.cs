using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LumenPomodoro.Controls;

public partial class ArcProgress : UserControl
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ArcProgress),
            new PropertyMetadata(100.0, OnProgressChanged));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ArcProgress),
            new PropertyMetadata(4.0, OnProgressChanged));

    public static readonly DependencyProperty ForegroundArcBrushProperty =
        DependencyProperty.Register(nameof(ForegroundArcBrush), typeof(Brush), typeof(ArcProgress),
            new PropertyMetadata(null, OnProgressChanged));

    public static readonly DependencyProperty BackgroundArcBrushProperty =
        DependencyProperty.Register(nameof(BackgroundArcBrush), typeof(Brush), typeof(ArcProgress),
            new PropertyMetadata(null, OnProgressChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush ForegroundArcBrush
    {
        get => (Brush)GetValue(ForegroundArcBrushProperty);
        set => SetValue(ForegroundArcBrushProperty, value);
    }

    public Brush BackgroundArcBrush
    {
        get => (Brush)GetValue(BackgroundArcBrushProperty);
        set => SetValue(BackgroundArcBrushProperty, value);
    }

    public ArcProgress()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateArcs();
        Loaded += (_, _) => UpdateArcs();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ArcProgress)d).UpdateArcs();
    }

    private void UpdateArcs()
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var center = size / 2;
        var radius = center - StrokeThickness / 2;
        if (radius <= 0) return;

        BackgroundArc.Data = CreateCircleGeometry(center, radius);
        BackgroundArc.Stroke = BackgroundArcBrush ?? new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
        BackgroundArc.StrokeThickness = StrokeThickness;

        ForegroundArc.Data = CreateArcGeometry(center, radius, Progress / 100.0);
        ForegroundArc.Stroke = ForegroundArcBrush ?? (Brush)Application.Current.FindResource("AccentFillColorDefaultBrush");
        ForegroundArc.StrokeThickness = StrokeThickness;
        ForegroundArc.StrokeStartLineCap = PenLineCap.Round;
        ForegroundArc.StrokeEndLineCap = PenLineCap.Round;
    }

    private static Geometry CreateCircleGeometry(double center, double radius)
    {
        var figure = new PathFigure { StartPoint = new Point(center, center - radius) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(center - 0.001, center - radius),
            Size = new Size(radius, radius),
            IsLargeArc = true,
            SweepDirection = SweepDirection.Clockwise
        });
        return new PathGeometry { Figures = { figure } };
    }

    private static Geometry CreateArcGeometry(double center, double radius, double fraction)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        if (fraction <= 0) return Geometry.Empty;

        var startAngle = -Math.PI / 2;
        var endAngle = startAngle + 2 * Math.PI * fraction;

        var startX = center + radius * Math.Cos(startAngle);
        var startY = center + radius * Math.Sin(startAngle);
        var endX = center + radius * Math.Cos(endAngle);
        var endY = center + radius * Math.Sin(endAngle);

        var figure = new PathFigure { StartPoint = new Point(startX, startY) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            IsLargeArc = fraction > 0.5,
            SweepDirection = SweepDirection.Clockwise
        });
        return new PathGeometry { Figures = { figure } };
    }
}
