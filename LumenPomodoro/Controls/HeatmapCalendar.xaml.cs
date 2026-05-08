using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.Models;

namespace LumenPomodoro.Controls;

public partial class HeatmapCalendar : UserControl
{
    public static readonly DependencyProperty HeatmapDaysProperty =
        DependencyProperty.Register(nameof(HeatmapDays), typeof(List<HeatmapDay>), typeof(HeatmapCalendar),
            new PropertyMetadata(null, OnDataChanged));

    public List<HeatmapDay>? HeatmapDays
    {
        get => (List<HeatmapDay>?)GetValue(HeatmapDaysProperty);
        set => SetValue(HeatmapDaysProperty, value);
    }

    private static readonly string[] DayLabels = ["", "周一", "", "周三", "", "周五", ""];
    private static readonly string[] MonthLabels = ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"];

    public HeatmapCalendar()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HeatmapCalendar)d).Render();
    }

    private void Render()
    {
        HeatmapCanvas.Children.Clear();
        var data = HeatmapDays;
        if (data == null || data.Count == 0 || ActualWidth <= 0) return;

        var availableWidth = ActualWidth - 50;
        var gap = 3.0;
        var cellSize = Math.Max(4, Math.Floor((availableWidth - 52 * gap) / 53));
        var cellPlusGap = cellSize + gap;
        var marginLeft = 40.0;
        var marginTop = 18.0;

        var accentBrush = Application.Current.TryFindResource("AccentFillColorDefaultBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 102, 204));

        var bgBrush = Application.Current.TryFindResource("CardBackgroundFillColorSecondaryBrush") as Brush
                      ?? new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));

        var levelBrushes = new[]
        {
            bgBrush,
            new SolidColorBrush(((SolidColorBrush)accentBrush).Color) { Opacity = 0.2 },
            new SolidColorBrush(((SolidColorBrush)accentBrush).Color) { Opacity = 0.45 },
            new SolidColorBrush(((SolidColorBrush)accentBrush).Color) { Opacity = 0.7 },
            accentBrush
        };

        var textBrush = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush
                        ?? Brushes.Gray;

        // 日期标签
        for (int row = 0; row < 7; row++)
        {
            if (!string.IsNullOrEmpty(DayLabels[row]))
            {
                var label = new TextBlock
                {
                    Text = DayLabels[row],
                    FontSize = 10,
                    Foreground = textBrush,
                    FontFamily = (FontFamily)Application.Current.TryFindResource("InterRegular")!
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, marginTop + row * cellPlusGap - 2);
                HeatmapCanvas.Children.Add(label);
            }
        }

        // 月份标签（在列上方）
        if (data.Count == 365)
        {
            var startMonth = data[0].Date.Month;
            var monthMarked = new bool[12];
            for (int i = 0; i < 365; i++)
            {
                var m = data[i].Date.Month - 1;
                if (!monthMarked[m])
                {
                    monthMarked[m] = true;
                    var col = i / 7;
                    var mLabel = new TextBlock
                    {
                        Text = MonthLabels[m],
                        FontSize = 10,
                        Foreground = textBrush,
                        FontFamily = (FontFamily)Application.Current.TryFindResource("InterRegular")!
                    };
                    Canvas.SetLeft(mLabel, marginLeft + col * cellPlusGap);
                    Canvas.SetTop(mLabel, 0);
                    HeatmapCanvas.Children.Add(mLabel);
                }
            }
        }

        // 热力图格子
        for (int i = 0; i < data.Count && i < 365; i++)
        {
            var col = i / 7;
            var row = i % 7;
            var day = data[i];
            var level = Math.Clamp(day.IntensityLevel, 0, 4);

            var rect = new Rectangle
            {
                Width = cellSize,
                Height = cellSize,
                RadiusX = 2,
                RadiusY = 2,
                Fill = levelBrushes[level],
                ToolTip = $"{day.Date:yyyy-MM-dd}\n{day.FocusMinutes} 分钟"
            };
            Canvas.SetLeft(rect, marginLeft + col * cellPlusGap);
            Canvas.SetTop(rect, marginTop + row * cellPlusGap);
            HeatmapCanvas.Children.Add(rect);
        }

        HeatmapCanvas.Height = marginTop + 7 * cellPlusGap + 4;
    }
}
