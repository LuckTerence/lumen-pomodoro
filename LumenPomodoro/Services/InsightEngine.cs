using LumenPomodoro.Models;

namespace LumenPomodoro.Services;

public class InsightEngine
{
    private static readonly string[] DayNames = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];

    public List<HeatmapDay> GetHeatmapData(List<FocusSession> sessions)
    {
        var today = DateTime.Today;
        var startDate = today.AddDays(-364);

        var completedSessions = sessions
            .Where(s => s.Completed && s.EndTime.HasValue && s.EndTime.Value.Date >= startDate)
            .ToList();

        var dailyMinutes = completedSessions
            .GroupBy(s => s.EndTime!.Value.Date)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.FocusMinutes));

        var maxMinutes = dailyMinutes.Values.DefaultIfEmpty(0).Max();

        var result = new List<HeatmapDay>(365);
        for (int i = 0; i < 365; i++)
        {
            var date = startDate.AddDays(i);
            dailyMinutes.TryGetValue(date, out var minutes);

            int level = 0;
            if (minutes > 0 && maxMinutes > 0)
            {
                var ratio = (double)minutes / maxMinutes;
                level = ratio <= 0.25 ? 1 : ratio <= 0.5 ? 2 : ratio <= 0.75 ? 3 : 4;
            }

            result.Add(new HeatmapDay { Date = date, FocusMinutes = minutes, IntensityLevel = level });
        }

        return result;
    }

    public List<HourlyDataPoint> GetHourlyDistribution(List<FocusSession> sessions, DateTime start, DateTime end)
    {
        var filtered = sessions
            .Where(s => s.Completed && s.EndTime.HasValue
                && s.EndTime.Value.Date >= start.Date
                && s.EndTime.Value.Date <= end.Date)
            .ToList();

        var hourlyGroups = filtered
            .GroupBy(s => s.EndTime!.Value.Hour)
            .ToDictionary(g => g.Key, g => new { Minutes = g.Sum(s => s.FocusMinutes), Count = g.Count() });

        var result = new List<HourlyDataPoint>(24);
        for (int h = 0; h < 24; h++)
        {
            if (hourlyGroups.TryGetValue(h, out var data))
                result.Add(new HourlyDataPoint { Hour = h, TotalMinutes = data.Minutes, SessionCount = data.Count });
            else
                result.Add(new HourlyDataPoint { Hour = h, TotalMinutes = 0, SessionCount = 0 });
        }

        return result;
    }

    public List<TaskSlice> GetTaskBreakdown(List<FocusSession> sessions, DateTime start, DateTime end, List<TaskItem>? tasks = null)
    {
        var filtered = sessions
            .Where(s => s.Completed && s.EndTime.HasValue
                && s.EndTime.Value.Date >= start.Date
                && s.EndTime.Value.Date <= end.Date)
            .ToList();

        var total = filtered.Count;
        if (total == 0) return [];

        var colorMap = tasks?.Where(t => !string.IsNullOrEmpty(t.Name))
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First().Color)
            ?? new Dictionary<string, string>();

        return filtered
            .GroupBy(s => string.IsNullOrEmpty(s.TaskName) ? "未分类" : s.TaskName)
            .Select(g => new TaskSlice
            {
                TaskName = g.Key,
                TaskColor = colorMap.TryGetValue(g.Key, out var c) ? c : "#3B82F6",
                PomodoroCount = g.Count(),
                Percentage = Math.Round((double)g.Count() / total * 100, 1)
            })
            .OrderByDescending(s => s.PomodoroCount)
            .ToList();
    }

    public List<WeeklyDataPoint> GetWeeklyTrend(List<FocusSession> sessions)
    {
        var today = DateTime.Today;
        var thisMonday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        if (thisMonday > today) thisMonday = thisMonday.AddDays(-7);

        var result = new List<WeeklyDataPoint>(8);
        for (int i = 7; i >= 0; i--)
        {
            var weekStart = thisMonday.AddDays(-7 * i);
            var weekEnd = weekStart.AddDays(7);

            var weekSessions = sessions
                .Where(s => s.Completed && s.EndTime.HasValue
                    && s.EndTime.Value.Date >= weekStart.Date
                    && s.EndTime.Value.Date < weekEnd.Date)
                .ToList();

            result.Add(new WeeklyDataPoint
            {
                WeekStart = weekStart,
                TotalMinutes = weekSessions.Sum(s => s.FocusMinutes),
                CompletedPomodoros = weekSessions.Count
            });
        }

        return result;
    }

    public List<Insight> GetInsights(List<FocusSession> sessions, List<TaskItem> tasks)
    {
        var insights = new List<Insight>();
        var completed = sessions.Where(s => s.Completed && s.EndTime.HasValue).ToList();

        if (completed.Count < 3)
        {
            insights.Add(new Insight
            {
                Title = "开始你的专注之旅",
                Description = "完成更多番茄钟后，这里会为你生成专属的学习洞察。",
                Type = InsightType.Motivation
            });
            return insights;
        }

        // 1. 峰值时段
        var hourGroups = completed.GroupBy(s => s.EndTime!.Value.Hour);
        var bestHour = hourGroups
            .Select(g => new { Hour = g.Key, Avg = g.Average(s => s.FocusMinutes), Count = g.Count() })
            .Where(x => x.Count >= 3)
            .OrderByDescending(x => x.Avg)
            .FirstOrDefault();

        if (bestHour != null)
        {
            insights.Add(new Insight
            {
                Title = "你的黄金时段",
                Description = $"{bestHour.Hour}:00 左右是你效率最高的时段，平均每次专注 {(int)bestHour.Avg} 分钟。",
                Type = InsightType.PeakHour
            });
        }

        // 2. 最佳日期
        var dayGroups = completed
            .GroupBy(s => s.EndTime!.Value.DayOfWeek)
            .Where(g => g.Count() >= 3)
            .Select(g => new { Day = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        if (dayGroups != null)
        {
            insights.Add(new Insight
            {
                Title = "高效日",
                Description = $"{DayNames[(int)dayGroups.Day]}是你最高效的一天，累计完成 {dayGroups.Total} 个番茄钟。",
                Type = InsightType.BestDay
            });
        }

        // 3. 趋势检测
        var fourWeeksAgo = DateTime.Today.AddDays(-28);
        var eightWeeksAgo = DateTime.Today.AddDays(-56);
        var recent4Weeks = completed.Count(s => s.EndTime!.Value.Date >= fourWeeksAgo);
        var prior4Weeks = completed.Count(s => s.EndTime!.Value.Date >= eightWeeksAgo && s.EndTime.Value.Date < fourWeeksAgo);

        if (prior4Weeks > 0)
        {
            var change = (double)(recent4Weeks - prior4Weeks) / prior4Weeks;
            if (change > 0.15)
            {
                insights.Add(new Insight
                {
                    Title = "上升趋势",
                    Description = $"最近 4 周比之前多完成了 {(int)(change * 100)}% 的番茄钟，继续保持！",
                    Type = InsightType.Trend
                });
            }
            else if (change < -0.15)
            {
                insights.Add(new Insight
                {
                    Title = "节奏调整",
                    Description = "最近专注时长有所下降，试试缩短单次时间或调整学习科目。",
                    Type = InsightType.Trend
                });
            }
        }

        // 4. 连续天数
        var streak = CalculateStreak(completed);
        if (streak >= 3)
        {
            insights.Add(new Insight
            {
                Title = "连续专注",
                Description = $"你已经连续专注 {streak} 天了，保持这个节奏！",
                Type = InsightType.Streak
            });
        }
        else if (streak == 0 && completed.Any())
        {
            var lastSession = completed.MaxBy(s => s.EndTime);
            if (lastSession != null && (DateTime.Today - lastSession.EndTime!.Value.Date).TotalDays >= 1)
            {
                insights.Add(new Insight
                {
                    Title = "休息太久啦",
                    Description = "今天还没有开始专注，准备好了就出发吧。",
                    Type = InsightType.Streak
                });
            }
        }

        // 5. 任务提醒
        var last7Days = completed.Where(s => s.EndTime!.Value.Date >= DateTime.Today.AddDays(-7)).ToList();
        if (last7Days.Count > 0 && tasks.Count > 0)
        {
            var taskAvg = last7Days
                .GroupBy(s => string.IsNullOrEmpty(s.TaskName) ? "未分类" : s.TaskName)
                .Select(g => new { Name = g.Key, Avg = (double)g.Count() / 7 })
                .Where(x => x.Avg < 1.0)
                .OrderBy(x => x.Avg)
                .FirstOrDefault();

            if (taskAvg != null)
            {
                var totalForTask = completed.Count(s => (string.IsNullOrEmpty(s.TaskName) ? "未分类" : s.TaskName) == taskAvg.Name);
                if (totalForTask >= 5)
                {
                    insights.Add(new Insight
                    {
                        Title = "需要关注",
                        Description = $"「{taskAvg.Name}」最近 7 天平均每天只有 {taskAvg.Avg:F1} 个番茄钟，考虑增加投入。",
                        Type = InsightType.TaskCompletion
                    });
                }
            }
        }

        // 6. 里程碑
        var milestones = new[] { 10, 50, 100, 500, 1000 };
        var totalPomodoros = completed.Count;
        var latestMilestone = milestones.Where(m => totalPomodoros >= m).DefaultIfEmpty(0).Max();
        if (latestMilestone > 0 && insights.Count == 0)
        {
            insights.Add(new Insight
            {
                Title = "里程碑达成",
                Description = $"你已经完成了 {latestMilestone} 个番茄钟！这是一个了不起的成就。",
                Type = InsightType.Motivation
            });
        }

        // 兜底
        if (insights.Count == 0)
        {
            insights.Add(new Insight
            {
                Title = "稳步前行",
                Description = $"累计完成 {totalPomodoros} 个番茄钟，每一天都在进步。",
                Type = InsightType.Motivation
            });
        }

        return insights.Take(5).ToList();
    }

    private static int CalculateStreak(List<FocusSession> completed)
    {
        if (completed.Count == 0) return 0;

        var daysWithSessions = completed
            .Select(s => s.EndTime!.Value.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (daysWithSessions[0] != DateTime.Today && daysWithSessions[0] != DateTime.Today.AddDays(-1))
            return 0;

        int streak = 1;
        for (int i = 1; i < daysWithSessions.Count; i++)
        {
            if (daysWithSessions[i] == daysWithSessions[i - 1].AddDays(-1))
                streak++;
            else
                break;
        }

        return streak;
    }
}
