using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface IInsightEngine
{
    List<HeatmapDay> GetHeatmapData(List<FocusSession> sessions);
    List<HourlyDataPoint> GetHourlyDistribution(List<FocusSession> sessions, DateTime start, DateTime end);
    List<TaskSlice> GetTaskBreakdown(List<FocusSession> sessions, DateTime start, DateTime end, List<TaskItem>? tasks = null);
    List<WeeklyDataPoint> GetWeeklyTrend(List<FocusSession> sessions);
    List<Insight> GetInsights(List<FocusSession> sessions, List<TaskItem> tasks);
}
