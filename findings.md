# 详细实施方案

## Feature 4: 专注完成后的笔记

### 数据模型
```csharp
// FocusSession.cs 新增
public string? Notes { get; set; }
```

### UI 改动
**TimerPage.xaml CompletedPanel:**
```xml
<!-- 在"开始休息"按钮上方添加 -->
<TextBox x:Name="NotesBox" 
         PlaceholderText="记录这轮做了什么（可选）"
         MaxWidth="280" Margin="0,0,0,12"
         FontSize="13" Padding="12,8"
         AcceptsReturn="False" MaxLength="200" />
```

### 业务逻辑
**MainViewModel.cs:**
```csharp
// TimerService_TimerCompleted 中
_currentSession.Notes = _currentNotes; // 从 UI 绑定获取

// 新增属性
private string _currentNotes = string.Empty;
public string CurrentNotes
{
    get => _currentNotes;
    set { _currentNotes = value; OnPropertyChanged(); }
}
```

### 统计展示
**StatsPage.xaml:**
- 在"任务分布"卡片下方新增"最近记录"卡片
- 显示最近 10 条有笔记的 Session
- 格式：日期 + 任务名 + 笔记内容

### 涉及文件
| 文件 | 改动 |
|------|------|
| Models/FocusSession.cs | 新增 Notes 属性 |
| Views/Pages/TimerPage.xaml | CompletedPanel 添加 TextBox |
| Views/Pages/TimerPage.xaml.cs | 绑定 Notes 输入 |
| ViewModels/MainViewModel.cs | 保存 Notes 到 Session |
| Views/Pages/StatsPage.xaml | 新增"最近记录"卡片 |
| ViewModels/StatsViewModel.cs | 暴露 RecentNotes 属性 |

---

## Feature 5: 专注质量评分

### 评分规则
| 指标 | 权重 | 说明 |
|------|------|------|
| 完整完成 | +1 | 未提前重置 |
| 无暂停 | +1 | 未触发 Pause |
| 无走神 | +1 | 未触发 PresenceLost |

**评分：** 3 星 = 完美，2 星 = 良好，1 星 = 需改进

### 数据模型
```csharp
// FocusSession.cs 新增
public int QualityScore { get; set; } // 1-3
```

### 业务逻辑
**MainViewModel.cs:**
```csharp
// 新增跟踪字段
private bool _sessionPaused = false;
private bool _sessionPresenceLost = false;

// PauseFocus() 中
_sessionPaused = true;

// OnPresenceLost() 中
_sessionPresenceLost = true;

// TimerService_TimerCompleted 中
int score = 1; // 基础分：完整完成
if (!_sessionPaused) score++;
if (!_sessionPresenceLost) score++;
_currentSession.QualityScore = score;

// StartFocus() 中重置
_sessionPaused = false;
_sessionPresenceLost = false;
```

### UI 展示
**TimerPage.xaml:**
```xml
<!-- CompletedPanel 中，笔记框下方 -->
<TextBlock FontSize="16" HorizontalAlignment="Center" Margin="0,0,0,8">
    <Run Text="{Binding QualityStars}" />
</TextBlock>
```

**StatsPage.xaml:**
```xml
<!-- 概览卡片中新增一行 -->
<TextBlock Text="{Binding AvgQualityScore, StringFormat='{}平均质量 {0:F1} 星'}"
           FontSize="13" Foreground="{DynamicResource TextFillColorSecondaryBrush}" />
```

### 涉及文件
| 文件 | 改动 |
|------|------|
| Models/FocusSession.cs | 新增 QualityScore 属性 |
| ViewModels/MainViewModel.cs | 跟踪暂停/走神状态，计算评分 |
| Views/Pages/TimerPage.xaml | 显示星级 |
| ViewModels/StatsViewModel.cs | 计算平均质量分 |
| Views/Pages/StatsPage.xaml | 显示平均质量分 |

---

## Feature 6: Streak 强化

### 数据来源
InsightEngine.CalculateStreak() 已实现，返回连续天数。

### UI 改动
**TimerPage.xaml:**
```xml
<!-- 底部统计摘要下方 -->
<TextBlock FontSize="11" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
           HorizontalAlignment="Center" Margin="0,4,0,0"
           Visibility="{Binding StreakDays, Converter={StaticResource ZeroToCollapsedConverter}}">
    <Run Text="已连续专注 " />
    <Run Text="{Binding StreakDays}" Foreground="{DynamicResource TextFillColorSecondaryBrush}" FontWeight="SemiBold" />
    <Run Text=" 天" />
</TextBlock>

<!-- 断 streak 时的鼓励文案 -->
<TextBlock FontSize="11" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
           HorizontalAlignment="Center" Margin="0,4,0,0"
           Text="没关系，今天重新开始"
           Visibility="{Binding ShowStreakEncouragement, Converter={StaticResource BooleanToVisibilityConverter}}" />
```

### 业务逻辑
**MainViewModel.cs:**
```csharp
// 新增属性
private int _streakDays;
public int StreakDays
{
    get => _streakDays;
    set { _streakDays = value; OnPropertyChanged(); }
}

private bool _showStreakEncouragement;
public bool ShowStreakEncouragement
{
    get => _showStreakEncouragement;
    set { _showStreakEncouragement = value; OnPropertyChanged(); }
}

// LoadData() 或 RefreshStats() 中
var completed = _storageService.LoadSessions()
    .Where(s => s.Completed && s.EndTime.HasValue).ToList();
StreakDays = InsightEngine.CalculateStreak(completed);

// 如果今天还没有 session 且 streak > 0，显示鼓励
if (StreakDays == 0 && completed.Any())
{
    var lastSession = completed.MaxBy(s => s.EndTime);
    if (lastSession != null && (DateTime.Today - lastSession.EndTime!.Value.Date).TotalDays >= 1)
        ShowStreakEncouragement = true;
}
```

### 涉及文件
| 文件 | 改动 |
|------|------|
| ViewModels/MainViewModel.cs | 暴露 StreakDays 属性 |
| Views/Pages/TimerPage.xaml | 显示连续天数和鼓励文案 |
| Converters/ | 可能需要 ZeroToCollapsedConverter |

---

## Feature 7: 快速任务切换

### UI 改动
**TimerPage.xaml:**
```xml
<!-- IdlePanel 中，"开始专注"按钮上方 -->
<ComboBox x:Name="QuickTaskCombo"
          ItemsSource="{Binding Tasks}"
          SelectedItem="{Binding SelectedTask}"
          DisplayMemberPath="Name"
          MinWidth="200" Margin="0,0,0,12"
          FontSize="13" />

<!-- CompletedPanel 中，笔记框下方也添加 -->
<ComboBox ItemsSource="{Binding Tasks}"
          SelectedItem="{Binding SelectedTask}"
          DisplayMemberPath="Name"
          MinWidth="200" Margin="0,0,0,12"
          FontSize="13" />
```

### 交互逻辑
- 选择任务后自动更新 SelectedTask
- 任务芯片保留，点击仍可跳转 TasksPage
- 下拉框显示任务颜色圆点 + 名称

### 涉及文件
| 文件 | 改动 |
|------|------|
| Views/Pages/TimerPage.xaml | 添加 ComboBox |
| Views/Pages/TimerPage.xaml.cs | 可能需要处理选择事件 |

---

## Feature 8: 键盘快捷键增强

### 快捷键映射
| 按键 | 功能 |
|------|------|
| Space | 开始/暂停/继续（已有） |
| Esc | 重置（已有） |
| 1-9 | 选择第 N 个任务 |
| Tab | 切换到下一个页面 |
| Shift+Tab | 切换到上一个页面 |

### 实现
**TimerPage.xaml.cs:**
```csharp
protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key >= Key.D1 && e.Key <= Key.D9)
    {
        int index = e.Key - Key.D1;
        if (index < _viewModel.Tasks.Count)
            _viewModel.SelectedTask = _viewModel.Tasks[index];
        e.Handled = true;
        return;
    }
    base.OnKeyDown(e);
}
```

**MainWindow.xaml.cs:**
```csharp
// Tab 切换页面
protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.Tab)
    {
        int currentIndex = NavView.MenuItems.IndexOf(NavView.SelectedItem);
        int nextIndex = e.KeyboardDevice.Modifiers == ModifierKeys.Shift
            ? (currentIndex - 1 + NavView.MenuItems.Count) % NavView.MenuItems.Count
            : (currentIndex + 1) % NavView.MenuItems.Count;
        NavView.SelectedItem = NavView.MenuItems[nextIndex];
        e.Handled = true;
        return;
    }
    base.OnKeyDown(e);
}
```

### UI 更新
**TimerPage.xaml:**
```xml
<!-- 更新快捷键提示 -->
<TextBlock FontSize="11" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
           HorizontalAlignment="Center" Margin="0,8,0,0">
    <Run Text="空格 开始/暂停 · Esc 重置 · 1-9 切换任务" />
</TextBlock>
```

### 涉及文件
| 文件 | 改动 |
|------|------|
| Views/Pages/TimerPage.xaml.cs | 数字键选择任务 |
| Views/MainWindow.xaml.cs | Tab 切换页面 |
| Views/Pages/TimerPage.xaml | 更新快捷键提示 |

---

## Feature 9: 学习计划功能

### 数据模型
```csharp
// Settings.cs 新增
public int DailyTargetPomodoros { get; set; } = 8;
```

### UI 改动
**TimerPage.xaml:**
```xml
<!-- 底部统计摘要，修改为 -->
<TextBlock FontSize="11" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
           HorizontalAlignment="Center">
    <Run Text="今日 " />
    <Run Text="{Binding TodayStats.CompletedPomodoros}" FontWeight="SemiBold" />
    <Run Text="/" />
    <Run Text="{Binding AppSettings.DailyTargetPomodoros}" FontWeight="SemiBold" />
    <Run Text=" · 专注 " />
    <Run Text="{Binding TodayStats.TotalFocusMinutes}" FontWeight="SemiBold" />
    <Run Text=" 分" />
</TextBlock>
```

**SettingsPage.xaml:**
```xml
<!-- 计时分组中新增 -->
<Grid Margin="0,12,0,0">
    <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
    <TextBlock Grid.Column="0" Text="每日目标（番茄钟数）" Style="{StaticResource SettingLabel}" />
    <ui:NumberBox Grid.Column="1" Value="{Binding DailyTargetPomodoros, UpdateSourceTrigger=PropertyChanged}" MinWidth="70" Minimum="0" Maximum="50" />
</Grid>
```

### 里程碑触发
**MainViewModel.cs:**
```csharp
// CheckMilestones() 中新增
if (AppSettings.DailyTargetPomodoros > 0 && todayCount >= AppSettings.DailyTargetPomodoros)
    ShowInAppNotification("里程碑", "今日番茄目标达成！");
```

### 涉及文件
| 文件 | 改动 |
|------|------|
| Models/Settings.cs | 新增 DailyTargetPomodoros |
| Views/Pages/TimerPage.xaml | 显示 X/Y 格式 |
| Views/Pages/SettingsPage.xaml | 新增配置项 |
| ViewModels/MainViewModel.cs | 里程碑触发 |
| ViewModels/SettingsViewModel.cs | 暴露属性 |

---

## Feature 10: 效率趋势分析

### 效率指标
- **完成率：** 完成的 Session 数 / 开始的 Session 数
- **平均专注时长：** 总分钟数 / 完成数
- **质量分：** 平均 QualityScore

### 数据模型
```csharp
// InsightModels.cs 新增
public class EfficiencyDataPoint
{
    public DateTime WeekStart { get; set; }
    public double CompletionRate { get; set; } // 0-1
    public double AvgFocusMinutes { get; set; }
    public double AvgQualityScore { get; set; } // 1-3
}
```

### InsightEngine 新增方法
```csharp
public List<EfficiencyDataPoint> GetEfficiencyTrend(List<FocusSession> sessions)
{
    var today = DateTime.Today;
    var thisMonday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
    if (thisMonday > today) thisMonday = thisMonday.AddDays(-7);

    var result = new List<EfficiencyDataPoint>(8);
    for (int i = 7; i >= 0; i--)
    {
        var weekStart = thisMonday.AddDays(-7 * i);
        var weekEnd = weekStart.AddDays(7);

        var weekSessions = sessions
            .Where(s => s.StartTime.Date >= weekStart.Date && s.StartTime.Date < weekEnd.Date)
            .ToList();

        var completed = weekSessions.Where(s => s.Completed).ToList();

        result.Add(new EfficiencyDataPoint
        {
            WeekStart = weekStart,
            CompletionRate = weekSessions.Count > 0 ? (double)completed.Count / weekSessions.Count : 0,
            AvgFocusMinutes = completed.Count > 0 ? completed.Average(s => s.FocusMinutes) : 0,
            AvgQualityScore = completed.Count > 0 ? completed.Average(s => s.QualityScore) : 0
        });
    }
    return result;
}
```

### UI 展示
**StatsPage.xaml:**
```xml
<!-- 在"周趋势"卡片下方新增 -->
<Border Style="{StaticResource StatsSection}">
    <StackPanel>
        <TextBlock Text="效率趋势" Style="{StaticResource SectionTitle}" />
        <controls:EfficiencyTrendChart EfficiencyData="{Binding EfficiencyTrend}" />
    </StackPanel>
</Border>
```

### 涉及文件
| 文件 | 改动 |
|------|------|
| Models/InsightModels.cs | 新增 EfficiencyDataPoint |
| Services/InsightEngine.cs | 新增 GetEfficiencyTrend |
| Controls/ | 新增 EfficiencyTrendChart 控件 |
| ViewModels/StatsViewModel.cs | 暴露 EfficiencyTrend |
| Views/Pages/StatsPage.xaml | 展示效率趋势 |

---

## Feature 11: 最佳学习时段推荐

### 分析逻辑
```csharp
// InsightEngine.cs 新增
public Insight? GetBestTimeRecommendation(List<FocusSession> sessions)
{
    var completed = sessions.Where(s => s.Completed && s.EndTime.HasValue).ToList();
    if (completed.Count < 10) return null;

    // 按小时分组，计算每小时的完成率和平均质量
    var hourStats = completed
        .GroupBy(s => s.EndTime!.Value.Hour)
        .Select(g => new
        {
            Hour = g.Key,
            Count = g.Count(),
            AvgQuality = g.Average(s => s.QualityScore),
            AvgMinutes = g.Average(s => s.FocusMinutes)
        })
        .Where(x => x.Count >= 3) // 至少 3 个样本
        .ToList();

    if (hourStats.Count == 0) return null;

    // 综合评分：质量分 * 0.6 + 标准化时长 * 0.4
    var maxMinutes = hourStats.Max(x => x.AvgMinutes);
    var best = hourStats
        .OrderByDescending(x => x.AvgQuality * 0.6 + (x.AvgMinutes / maxMinutes) * 0.4)
        .First();

    // 生成建议
    var timeRange = $"{best.Hour}:00-{best.Hour + 2}:00";
    return new Insight
    {
        Title = "最佳学习时段",
        Description = $"你{timeRange}效率最高，建议安排重要科目。",
        Type = InsightType.PeakHour
    };
}
```

### 智能洞察集成
**InsightEngine.GetInsights() 中：**
```csharp
// 在现有洞察生成后，添加时段建议
var timeRecommendation = GetBestTimeRecommendation(completed);
if (timeRecommendation != null && insights.Count < MaxInsightCount)
    insights.Add(timeRecommendation);
```

### 涉及文件
| 文件 | 改动 |
|------|------|
| Services/InsightEngine.cs | 新增 GetBestTimeRecommendation |

---

## 执行顺序建议

1. **Feature 4 (笔记)** - 中等复杂度，需要改数据模型
2. **Feature 5 (质量评分)** - 依赖笔记功能的数据模型改动
3. **Feature 6 (Streak)** - 简单，已有算法
4. **Feature 9 (学习计划)** - 简单，新增配置项
5. **Feature 7 (快速切换)** - 简单，UI 改动
6. **Feature 8 (键盘增强)** - 简纯，事件处理
7. **Feature 10 (效率趋势)** - 中等，需要新图表控件
8. **Feature 11 (时段推荐)** - 简单，纯算法

## 依赖关系
```
Feature 4 (笔记) ──→ Feature 5 (质量评分) ──→ Feature 10 (效率趋势)
                                                      ↓
Feature 6 (Streak) ──→ 独立                        Feature 11 (时段推荐)
Feature 9 (学习计划) ──→ 独立
Feature 7 (快速切换) ──→ 独立
Feature 8 (键盘增强) ──→ 独立
```
