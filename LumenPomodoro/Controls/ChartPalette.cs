using System.Collections.Generic;
using System.Windows.Media;

namespace LumenPomodoro.Controls;

/// <summary>
/// 图表集中调色板 — 所有 Charts 共享。与 CustomStyles.xaml token 保持一致。
/// </summary>
public static class ChartPalette
{
    public static readonly SolidColorBrush Accent = new(Color.FromRgb(29, 185, 84));
    public static readonly SolidColorBrush Success = new(Color.FromRgb(29, 185, 84));
    public static readonly SolidColorBrush Warning = new(Color.FromRgb(245, 166, 35));
    public static readonly SolidColorBrush Danger = new(Color.FromRgb(241, 94, 108));
    public static readonly SolidColorBrush Info = new(Color.FromRgb(74, 144, 217));

    public static readonly SolidColorBrush Canvas = new(Color.FromRgb(18, 18, 18));
    public static readonly SolidColorBrush SurfaceOverlay = new(Color.FromRgb(40, 40, 40));
    public static readonly SolidColorBrush TextPrimary = new(Color.FromRgb(255, 255, 255));
    public static readonly SolidColorBrush TextTertiary = new(Color.FromRgb(106, 106, 106));

    public static readonly SolidColorBrush BackgroundSubtle = new(Color.FromArgb(26, 128, 128, 128));

    public static readonly IReadOnlyList<SolidColorBrush> Series = new[]
    {
        new SolidColorBrush(Color.FromRgb(29, 185, 84)),
        new SolidColorBrush(Color.FromRgb(74, 144, 217)),
        new SolidColorBrush(Color.FromRgb(245, 166, 35)),
        new SolidColorBrush(Color.FromRgb(241, 94, 108)),
        new SolidColorBrush(Color.FromRgb(139, 92, 246)),
        new SolidColorBrush(Color.FromRgb(52, 211, 153)),
    };
}
