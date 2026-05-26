using System.ComponentModel;
using System.Runtime.CompilerServices;
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

public class StatsViewModel : INotifyPropertyChanged
{
    private readonly IStorageService _storageService;
    private readonly IInsightEngine _insightEngine;

    private int _completedPomodoros;
    private int _totalFocusMinutes;
    private double _avgQualityScore;
    private int _streakDays;
    private DateTime _currentDate = DateTime.Today;
    private StatsPeriod _currentPeriod = StatsPeriod.Day;
    private string _statsDateLabel = "今日统计";
    private bool _canGoNext;
    private string _periodSelection = "Day";

    private List<HeatmapDay> _heatmapDays = [];
    private List<HourlyDataPoint> _hourlyData = [];
    private List<TaskSlice> _taskBreakdown = [];
    private List<WeeklyDataPoint> _weeklyTrend = [];
    private List<Insight> _insights = [];
    private List<GoalProgress> _goalProgress = [];
    private List<ComparisonData> _comparisons = [];
    private List<EfficiencyDataPoint> _efficiencyTrend = [];
    private List<CategoryStats> _categoryBreakdown = [];
    private bool _hasZeroCategory;
    private string _zeroCategoryWarning = string.Empty;
    private int _maxCategoryMinutes;

    // 过滤条件
    private DateTime? _filterDateFrom;
    private DateTime? _filterDateTo;
    private string _filterKeyword = string.Empty;
    private TaskItem? _selectedFilterTask;
    private List<TaskItem> _availableTasks = [];
    private bool _isFilterVisible;
    private bool _hasActiveFilter;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int CompletedPomodoros
    {
        get => _completedPomodoros;
        set { if (_completedPomodoros != value) { _completedPomodoros = value; OnPropertyChanged(); } }
    }

    public int TotalFocusMinutes
    {
        get => _totalFocusMinutes;
        set { if (_totalFocusMinutes != value) { _totalFocusMinutes = value; OnPropertyChanged(); } }
    }

    public double AvgQualityScore
    {
        get => _avgQualityScore;
        set { if (_avgQualityScore != value) { _avgQualityScore = value; OnPropertyChanged(); } }
    }

    public int StreakDays
    {
        get => _streakDays;
        set { if (_streakDays != value) { _streakDays = value; OnPropertyChanged(); } }
    }

    public string StatsDateLabel
    {
        get => _statsDateLabel;
        set { if (_statsDateLabel != value) { _statsDateLabel = value; OnPropertyChanged(); } }
    }

    public bool CanGoNext
    {
        get => _canGoNext;
        set { if (_canGoNext != value) { _canGoNext = value; OnPropertyChanged(); } }
    }

    public string PeriodSelection
    {
        get => _periodSelection;
        set
        {
            if (_periodSelection != value)
            {
                _periodSelection = value;
                _currentPeriod = value switch
                {
                    "Week" => StatsPeriod.Week,
                    "Month" => StatsPeriod.Month,
                    _ => StatsPeriod.Day
                };
                _currentDate = DateTime.Today;
                OnPropertyChanged();
                LoadStatsForCurrentPeriod();
            }
        }
    }

    public List<HeatmapDay> HeatmapDays
    {
        get => _heatmapDays;
        set { if (_heatmapDays != value) { _heatmapDays = value; OnPropertyChanged(); } }
    }

    public List<HourlyDataPoint> HourlyData
    {
        get => _hourlyData;
        set { if (_hourlyData != value) { _hourlyData = value; OnPropertyChanged(); } }
    }

    public List<TaskSlice> TaskBreakdown
    {
        get => _taskBreakdown;
        set { if (_taskBreakdown != value) { _taskBreakdown = value; OnPropertyChanged(); } }
    }

    public List<WeeklyDataPoint> WeeklyTrend
    {
        get => _weeklyTrend;
        set { if (_weeklyTrend != value) { _weeklyTrend = value; OnPropertyChanged(); } }
    }

    public List<Insight> Insights
    {
        get => _insights;
        set { if (_insights != value) { _insights = value; OnPropertyChanged(); } }
    }

    public List<GoalProgress> GoalProgress
    {
        get => _goalProgress;
        set { if (_goalProgress != value) { _goalProgress = value; OnPropertyChanged(); } }
    }

    public List<ComparisonData> Comparisons
    {
        get => _comparisons;
        set { if (_comparisons != value) { _comparisons = value; OnPropertyChanged(); } }
    }

    public List<EfficiencyDataPoint> EfficiencyTrend
    {
        get => _efficiencyTrend;
        set { if (_efficiencyTrend != value) { _efficiencyTrend = value; OnPropertyChanged(); } }
    }

    public List<CategoryStats> CategoryBreakdown
    {
        get => _categoryBreakdown;
        set { if (_categoryBreakdown != value) { _categoryBreakdown = value; OnPropertyChanged(); } }
    }

    public bool HasZeroCategory
    {
        get => _hasZeroCategory;
        set { if (_hasZeroCategory != value) { _hasZeroCategory = value; OnPropertyChanged(); } }
    }

    public string ZeroCategoryWarning
    {
        get => _zeroCategoryWarning;
        set { if (_zeroCategoryWarning != value) { _zeroCategoryWarning = value; OnPropertyChanged(); } }
    }

    public int MaxCategoryMinutes
    {
        get => _maxCategoryMinutes;
        set { if (_maxCategoryMinutes != value) { _maxCategoryMinutes = value; OnPropertyChanged(); } }
    }

    // 过滤属性
    public DateTime? FilterDateFrom
    {
        get => _filterDateFrom;
        set { if (_filterDateFrom != value) { _filterDateFrom = value; OnPropertyChanged(); } }
    }

    public DateTime? FilterDateTo
    {
        get => _filterDateTo;
        set { if (_filterDateTo != value) { _filterDateTo = value; OnPropertyChanged(); } }
    }

    public string FilterKeyword
    {
        get => _filterKeyword;
        set { if (_filterKeyword != value) { _filterKeyword = value; OnPropertyChanged(); } }
    }

    public TaskItem? SelectedFilterTask
    {
        get => _selectedFilterTask;
        set { if (_selectedFilterTask != value) { _selectedFilterTask = value; OnPropertyChanged(); } }
    }

    public List<TaskItem> AvailableTasks
    {
        get => _availableTasks;
        set { if (_availableTasks != value) { _availableTasks = value; OnPropertyChanged(); } }
    }

    public bool IsFilterVisible
    {
        get => _isFilterVisible;
        set { if (_isFilterVisible != value) { _isFilterVisible = value; OnPropertyChanged(); } }
    }

    public bool HasActiveFilter
    {
        get => _hasActiveFilter;
        set { if (_hasActiveFilter != value) { _hasActiveFilter = value; OnPropertyChanged(); } }
    }

    public void ToggleFilter()
    {
        IsFilterVisible = !IsFilterVisible;
        if (IsFilterVisible) LoadAvailableTasks();
    }

    public void ApplyFilter()
    {
        HasActiveFilter = FilterDateFrom.HasValue || FilterDateTo.HasValue
            || !string.IsNullOrWhiteSpace(FilterKeyword) || SelectedFilterTask != null;
        LoadStatsForCurrentPeriod();
    }

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

    public StatsViewModel(IStorageService storageService, IInsightEngine insightEngine)
    {
        _storageService = storageService;
        _insightEngine = insightEngine;
    }

    public void Refresh()
    {
        LoadStatsForCurrentPeriod();
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
        // 只加载一次 sessions，分发给所有 InsightEngine 方法，避免重复 JSON 反序列化
        var sessions = _storageService.LoadSessions();
        var tasks = _storageService.LoadTasks();

        // 按当前周期过滤
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

        // 应用自定义日期范围（覆盖周期选择）
        if (_filterDateFrom.HasValue && _filterDateTo.HasValue)
        {
            periodStart = _filterDateFrom.Value.Date;
            periodEnd = _filterDateTo.Value.Date;
            StatsDateLabel = $"{periodStart:M月d日} - {periodEnd:M月d日}";
            CanGoNext = false;
        }

        // 构建为单次 .ToList() 的复合过滤，避免中间 List 分配
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

        // 预过滤已完成 sessions，避免各 InsightEngine 方法重复 .Where(s => s.Completed)
        var completedSessions = sessions.Where(s => s.Completed && s.EndTime.HasValue).ToList();
        StreakDays = InsightEngine.CalculateStreak(completedSessions);

        // 图表数据 — 传入已预过滤的 completedSessions
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
            EfficiencyTrend = _insightEngine.GetEfficiencyTrend(sessions); // 需要全量 sessions 计算完成率
        }
        else
        {
            GoalProgress = [];
            Comparisons = [];
            EfficiencyTrend = [];
        }

        // 科目均衡分析
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

        // 检查是否有科目 0 分钟（仅在周视图下检查）
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
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
