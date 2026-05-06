using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LumenPomodoro.Services;

namespace LumenPomodoro.Views;

public partial class StatsWindow : Window
{
    private readonly StorageService _storageService;

    public StatsWindow(StorageService storageService)
    {
        InitializeComponent();
        _storageService = storageService;
        LoadStats();
    }

    private void LoadStats()
    {
        var stats = _storageService.GetTodayStats();

        PomodoroCountText.Text = stats.CompletedPomodoros.ToString();
        FocusMinutesText.Text = stats.TotalFocusMinutes.ToString();

        TaskStatsPanel.Children.Clear();

        if (stats.TaskStats.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "今日暂无专注记录",
                FontSize = 14,
                Foreground = (Brush)FindResource("TertiaryTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 0)
            };
            TaskStatsPanel.Children.Add(emptyText);
            return;
        }

        var maxCount = stats.TaskStats.Values.Max();

        foreach (var kvp in stats.TaskStats.OrderByDescending(s => s.Value))
        {
            var card = new Border
            {
                Background = (Brush)FindResource("CardBackgroundBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var panel = new StackPanel();

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var nameText = new TextBlock
            {
                Text = kvp.Key,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = (Brush)FindResource("PrimaryTextBrush")
            };
            var countText = new TextBlock
            {
                Text = $"{kvp.Value} 个番茄钟",
                FontSize = 13,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(countText, Dock.Right);
            header.Children.Add(countText);
            header.Children.Add(nameText);
            panel.Children.Add(header);

            var barGrid = new Grid
            {
                Height = 6
            };

            var barBg = new Rectangle
            {
                Fill = (Brush)FindResource("BorderBrush"),
                RadiusX = 3,
                RadiusY = 3
            };

            var fillPct = maxCount > 0 ? (double)kvp.Value / maxCount : 0;
            var barFill = new Rectangle
            {
                Fill = (Brush)FindResource("PrimaryBrush"),
                RadiusX = 3,
                RadiusY = 3,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            barGrid.Loaded += (s, e) =>
            {
                barFill.Width = barGrid.ActualWidth * fillPct;
            };

            barGrid.Children.Add(barBg);
            barGrid.Children.Add(barFill);
            panel.Children.Add(barGrid);

            card.Child = panel;
            TaskStatsPanel.Children.Add(card);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
