using System.Windows;
using LumenPomodoro.Models;

namespace LumenPomodoro.Views;

public partial class DailyReportDialog : Window
{
    public DailyReportDialog(DailyReport report)
    {
        InitializeComponent();

        DateText.Text = report.Date.ToString("yyyy年M月d日");
        PomodoroCountText.Text = report.CompletedPomodoros.ToString();
        MinutesText.Text = report.TotalMinutes.ToString();
        MainTaskText.Text = report.MainTask;
        StreakText.Text = report.StreakDays > 0
            ? $"已连续学习 {report.StreakDays} 天"
            : "继续加油，保持学习节奏";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
