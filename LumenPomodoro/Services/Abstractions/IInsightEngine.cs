using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface IInsightEngine
{
    List<HeatmapDay> GetHeatmapData(List<FocusSession> sessions);
    List<HourlyDataPoint> GetHourlyDistribution(List<FocusSession> sessions, DateTime start, DateTime end);
    List<TaskSlice> GetTaskBreakdown(List<FocusSession> sessions, DateTime start, DateTime end, List<TaskItem>? tasks = null);
    List<WeeklyDataPoint> GetWeeklyTrend(List<FocusSession> sessions);
    List<Insight> GetInsights(List<FocusSession> sessions, List<TaskItem> tasks);
    List<GoalProgress> GetGoalProgress(List<FocusSession> sessions, int dailyGoalMinutes, int weeklyGoalMinutes);
    List<ComparisonData> GetComparisons(List<FocusSession> sessions);
    List<EfficiencyDataPoint> GetEfficiencyTrend(List<FocusSession> sessions);
}
