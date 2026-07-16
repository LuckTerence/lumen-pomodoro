using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;

namespace LumenPomodoro.ViewModels;

public enum StatsPeriod
{
    Day,
    Week,
    Month
}

public partial class StatsViewModel : ObservableObject
{
    private readonly IStorageService _storageService;
    private readonly IInsightEngine _insightEngine;
    private DateTime _currentDate = DateTime.Today;
    private StatsPeriod _currentPeriod = StatsPeriod.Day;

    [ObservableProperty]
    private int _completedPomodoros;

    [ObservableProperty]
    private int _totalFocusMinutes;

    [ObservableProperty]
    private double _avgQualityScore;

    [ObservableProperty]
    private int _streakDays;

    [ObservableProperty]
    private string _statsDateLabel = "今日统计";

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private string _periodSelection = "Day";

    [ObservableProperty]
    private List<HeatmapDay> _heatmapDays = [];

    [ObservableProperty]
    private List<HourlyDataPoint> _hourlyData = [];

    [ObservableProperty]
    private List<TaskSlice> _taskBreakdown = [];

    [ObservableProperty]
    private List<WeeklyDataPoint> _weeklyTrend = [];

    [ObservableProperty]
    private List<Insight> _insights = [];

    [ObservableProperty]
    private List<GoalProgress> _goalProgress = [];

    [ObservableProperty]
    private List<ComparisonData> _comparisons = [];

    [ObservableProperty]
    private List<EfficiencyDataPoint> _efficiencyTrend = [];

    [ObservableProperty]
    private List<CategoryStats> _categoryBreakdown = [];

    [ObservableProperty]
    private bool _hasZeroCategory;

    [ObservableProperty]
    private string _zeroCategoryWarning = string.Empty;

    [ObservableProperty]
    private int _maxCategoryMinutes;

    [ObservableProperty]
    private List<AchievementItem> _achievements = [];

    public bool HasAchievements => Achievements.Count > 0;

    // 过滤条件
    [ObservableProperty]
    private DateTime? _filterDateFrom;

    [ObservableProperty]
    private DateTime? _filterDateTo;

    [ObservableProperty]
    private string _filterKeyword = string.Empty;

    [ObservableProperty]
    private TaskItem? _selectedFilterTask;

    [ObservableProperty]
    private List<TaskItem> _availableTasks = [];

    [ObservableProperty]
    private bool _isFilterVisible;

    [ObservableProperty]
    private bool _hasActiveFilter;

    public StatsViewModel(IStorageService storageService, IInsightEngine insightEngine)
    {
        _storageService = storageService;
        _insightEngine = insightEngine;
    }

    public void Refresh()
    {
        LoadStatsForCurrentPeriod();
    }

    partial void OnPeriodSelectionChanged(string value)
    {
        _currentPeriod = value switch
        {
            "Week" => StatsPeriod.Week,
            "Month" => StatsPeriod.Month,
            _ => StatsPeriod.Day
        };
        _currentDate = DateTime.Today;
        LoadStatsForCurrentPeriod();
    }

    partial void OnAchievementsChanged(List<AchievementItem> value)
    {
        // 触发 HasAchievements 的变更通知
        OnPropertyChanged(nameof(HasAchievements));
    }

    [RelayCommand]
    public void ToggleFilter()
    {
        IsFilterVisible = !IsFilterVisible;
        if (IsFilterVisible) LoadAvailableTasks();
    }

    [RelayCommand]
    public void ApplyFilter()
    {
        HasActiveFilter = FilterDateFrom.HasValue || FilterDateTo.HasValue
            || !string.IsNullOrWhiteSpace(FilterKeyword) || SelectedFilterTask != null;
        LoadStatsForCurrentPeriod();
    }

    [RelayCommand]
    public void ResetFilter()
    {
        FilterDateFrom = null;
        FilterDateTo = null;
        FilterKeyword = string.Empty;
        SelectedFilterTask = null;
        HasActiveFilter = false;
        IsFilterVisible = false;
        LoadStatsForCurrentPeriod();
    }

    private void LoadAvailableTasks()
    {
        AvailableTasks = _storageService.LoadTasks();
    }

    public void ShiftDate(int direction)
    {
        switch (_currentPeriod)
        {
            case StatsPeriod.Day:
                var newDay = _currentDate.AddDays(direction);
                if (newDay > DateTime.Today) return;
                _currentDate = newDay;
                break;
            case StatsPeriod.Week:
                var newWeek = _currentDate.AddDays(direction * 7);
                var maxWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                if (newWeek > maxWeek) return;
                _currentDate = newWeek;
                break;
            case StatsPeriod.Month:
                var newMonth = _currentDate.AddMonths(direction);
                var maxMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                if (newMonth > maxMonth) return;
                _currentDate = newMonth;
                break;
        }
        LoadStatsForCurrentPeriod();
    }

    public List<FocusSession> GetAllSessions() => _storageService.LoadSessions();

    private void LoadStatsForCurrentPeriod()
    {
        var sessions = _storageService.LoadSessions();
        var tasks = _storageService.LoadTasks();

        DateTime periodStart, periodEnd;
        List<FocusSession> filteredSessions;
        switch (_currentPeriod)
        {
            case StatsPeriod.Day:
                StatsDateLabel = _currentDate.Date == DateTime.Today
                    ? "今日统计"
                    : _currentDate.ToString("M月d日");
                periodStart = _currentDate.Date;
                periodEnd = _currentDate.Date;
                CanGoNext = _currentDate.Date < DateTime.Today;
                break;

            case StatsPeriod.Week:
                var weekStart = _currentDate.AddDays(-(int)_currentDate.DayOfWeek);
                var weekEnd = weekStart.AddDays(6);
                StatsDateLabel = $"{weekStart:M月d日}-{weekEnd:M月d日}";
                periodStart = weekStart;
                periodEnd = weekEnd;
                CanGoNext = _currentDate < DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                break;

            case StatsPeriod.Month:
                var monthStart = new DateTime(_currentDate.Year, _currentDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                StatsDateLabel = $"{_currentDate.Year}年{_currentDate.Month}月";
                periodStart = monthStart;
                periodEnd = monthEnd;
                CanGoNext = _currentDate < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                break;

            default:
                periodStart = DateTime.Today;
                periodEnd = DateTime.Today;
                break;
        }

        if (FilterDateFrom.HasValue && FilterDateTo.HasValue)
        {
            periodStart = FilterDateFrom.Value.Date;
            periodEnd = FilterDateTo.Value.Date;
            StatsDateLabel = $"{periodStart:M月d日} - {periodEnd:M月d日}";
            CanGoNext = false;
        }

        var taskFilterId = SelectedFilterTask?.Id;
        var keyword = string.IsNullOrWhiteSpace(FilterKeyword) ? null : FilterKeyword.Trim();
        var fromDate = periodStart;
        var toDate = periodEnd;

        filteredSessions = sessions
            .Where(s => s.Completed && s.EndTime.HasValue
                && s.EndTime.Value.Date >= fromDate
                && s.EndTime.Value.Date <= toDate
                && (taskFilterId == null || s.TaskId == taskFilterId)
                && (keyword == null
                    || (s.TaskName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true)
                    || (s.Notes?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true)))
            .ToList();

        CompletedPomodoros = filteredSessions.Count;
        TotalFocusMinutes = filteredSessions.Sum(s => s.FocusMinutes);
        var scoredSessions = filteredSessions.Where(s => s.QualityScore > 0).ToList();
        AvgQualityScore = scoredSessions.Count > 0
            ? scoredSessions.Average(s => s.QualityScore)
            : 0;

        var completedSessions = sessions.Where(s => s.Completed && s.EndTime.HasValue).ToList();
        StreakDays = InsightEngine.CalculateStreak(completedSessions);

        HeatmapDays = _insightEngine.GetHeatmapData(completedSessions);
        HourlyData = _insightEngine.GetHourlyDistribution(completedSessions, periodStart, periodEnd);
        TaskBreakdown = _insightEngine.GetTaskBreakdown(completedSessions, periodStart, periodEnd, tasks);
        WeeklyTrend = _insightEngine.GetWeeklyTrend(completedSessions);
        Insights = _insightEngine.GetInsights(completedSessions, tasks);

        var settings = _storageService.LoadSettings();

        if (settings.InsightsEnabled)
        {
            GoalProgress = _insightEngine.GetGoalProgress(completedSessions, settings.DailyGoalMinutes, settings.WeeklyGoalMinutes);
            Comparisons = _insightEngine.GetComparisons(completedSessions);
            EfficiencyTrend = _insightEngine.GetEfficiencyTrend(sessions);
        }
        else
        {
            GoalProgress = [];
            Comparisons = [];
            EfficiencyTrend = [];
        }

        var taskDict = tasks.ToDictionary(t => t.Id, t => t);
        var categoryGroups = filteredSessions
            .Where(s => taskDict.ContainsKey(s.TaskId))
            .GroupBy(s => taskDict[s.TaskId])
            .Select(g => new CategoryStats
            {
                Category = string.IsNullOrEmpty(g.Key.Category) ? g.Key.Name : g.Key.Category,
                TotalMinutes = g.Sum(s => s.FocusMinutes),
                PomodoroCount = g.Count(),
                Color = g.Key.Color
            })
            .OrderByDescending(c => c.TotalMinutes)
            .ToList();

        CategoryBreakdown = categoryGroups;
        MaxCategoryMinutes = categoryGroups.Any() ? categoryGroups.Max(c => c.TotalMinutes) : 0;

        if (_currentPeriod == StatsPeriod.Week && categoryGroups.Any())
        {
            var zeroCategories = categoryGroups.Where(c => c.TotalMinutes == 0).ToList();
            HasZeroCategory = zeroCategories.Any();
            ZeroCategoryWarning = HasZeroCategory
                ? $"{string.Join("、", zeroCategories.Select(c => c.Category))} 本周尚未学习"
                : string.Empty;
        }
        else
        {
            HasZeroCategory = false;
            ZeroCategoryWarning = string.Empty;
        }

        UpdateAchievements(filteredSessions);
    }

    private void UpdateAchievements(List<FocusSession> sessions)
    {
        var achievements = new List<AchievementItem>();
        var completed = sessions.Where(s => s.Completed).ToList();
        var totalPomodoros = completed.Count;
        var totalMinutes = completed.Sum(s => s.FocusMinutes);

        if (StreakDays >= 7)
            achievements.Add(new AchievementItem { Icon = "🔥", Title = $"{StreakDays} 天连击", Subtitle = "连续专注" });
        if (StreakDays >= 30)
            achievements.Add(new AchievementItem { Icon = "💎", Title = "钻石连击", Subtitle = "30 天连续专注" });
        if (StreakDays >= 100)
            achievements.Add(new AchievementItem { Icon = "👑", Title = "王者连击", Subtitle = "100 天连续专注" });
        if (totalPomodoros >= 10)
            achievements.Add(new AchievementItem { Icon = "🍅", Title = $"{totalPomodoros} 个番茄", Subtitle = "累计番茄数" });
        if (totalPomodoros >= 100)
            achievements.Add(new AchievementItem { Icon = "⭐", Title = "百番达人", Subtitle = "100+ 个番茄" });
        if (totalMinutes >= 1000)
            achievements.Add(new AchievementItem { Icon = "⏱", Title = $"{totalMinutes / 60} 小时", Subtitle = "累计专注时长" });

        Achievements = achievements;
    }
}
