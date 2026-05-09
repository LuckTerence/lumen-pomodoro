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

    private static readonly string[] DayLabels = ["周一", "", "周三", "", "周五", "", ""];
    private static readonly string[] MonthLabels = ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"];

    public HeatmapCalendar()
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
        ((HeatmapCalendar)d).Render();
    }

    private void Render()
    {
        HeatmapCanvas.Children.Clear();
        var data = HeatmapDays;
        if (data == null || data.Count == 0 || ActualWidth <= 0) return;

        var firstDate = data.Min(d => d.Date.Date);
        var lastDate = data.Max(d => d.Date.Date);
        var firstRow = GetMondayBasedRow(firstDate);
        var gridStart = firstDate.AddDays(-firstRow);
        var totalColumns = Math.Max(1, ((lastDate - gridStart).Days / 7) + 1);

        var marginLeft = 40.0;
        var marginTop = 18.0;
        var availableWidth = Math.Max(0, ActualWidth - marginLeft - 4);
        var gap = 3.0;
        var cellSize = Math.Clamp(Math.Floor((availableWidth - (totalColumns - 1) * gap) / totalColumns), 5, 10);
        var cellPlusGap = cellSize + gap;

        var accentBrush = Application.Current.TryFindResource("AccentFillColorDefaultBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 102, 204));

        var bgBrush = Application.Current.TryFindResource("CardBackgroundFillColorSecondaryBrush") as Brush
                      ?? new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));

        var accentColor = GetBrushColor(accentBrush, Color.FromRgb(0, 102, 204));
        var levelBrushes = new Brush[]
        {
            bgBrush,
            new SolidColorBrush(accentColor) { Opacity = 0.2 },
            new SolidColorBrush(accentColor) { Opacity = 0.45 },
            new SolidColorBrush(accentColor) { Opacity = 0.7 },
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
                    Foreground = textBrush
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, marginTop + row * cellPlusGap - 2);
                HeatmapCanvas.Children.Add(label);
            }
        }

        // 月份标签按该月在热力图中第一次出现的位置落点
        var monthMarked = new bool[12];
        foreach (var day in data.OrderBy(d => d.Date))
        {
            var monthIndex = day.Date.Month - 1;
            if (monthMarked[monthIndex])
            {
                continue;
            }

            monthMarked[monthIndex] = true;
            var col = (day.Date.Date - gridStart).Days / 7;
            var mLabel = new TextBlock
            {
                Text = MonthLabels[monthIndex],
                FontSize = 10,
                Foreground = textBrush
            };
            Canvas.SetLeft(mLabel, marginLeft + col * cellPlusGap);
            Canvas.SetTop(mLabel, 0);
            HeatmapCanvas.Children.Add(mLabel);
        }

        // 热力图格子
        foreach (var day in data.OrderBy(d => d.Date))
        {
            var col = (day.Date.Date - gridStart).Days / 7;
            var row = GetMondayBasedRow(day.Date.Date);
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
        HeatmapCanvas.Width = marginLeft + totalColumns * cellSize + (totalColumns - 1) * gap + 4;
    }

    private static int GetMondayBasedRow(DateTime date)
    {
        return ((int)date.DayOfWeek + 6) % 7;
    }

    private static Color GetBrushColor(Brush brush, Color fallback)
    {
        return brush is SolidColorBrush solid ? solid.Color : fallback;
    }
}
