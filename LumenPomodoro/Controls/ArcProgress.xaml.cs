using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

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

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(nameof(AnimationDuration), typeof(Duration), typeof(ArcProgress),
            new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(300))));

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

    public Duration AnimationDuration
    {
        get => (Duration)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    private double _currentProgressFraction = 1.0;
    private DoubleAnimation? _currentAnimation;
    private Action<object?, EventArgs>? _runningRendering;
    private bool _isRenderingSubscribed;
    private double _animStartFraction;
    private double _animTargetFraction;
    private DateTime _animStartTime;
    private TimeSpan _animDuration;

    public ArcProgress()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateArcs();
        Loaded += (_, _) => UpdateArcs();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ArcProgress)d).OnProgressValueChanged();
    }

    private void OnProgressValueChanged()
    {
        var targetFraction = Math.Clamp(Progress / 100.0, 0.0, 1.0);

        if (!IsLoaded || AnimationDuration.TimeSpan.TotalMilliseconds <= 0)
        {
            _currentProgressFraction = targetFraction;
            UpdateArcs();
            return;
        }

        if (_runningRendering != null)
        {
            _runningRendering -= OnAnimationStep;
        }

        _animStartFraction = _currentProgressFraction;
        _animTargetFraction = targetFraction;
        _animStartTime = DateTime.Now;
        _animDuration = AnimationDuration.TimeSpan;

        _currentAnimation = new DoubleAnimation(0, 1, AnimationDuration);
        _currentAnimation.Completed += Animation_Completed;

        if (!_isRenderingSubscribed)
        {
            _isRenderingSubscribed = true;
            CompositionTarget.Rendering += OnRendering;
        }

        _runningRendering += OnAnimationStep;
        RenderArcs(_currentProgressFraction);
    }

    private void OnAnimationStep(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.Now - _animStartTime).TotalMilliseconds;
        var durationMs = _animDuration.TotalMilliseconds;
        if (durationMs <= 0) durationMs = 1;
        var progress = Math.Min(1.0, elapsed / durationMs);
        var eased = EaseOutQuad(progress);
        var easedValue = _animStartFraction + (_animTargetFraction - _animStartFraction) * eased;
        RenderArcs(easedValue);
        if (progress >= 1.0)
        {
            _runningRendering -= OnAnimationStep;
            _currentProgressFraction = _animTargetFraction;
            RenderArcs(_animTargetFraction);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        _runningRendering?.Invoke(sender, e);
    }

    private static double EaseOutQuad(double t) => 1 - (1 - t) * (1 - t);

    private void Animation_Completed(object? sender, EventArgs e)
    {
        _currentAnimation = null;
    }

    private void UpdateArcs()
    {
        RenderArcs(_currentProgressFraction);
    }

    private void RenderArcs(double fraction)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var center = size / 2;
        var radius = center - StrokeThickness / 2;
        if (radius <= 0) return;

        BackgroundArc.Data = CreateCircleGeometry(center, radius);
        BackgroundArc.Stroke = BackgroundArcBrush ?? new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
        BackgroundArc.StrokeThickness = StrokeThickness;

        ForegroundArc.Data = CreateArcGeometry(center, radius, fraction);
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
