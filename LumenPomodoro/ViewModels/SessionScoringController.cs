using System;
using System.Collections.Generic;
using System.Linq;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;

namespace LumenPomodoro.ViewModels;

/// <summary>
/// 质量评分、笔记保存、缺席检测（streak）、里程碑、昨日日报生成。
/// 纯计算服务，MainViewModel 调用方法并自行更新绑定属性。
/// </summary>
public class SessionScoringController
{
    public static bool ShouldSuggestLongBreak(int todayCompletedPomodoros, Settings settings)
    {
        return todayCompletedPomodoros > 0
            && settings.LongBreakInterval > 0
            && todayCompletedPomodoros % settings.LongBreakInterval == 0;
    }

    public static void SaveNotes(IStorageService storage, string? lastSessionId, string notes)
    {
        if (lastSessionId == null || string.IsNullOrWhiteSpace(notes)) return;
        storage.UpdateSession(lastSessionId, session => session.Notes = notes.Trim());
    }

    public static void SaveRating(IStorageService storage, string? lastSessionId, int stars)
    {
        if (lastSessionId == null) return;
        storage.UpdateSession(lastSessionId, session => session.QualityScore = stars);
    }

    public static string GetRatingStars(int rating)
    {
        return rating > 0
            ? new string('\u2605', rating) + new string('\u2606', 5 - rating)
            : string.Empty;
    }

    public static string GetCompletedSummary(string taskName, int focusMinutes)
    {
        return string.IsNullOrEmpty(taskName)
            ? string.Empty
            : $"刚刚完成：{taskName} · {focusMinutes} 分钟";
    }

    public static void CheckMilestones(DailyStats todayStats, Settings settings,
        Action<string, string> showInApp)
    {
        if (todayStats.CompletedPomodoros == 1)
            showInApp(Properties.LocalizedStrings.Milestone_First, Properties.LocalizedStrings.Milestone_First);

        if (todayStats.TotalFocusMinutes >= settings.DailyGoalMinutes && settings.DailyGoalMinutes > 0)
            showInApp(Properties.LocalizedStrings.Milestone_Daily, Properties.LocalizedStrings.Milestone_Daily);

        if (settings.DailyTargetPomodoros > 0 && todayStats.CompletedPomodoros >= settings.DailyTargetPomodoros)
            showInApp(Properties.LocalizedStrings.Milestone_Target, Properties.LocalizedStrings.Milestone_Target);
    }

    public static int CalculateStreak(IEnumerable<FocusSession> completedSessions)
    {
        return InsightEngine.CalculateStreak(completedSessions.ToList());
    }

    public static bool ShouldShowStreakEncouragement(List<FocusSession> completedSessions)
    {
        if (completedSessions.Count == 0) return false;
        var lastSession = completedSessions.MaxBy(s => s.EndTime);
        return lastSession != null && (DateTime.Today - lastSession.EndTime!.Value.Date).TotalDays >= 1;
    }

    public static DailyReport? GetYesterdayReport(IStorageService storage)
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var allCompleted = new List<FocusSession>();
        var sessions = new List<FocusSession>();

        foreach (var s in storage.LoadSessions().Where(s => s.Completed && s.EndTime.HasValue))
        {
            allCompleted.Add(s);
            if (s.EndTime!.Value.Date == yesterday)
                sessions.Add(s);
        }

        if (sessions.Count == 0) return null;

        var mainTask = sessions.GroupBy(s => s.TaskName)
            .OrderByDescending(g => g.Sum(s => s.FocusMinutes))
            .FirstOrDefault()?.Key ?? "未分类";

        var uniqueTasks = sessions.Select(s => s.TaskName).Distinct().Count();
        var avgQuality = sessions
            .Where(s => s.QualityScore > 0)
            .Select(s => (double)s.QualityScore)
            .DefaultIfEmpty(0)
            .Average();

        var categorySuggestion = "";
        var allTasks = storage.LoadTasks();
        var yesterdayCategories = sessions
            .Join(allTasks, s => s.TaskId, t => t.Id,
                (s, t) => string.IsNullOrEmpty(t.Category) ? t.Name : t.Category)
            .Distinct()
            .ToHashSet();
        var allCategories = allTasks
            .Select(t => string.IsNullOrEmpty(t.Category) ? t.Name : t.Category)
            .Distinct()
            .ToList();
        var missed = allCategories.Where(c => !yesterdayCategories.Contains(c)).ToList();
        if (missed.Count > 0 && missed.Count <= 3)
            categorySuggestion = $"昨天没有学习「{string.Join("」「", missed)}」，今天可以补上进度";

        return new DailyReport
        {
            Date = yesterday,
            CompletedPomodoros = sessions.Count,
            TotalMinutes = sessions.Sum(s => s.FocusMinutes),
            MainTask = mainTask,
            StreakDays = InsightEngine.CalculateStreak(allCompleted),
            AvgQualityScore = Math.Round(avgQuality, 1),
            UniqueTasksCount = uniqueTasks,
            CategorySuggestion = categorySuggestion
        };
    }
}
