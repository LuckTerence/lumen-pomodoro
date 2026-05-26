using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;

namespace LumenPomodoro.Services;

public class InsightEngine : IInsightEngine
{
    private static readonly string[] DayNames = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];

    private const int MinSessionsForInsight = 3;
    private const int StreakThreshold = 3;
    private const double TrendChangeThreshold = 0.15;
    private const int RecentWeeksForTrend = 4;
    private const int TaskAttentionDays = 7;
    private const double TaskAttentionAvgThreshold = 1.0;
    private const int TaskAttentionMinTotal = 5;
    private const int MaxInsightCount = 5;
    private static readonly int[] Milestones = [10, 50, 100, 500, 1000];

    private static DateTime GetMonday(DateTime date) => date.AddDays(-(int)date.DayOfWeek);

    /// <param name="sessions">预过滤为已完成且有 EndTime</param>
    public List<HeatmapDay> GetHeatmapData(List<FocusSession> sessions)
    {
        var today = DateTime.Today;
        var completedAll = sessions;

        var earliestDate = completedAll.Count > 0
            ? completedAll.Min(s => s.EndTime!.Value.Date)
            : today;

        var fullRange = (today - earliestDate).Days;
        var daysToShow = Math.Max(90, Math.Min(365, fullRange + 7));
        var startDate = today.AddDays(-(daysToShow - 1));

        var dailyMinutes = completedAll
            .Where(s => s.EndTime!.Value.Date >= startDate)
            .GroupBy(s => s.EndTime!.Value.Date)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.FocusMinutes));

        var maxMinutes = dailyMinutes.Values.DefaultIfEmpty(0).Max();

        var result = new List<HeatmapDay>(daysToShow);
        for (int i = 0; i < daysToShow; i++)
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

    /// <param name="sessions">预过滤为已完成且有 EndTime</param>
    public List<HourlyDataPoint> GetHourlyDistribution(List<FocusSession> sessions, DateTime start, DateTime end)
    {
        var hourlyGroups = sessions
            .Where(s => s.EndTime!.Value.Date >= start.Date && s.EndTime!.Value.Date <= end.Date)
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

    /// <param name="sessions">预过滤为已完成且有 EndTime</param>
    public List<TaskSlice> GetTaskBreakdown(List<FocusSession> sessions, DateTime start, DateTime end, List<TaskItem>? tasks = null)
    {
        var filtered = sessions
            .Where(s => s.EndTime!.Value.Date >= start.Date && s.EndTime!.Value.Date <= end.Date)
            .ToList();

        var total = filtered.Count;
        if (total == 0) return [];

        var colorMap = tasks?.Where(t => !string.IsNullOrEmpty(t.Name) && !string.IsNullOrEmpty(t.Color))
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

    /// <param name="sessions">预过滤为已完成且有 EndTime</param>
    public List<WeeklyDataPoint> GetWeeklyTrend(List<FocusSession> sessions)
    {
        var today = DateTime.Today;
        var thisMonday = GetMonday(today);
        if (thisMonday > today) thisMonday = thisMonday.AddDays(-7);

        // 一次性按周 GroupBy，避免 8 次 O(n) 全表扫描
        var weekGroups = sessions
            .GroupBy(s =>
            {
                var daysFromFirst = (s.EndTime!.Value.Date - thisMonday.AddDays(-7 * 7)).Days;
                return daysFromFirst >= 0 ? daysFromFirst / 7 : -1;
            })
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<WeeklyDataPoint>(8);
        for (int i = 7; i >= 0; i--)
        {
            var weekStart = thisMonday.AddDays(-7 * i);
            weekGroups.TryGetValue(i, out var weekSessions);
            weekSessions ??= [];

            result.Add(new WeeklyDataPoint
            {
                WeekStart = weekStart,
                TotalMinutes = weekSessions.Sum(s => s.FocusMinutes),
                CompletedPomodoros = weekSessions.Count
            });
        }

        return result;
    }

    /// <param name="sessions">预过滤为已完成且有 EndTime</param>
    public List<Insight> GetInsights(List<FocusSession> sessions, List<TaskItem> tasks)
    {
        var insights = new List<Insight>();
        var completed = sessions;

        if (completed.Count < MinSessionsForInsight)
        {
            insights.Add(new Insight
            {
                Title = "开始你的专注之旅",
                Description = "完成更多番茄钟后，这里会为你生成专属的学习洞察。",
                Type = InsightType.Motivation
            });
            return insights;
        }

        // === 单次 GroupBy 合并 hourGroups + hourQualityGroups ===
        var hourGroups = completed
            .GroupBy(s => s.EndTime!.Value.Hour)
            .Select(g => new
            {
                Hour = g.Key,
                Count = g.Count(),
                AvgMinutes = g.Average(s => s.FocusMinutes),
                AvgQuality = g.Average(s => s.QualityScore)
            })
            .Where(x => x.Count >= MinSessionsForInsight)
            .ToList();

        var dayGroups = completed
            .GroupBy(s => s.EndTime!.Value.DayOfWeek)
            .Select(g => new { Day = g.Key, Total = g.Count() })
            .Where(x => x.Total >= MinSessionsForInsight)
            .ToList();

        var dateGroups = completed
            .GroupBy(s => s.EndTime!.Value.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var totalPomodoros = completed.Count;

        // 1. 峰值时段
        var bestHour = hourGroups.OrderByDescending(x => x.AvgMinutes).FirstOrDefault();
        if (bestHour != null)
        {
            insights.Add(new Insight
            {
                Title = "你的黄金时段",
                Description = $"{bestHour.Hour}:00 左右是你效率最高的时段，平均每次专注 {(int)bestHour.AvgMinutes} 分钟。",
                Type = InsightType.PeakHour,
                ActionHint = "试试把最重要科目安排在这个时段"
            });
        }

        // 2. 最佳学习时段（基于质量分，复用 hourGroups）
        if (hourGroups.Count > 0 && insights.Count < MaxInsightCount)
        {
            var maxMinutes = hourGroups.Max(x => x.AvgMinutes);
            if (maxMinutes <= 0) maxMinutes = 1;
            var bestTimeSlot = hourGroups
                .OrderByDescending(x => x.AvgQuality * 0.6 + (x.AvgMinutes / maxMinutes) * 0.4)
                .First();

            var timeRange = $"{bestTimeSlot.Hour}:00-{bestTimeSlot.Hour + 2}:00";
            insights.Add(new Insight
            {
                Title = "最佳学习时段",
                Description = $"你{timeRange}效率最高，建议安排重要科目。",
                Type = InsightType.PeakHour,
                ActionHint = "今天就把专业课放到这个时段"
            });
        }

        // 3. 最佳日期
        var bestDay = dayGroups.OrderByDescending(x => x.Total).FirstOrDefault();
        if (bestDay != null)
        {
            insights.Add(new Insight
            {
                Title = "高效日",
                Description = $"{DayNames[(int)bestDay.Day]}是你最高效的一天，累计完成 {bestDay.Total} 个番茄钟。",
                Type = InsightType.BestDay
            });
        }

        // 4. 趋势检测
        var fourWeeksAgo = DateTime.Today.AddDays(-RecentWeeksForTrend * 7);
        var eightWeeksAgo = DateTime.Today.AddDays(-RecentWeeksForTrend * 2 * 7);
        var recent4Weeks = dateGroups
            .Where(kvp => kvp.Key >= fourWeeksAgo)
            .Sum(kvp => kvp.Value.Count);
        var prior4Weeks = dateGroups
            .Where(kvp => kvp.Key >= eightWeeksAgo && kvp.Key < fourWeeksAgo)
            .Sum(kvp => kvp.Value.Count);

        if (prior4Weeks > 0)
        {
            var change = (double)(recent4Weeks - prior4Weeks) / prior4Weeks;
            if (change > TrendChangeThreshold)
            {
                insights.Add(new Insight
                {
                    Title = "上升趋势",
                    Description = $"最近 4 周比之前多完成了 {(int)(change * 100)}% 的番茄钟，继续保持！",
                    Type = InsightType.Trend
                });
            }
            else if (change < -TrendChangeThreshold)
            {
                insights.Add(new Insight
                {
                    Title = "节奏调整",
                    Description = "最近专注时长有所下降，试试缩短单次时间或调整学习科目。",
                    Type = InsightType.Trend,
                    ActionHint = "从25分钟开始重新找回节奏"
                });
            }
        }

        // 5. 连续天数
        var streak = CalculateStreak(completed);
        if (streak >= StreakThreshold)
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
                    Type = InsightType.Streak,
                    ActionHint = "完成一个番茄钟就能重新点燃 streak"
                });
            }
        }

        // 6. 任务提醒
        var cutoff = DateTime.Today.AddDays(-TaskAttentionDays);
        var last7Days = dateGroups
            .Where(kvp => kvp.Key >= cutoff)
            .SelectMany(kvp => kvp.Value)
            .ToList();
        if (last7Days.Count > 0 && tasks.Count > 0)
        {
            var taskAvg = last7Days
                .GroupBy(s => string.IsNullOrEmpty(s.TaskName) ? "未分类" : s.TaskName)
                .Select(g => new { Name = g.Key, Avg = (double)g.Count() / TaskAttentionDays })
                .Where(x => x.Avg < TaskAttentionAvgThreshold)
                .OrderBy(x => x.Avg)
                .FirstOrDefault();

            if (taskAvg != null)
            {
                var taskName = taskAvg.Name;
                var totalForTask = completed.Count(s => (string.IsNullOrEmpty(s.TaskName) ? "未分类" : s.TaskName) == taskName);
                if (totalForTask >= TaskAttentionMinTotal)
                {
                    insights.Add(new Insight
                    {
                        Title = "需要关注",
                        Description = $"「{taskAvg.Name}」最近 7 天平均每天只有 {taskAvg.Avg:F1} 个番茄钟，考虑增加投入。",
                        Type = InsightType.TaskCompletion,
                        ActionHint = "今天就先从这个科目开始吧"
                    });
                }
            }
        }

        // 7. 里程碑
        var latestMilestone = Milestones.Where(m => totalPomodoros >= m).DefaultIfEmpty(0).Max();
        if (latestMilestone > 0 && insights.Count == 0)
        {
            insights.Add(new Insight
            {
                Title = "里程碑达成",
                Description = $"你已经完成了 {latestMilestone} 个番茄钟！这是一个了不起的成就。",
                Type = InsightType.Motivation
            });
        }

        if (insights.Count == 0)
        {
            insights.Add(new Insight
            {
                Title = "稳步前行",
                Description = $"累计完成 {totalPomodoros} 个番茄钟，每一天都在进步。",
                Type = InsightType.Motivation
            });
        }

        return insights.Take(MaxInsightCount).ToList();
    }

    public static int CalculateStreak(List<FocusSession> completed)
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

    /// <param name="sessions">预过滤为已完成且有 EndTime</param>
    public List<GoalProgress> GetGoalProgress(List<FocusSession> sessions, int dailyGoalMinutes, int weeklyGoalMinutes)
    {
        var result = new List<GoalProgress>();
        var today = DateTime.Today;
        var completed = sessions;

        var thisMonday = GetMonday(today);
        if (thisMonday > today) thisMonday = thisMonday.AddDays(-7);

        // 单次遍历同时计算日 + 周
        int todayMinutes = 0, weekMinutes = 0;
        foreach (var s in completed)
        {
            var date = s.EndTime!.Value.Date;
            if (date >= thisMonday.Date && date <= today) weekMinutes += s.FocusMinutes;
            if (date == today) todayMinutes += s.FocusMinutes;
        }

        if (dailyGoalMinutes > 0)
        {
            result.Add(new GoalProgress
            {
                Label = "每日目标",
                CurrentMinutes = todayMinutes,
                TargetMinutes = dailyGoalMinutes,
                ProgressPercent = Math.Min(100, (double)todayMinutes / dailyGoalMinutes * 100),
                IsCompleted = todayMinutes >= dailyGoalMinutes
            });
        }

        if (weeklyGoalMinutes > 0)
        {
            result.Add(new GoalProgress
            {
                Label = "每周目标",
                CurrentMinutes = weekMinutes,
                TargetMinutes = weeklyGoalMinutes,
                ProgressPercent = Math.Min(100, (double)weekMinutes / weeklyGoalMinutes * 100),
                IsCompleted = weekMinutes >= weeklyGoalMinutes
            });
        }

        return result;
    }

    /// <param name="sessions">预过滤为已完成且有 EndTime</param>
    public List<ComparisonData> GetComparisons(List<FocusSession> sessions)
    {
        var result = new List<ComparisonData>();
        var today = DateTime.Today;
        var completed = sessions;

        var thisMonday = GetMonday(today);
        if (thisMonday > today) thisMonday = thisMonday.AddDays(-7);
        var lastMonday = thisMonday.AddDays(-7);
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // 单次遍历计算 4 个指标
        int thisWeek = 0, lastWeek = 0, thisMonth = 0, lastMonth = 0;
        foreach (var s in completed)
        {
            var date = s.EndTime!.Value.Date;
            if (date >= thisMonday.Date && date <= today) thisWeek += s.FocusMinutes;
            else if (date >= lastMonday.Date && date < thisMonday.Date) lastWeek += s.FocusMinutes;

            if (date >= thisMonthStart.Date && date <= today) thisMonth += s.FocusMinutes;
            else if (date >= lastMonthStart.Date && date < thisMonthStart.Date) lastMonth += s.FocusMinutes;
        }

        result.Add(BuildComparison("本周 vs 上周", thisWeek, lastWeek));
        result.Add(BuildComparison("本月 vs 上月", thisMonth, lastMonth));

        return result;
    }

    private static ComparisonData BuildComparison(string label, int current, int previous)
    {
        double changePercent = previous > 0 ? (double)(current - previous) / previous * 100 : 0;
        return new ComparisonData
        {
            Label = label,
            CurrentValue = current,
            PreviousValue = previous,
            ChangePercent = Math.Round(changePercent, 1),
            IsPositive = changePercent >= 0
        };
    }

    /// <param name="sessions">全量 sessions（含未完成），用于计算完成率</param>
    public List<EfficiencyDataPoint> GetEfficiencyTrend(List<FocusSession> sessions)
    {
        var today = DateTime.Today;
        var thisMonday = GetMonday(today);
        if (thisMonday > today) thisMonday = thisMonday.AddDays(-7);

        // 按周预分组，避免 8 次 O(n) 扫描
        var weekGroups = sessions
            .GroupBy(s =>
            {
                var daysFromFirst = (s.StartTime.Date - thisMonday.AddDays(-7 * 7)).Days;
                return daysFromFirst >= 0 ? daysFromFirst / 7 : -1;
            })
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<EfficiencyDataPoint>(8);
        for (int i = 7; i >= 0; i--)
        {
            var weekStart = thisMonday.AddDays(-7 * i);
            weekGroups.TryGetValue(i, out var weekSessions);
            weekSessions ??= [];

            var completed = weekSessions.Where(s => s.Completed).ToList();

            result.Add(new EfficiencyDataPoint
            {
                WeekStart = weekStart,
                CompletionRate = weekSessions.Count > 0 ? Math.Round((double)completed.Count / weekSessions.Count, 2) : 0,
                AvgFocusMinutes = completed.Count > 0 ? Math.Round(completed.Average(s => s.FocusMinutes), 1) : 0,
                AvgQualityScore = completed.Count > 0 ? Math.Round(completed.Average(s => s.QualityScore), 1) : 0
            });
        }
        return result;
    }
}
