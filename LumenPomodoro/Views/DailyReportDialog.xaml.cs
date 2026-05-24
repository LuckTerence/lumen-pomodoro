using LumenPomodoro.Models;
using LumenPomodoro.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LumenPomodoro.Views;

public partial class DailyReportDialog : Window
{
    public DailyReportDialog(DailyReport report)
    {
        InitializeComponent();
        DataContext = this;

        DateText.Text = report.Date.ToString("yyyy年M月d日") + " 专注总结";
        PomodoroCountText.Text = report.CompletedPomodoros.ToString();
        MinutesText.Text = report.TotalMinutes.ToString();
        MainTaskText.Text = string.IsNullOrEmpty(report.MainTask) ? "未记录具体任务" : report.MainTask;

        FocusSessionsText.Text = $"完成专注会话: {report.CompletedPomodoros} 个";
        AvgQualityText.Text = $"平均质量: {report.AvgQualityScore:F1} / 5.0";
        TotalTasksText.Text = $"完成科目: {report.UniqueTasksCount} 个";
        BestStreakText.Text = $"当日连击: {report.StreakDays} 天";

        if (report.StreakDays > 0)
        {
            StreakText.Text = $"已连续专注 {report.StreakDays} 天 · 再接再厉！";
        }
        else
        {
            StreakText.Text = "今天是新的开始，保持专注节奏！";
        }

        EncouragementText.Text = GetEncouragementText(report);

        if (!string.IsNullOrEmpty(report.CategorySuggestion))
        {
            CategorySuggestionText.Text = $"💡 {report.CategorySuggestion}";
            CategorySuggestionText.Visibility = Visibility.Visible;
        }
        else
        {
            CategorySuggestionText.Visibility = Visibility.Collapsed;
        }

        ShowAchievementBadge(report);
    }

    private string GetEncouragementText(DailyReport report)
    {
        var total = report.TotalMinutes;
        var pomodoros = report.CompletedPomodoros;

        if (total >= 240)
        {
            return "今天的你非常出色！4小时以上的专注是迈向精通的重要一步，继续保持这份热情！";
        }
        else if (total >= 180)
        {
            return "充实的一天！3小时的专注学习让你离目标更近了，明天继续加油！";
        }
        else if (total >= 120)
        {
            return "不错的进度！每天坚持2小时，积少成多终将看到改变！";
        }
        else if (total >= 60)
        {
            return "良好的开端！每天进步一点点，积累起来就是巨大的飞跃！";
        }
        else if (pomodoros >= 3)
        {
            return "完成了3个番茄钟！专注的质量比数量更重要，继续加油！";
        }
        else if (pomodoros >= 1)
        {
            return "今天已经开始了！保持这个节奏，明天会更好！";
        }
        else
        {
            return "新的开始！每一次专注都是对未来的投资，明天继续努力！";
        }
    }

    private void ShowAchievementBadge(DailyReport report)
    {
        var total = report.TotalMinutes;
        var pomodoros = report.CompletedPomodoros;
        var streak = report.StreakDays;

        if (total >= 240)
        {
            AchievementIcon.Text = "[徽章]";
            AchievementTitle.Text = "专注大师";
            AchievementDesc.Text = "单日专注4小时+，了不起的自律！";
            AchievementBadge.Visibility = Visibility.Visible;
        }
        else if (streak >= 7)
        {
            AchievementIcon.Text = "[徽章]";
            AchievementTitle.Text = "一周挑战完成";
            AchievementDesc.Text = $"连续专注 {streak} 天，习惯正在养成！";
            AchievementBadge.Visibility = Visibility.Visible;
        }
        else if (streak >= 3)
        {
            AchievementIcon.Text = "[徽章]";
            AchievementTitle.Text = "三天连击";
            AchievementDesc.Text = $"连续 {streak} 天，好习惯正在形成！";
            AchievementBadge.Visibility = Visibility.Visible;
        }
        else if (pomodoros >= 8)
        {
            AchievementIcon.Text = "[徽章]";
            AchievementTitle.Text = "高效达人";
            AchievementDesc.Text = $"完成 {pomodoros} 个番茄钟，今天收获满满！";
            AchievementBadge.Visibility = Visibility.Visible;
        }
        else if (total >= 180)
        {
            AchievementIcon.Text = "[徽章]";
            AchievementTitle.Text = "三小时突破";
            AchievementDesc.Text = "专注3小时+，今天的你很棒！";
            AchievementBadge.Visibility = Visibility.Visible;
        }
        else if (report.UniqueTasksCount >= 3)
        {
            AchievementIcon.Text = "[徽章]";
            AchievementTitle.Text = "全面发展";
            AchievementDesc.Text = $"今天学习了 {report.UniqueTasksCount} 个不同科目！";
            AchievementBadge.Visibility = Visibility.Visible;
        }
        else
        {
            AchievementBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}