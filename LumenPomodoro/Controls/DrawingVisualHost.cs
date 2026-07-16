using System.Windows;
using System.Windows.Media;

namespace LumenPomodoro.Controls;

/// <summary>
/// Lightweight host for DrawingVisual-based rendering.
/// Avoids the overhead of UIElement children in Canvas.
/// Supports optional hit-test rectangle for tooltip.
/// </summary>
public class DrawingVisualHost : FrameworkElement
{
    private readonly DrawingVisual _visual;
    private Rect _hitTestBounds;

    public DrawingVisualHost()
    {
        _visual = new DrawingVisual();
        AddVisualChild(_visual);
    }

    public DrawingContext RenderOpen() => _visual.RenderOpen();

    /// <summary>
    /// Set the hit-test bounds for tooltip support.
    /// </summary>
    public void SetHitTestBounds(Rect bounds) => _hitTestBounds = bounds;

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParams)
    {
        if (_hitTestBounds.Contains(hitTestParams.HitPoint))
            return new PointHitTestResult(this, hitTestParams.HitPoint);
        return null!;
    }

    /// <summary>
    /// Invalidate the visual to trigger re-render.
    /// </summary>
    public void InvalidateRender() => InvalidateVisual();
}
