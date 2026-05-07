# UI 设计优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Lumen Pomodoro 的 UI 从功能可用提升到视觉精致，核心升级为圆环进度指示器 + 完成庆祝动画 + 空状态 + 统计可视化。

**Architecture:** 在现有 WPF-UI 框架内，通过自定义控件（ArcProgress）、样式重构和动画增强实现升级。不引入新的第三方依赖。

**Tech Stack:** WPF + WPF-UI (Wpf.Ui 4.x) + 自定义 Arc 控件 + Storyboard 动画

---

## 文件结构

| 文件 | 职责 |
|------|------|
| `Controls/ArcProgress.xaml` + `.cs` | 圆环进度控件（新建） |
| `Converters/ProgressToArcConverter.cs` | Progress 百分比转 Arc 参数（新建） |
| `Views/Pages/TimerPage.xaml` | 计时器页面重构 |
| `Views/Pages/TimerPage.xaml.cs` | 计时器动画逻辑增强 |
| `Views/Pages/StatsPage.xaml` | 统计页面增加图表 + 空状态 |
| `Views/Pages/TasksPage.xaml` | 任务页面空状态 |
| `Views/Pages/SettingsPage.xaml` | 设置页分组卡片化 |
| `Themes/CustomStyles.xaml` | 新增/更新样式 |
| `ViewModels/MainViewModel.cs` | 新增 ProgressAngle 属性 |

---

## 第一批：核心视觉升级

### Task 1: 创建 ArcProgress 圆环进度控件

**Files:**
- Create: `LumenPomodoro/Controls/ArcProgress.xaml`
- Create: `LumenPomodoro/Controls/ArcProgress.xaml.cs`

- [ ] **Step 1: 创建 ArcProgress 控件 XAML**

```xml
<UserControl x:Class="LumenPomodoro.Controls.ArcProgress"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Path x:Name="BackgroundArc" Fill="Transparent" />
        <Path x:Name="ForegroundArc" Fill="Transparent" />
    </Grid>
</UserControl>
```

- [ ] **Step 2: 创建 ArcProgress 控件代码**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LumenPomodoro.Controls;

public partial class ArcProgress : UserControl
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ArcProgress),
            new PropertyMetadata(100.0, OnProgressChanged));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ArcProgress),
            new PropertyMetadata(4.0, OnProgressChanged));

    public static readonly DependencyProperty ForegroundArcBrushProperty =
        DependencyProperty.Register(nameof(ForegroundArcBrush), typeof(Brush), typeof(ArcProgress),
            new PropertyMetadata(null, OnProgressChanged));

    public static readonly DependencyProperty BackgroundArcBrushProperty =
        DependencyProperty.Register(nameof(BackgroundArcBrush), typeof(Brush), typeof(ArcProgress),
            new PropertyMetadata(null, OnProgressChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush ForegroundArcBrush
    {
        get => (Brush)GetValue(ForegroundArcBrushProperty);
        set => SetValue(ForegroundArcBrushProperty, value);
    }

    public Brush BackgroundArcBrush
    {
        get => (Brush)GetValue(BackgroundArcBrushProperty);
        set => SetValue(BackgroundArcBrushProperty, value);
    }

    public ArcProgress()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateArcs();
        Loaded += (_, _) => UpdateArcs();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ArcProgress)d).UpdateArcs();
    }

    private void UpdateArcs()
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var center = size / 2;
        var radius = center - StrokeThickness / 2;
        if (radius <= 0) return;

        BackgroundArc.Data = CreateCircleGeometry(center, radius);
        BackgroundArc.Stroke = BackgroundArcBrush ?? new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
        BackgroundArc.StrokeThickness = StrokeThickness;

        ForegroundArc.Data = CreateArcGeometry(center, radius, Progress / 100.0);
        ForegroundArc.Stroke = ForegroundArcBrush ?? (Brush)Application.Current.FindResource("AccentFillColorDefaultBrush");
        ForegroundArc.StrokeThickness = StrokeThickness;
        ForegroundArc.StrokeStartLineCap = PenLineCap.Round;
        ForegroundArc.StrokeEndLineCap = PenLineCap.Round;
    }

    private static Geometry CreateCircleGeometry(double center, double radius)
    {
        var figure = new PathFigure { StartPoint = new Point(center, center - radius) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(center - 0.001, center - radius),
            Size = new Size(radius, radius),
            IsLargeArc = true,
            SweepDirection = SweepDirection.Clockwise
        });
        return new PathGeometry { Figures = { figure } };
    }

    private static Geometry CreateArcGeometry(double center, double radius, double fraction)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        if (fraction <= 0) return Geometry.Empty;

        var startAngle = -Math.PI / 2;
        var endAngle = startAngle + 2 * Math.PI * fraction;

        var startX = center + radius * Math.Cos(startAngle);
        var startY = center + radius * Math.Sin(startAngle);
        var endX = center + radius * Math.Cos(endAngle);
        var endY = center + radius * Math.Sin(endAngle);

        var figure = new PathFigure { StartPoint = new Point(startX, startY) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            IsLargeArc = fraction > 0.5,
            SweepDirection = SweepDirection.Clockwise
        });
        return new PathGeometry { Figures = { figure } };
    }
}
```

- [ ] **Step 3: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 2: 重构 TimerPage — 圆环进度 + 视觉层次

**Files:**
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml`
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml.cs`

- [ ] **Step 1: 重写 TimerPage.xaml 布局**

核心变化：
- ArcProgress 圆环包裹计时器数字
- 任务选择器改为 Chip 样式（带背景色圆角矩形）
- 移除 SlimProgressBar
- 摄像头提醒行移至圆环下方
- 底部统计摘要保留

```xml
<Page x:Class="LumenPomodoro.Views.Pages.TimerPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      xmlns:converters="clr-namespace:LumenPomodoro.Converters"
      xmlns:controls="clr-namespace:LumenPomodoro.Controls"
      xmlns:models="clr-namespace:LumenPomodoro.Models">

    <Page.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/Themes/CustomStyles.xaml" />
            </ResourceDictionary.MergedDictionaries>
            <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
            <converters:StatusToVisibilityConverter x:Key="StatusToVisibilityConverter" />
        </ResourceDictionary>
    </Page.Resources>

    <Grid Margin="32,0">
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- 任务选择 Chip -->
        <Border Grid.Row="1" HorizontalAlignment="Center" Margin="0,0,0,20"
                Background="{DynamicResource ControlFillColorDefaultBrush}"
                CornerRadius="12" Padding="10,5" Cursor="Hand"
                MouseDown="TaskName_MouseDown"
                Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVisibilityConverter}, ConverterParameter=Idle}">
            <StackPanel Orientation="Horizontal">
                <Ellipse Width="8" Height="8" Margin="0,0,8,0" VerticalAlignment="Center">
                    <Ellipse.Fill>
                        <SolidColorBrush Color="{Binding SelectedTask.Color, FallbackValue=#6B7280}" />
                    </Ellipse.Fill>
                </Ellipse>
                <TextBlock FontSize="13" Foreground="{DynamicResource TextFillColorSecondaryBrush}" VerticalAlignment="Center">
                    <Run Text="{Binding SelectedTask.Name, Mode=OneWay, FallbackValue='选择任务'}" />
                    <Run Text="  ▾" FontSize="10" Foreground="{DynamicResource TextFillColorTertiaryBrush}" />
                </TextBlock>
            </StackPanel>
        </Border>

        <!-- 圆环进度 + 计时器数字 -->
        <Grid Grid.Row="2" HorizontalAlignment="Center" Margin="0,0,0,20" Width="240" Height="240">
            <controls:ArcProgress Progress="{Binding Progress}" StrokeThickness="4"
                                  ForegroundArcBrush="{DynamicResource AccentFillColorDefaultBrush}"
                                  BackgroundArcBrush="{DynamicResource ControlFillColorDefaultBrush}" />
            <TextBlock x:Name="TimerTextBlock" Text="{Binding RemainingTime}"
                       Style="{StaticResource TimerText}" FontSize="56"
                       HorizontalAlignment="Center" VerticalAlignment="Center"
                       RenderTransformOrigin="0.5,0.5">
                <TextBlock.RenderTransform>
                    <ScaleTransform ScaleX="1" ScaleY="1" />
                </TextBlock.RenderTransform>
            </TextBlock>
        </Grid>

        <!-- 摄像头提醒 -->
        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,12"
                    Visibility="{Binding IsCameraAlertActive, Converter={StaticResource BooleanToVisibilityConverter}}">
            <Ellipse x:Name="CameraAlertDot" Width="8" Height="8" Fill="{DynamicResource AccentFillColorDefaultBrush}" Margin="0,0,8,0" />
            <TextBlock Text="摄像头提醒中" FontSize="12" Foreground="{DynamicResource TextFillColorSecondaryBrush}" VerticalAlignment="Center" />
            <ui:Button Appearance="Secondary" Click="StopCameraButton_Click" FontSize="12" Padding="6,2" Margin="4,0,0,0">
                关闭
            </ui:Button>
        </StackPanel>

        <!-- 操作按钮区 -->
        <Grid Grid.Row="4">
            <StackPanel x:Name="IdlePanel" Orientation="Vertical" HorizontalAlignment="Center">
                <StackPanel.Style>
                    <Style TargetType="StackPanel">
                        <Setter Property="Visibility" Value="{Binding CurrentStatus, Converter={StaticResource StatusToVisibilityConverter}, ConverterParameter=Idle}" />
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsFocusCompleted}" Value="True">
                                <Setter Property="Visibility" Value="Collapsed" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </StackPanel.Style>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,8">
                    <ui:Button Appearance="Secondary" Click="AdjustTimeDown_Click" FontSize="16" Padding="8,2">-</ui:Button>
                    <TextBlock Text="{Binding AppSettings.WorkMinutes}" FontSize="13" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                               VerticalAlignment="Center" Margin="6,0" />
                    <TextBlock Text="分钟" FontSize="13" Foreground="{DynamicResource TextFillColorTertiaryBrush}" VerticalAlignment="Center" />
                    <ui:Button Appearance="Secondary" Click="AdjustTimeUp_Click" FontSize="16" Padding="8,2">+</ui:Button>
                </StackPanel>
                <ui:Button Appearance="Primary" Click="StartFocusButton_Click" FontSize="17" Padding="22,11">
                    开始专注
                </ui:Button>
            </StackPanel>

            <StackPanel x:Name="FocusPanel"
                        Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVisibilityConverter}, ConverterParameter=Focus}"
                        Orientation="Horizontal" HorizontalAlignment="Center">
                <ui:Button Appearance="Secondary" Click="PauseButton_Click">暂停</ui:Button>
                <ui:Button Appearance="Secondary" Click="ResetButton_Click" Margin="8,0,0,0">重置</ui:Button>
            </StackPanel>

            <StackPanel x:Name="PausedPanel"
                        Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVisibilityConverter}, ConverterParameter=Paused}"
                        Orientation="Horizontal" HorizontalAlignment="Center">
                <ui:Button Appearance="Primary" Click="ResumeButton_Click">继续</ui:Button>
                <ui:Button Appearance="Secondary" Click="ResetButton_Click" Margin="8,0,0,0">重置</ui:Button>
            </StackPanel>

            <StackPanel x:Name="BreakPanel"
                        Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVisibilityConverter}, ConverterParameter=Break}"
                        Orientation="Horizontal" HorizontalAlignment="Center">
                <ui:Button Appearance="Primary" Click="EndBreakButton_Click">结束休息</ui:Button>
            </StackPanel>

            <StackPanel x:Name="CompletedPanel" Orientation="Horizontal" HorizontalAlignment="Center">
                <StackPanel.Style>
                    <Style TargetType="StackPanel">
                        <Setter Property="Visibility" Value="Collapsed" />
                        <Style.Triggers>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding IsFocusCompleted}" Value="True" />
                                    <Condition Binding="{Binding CurrentStatus, Converter={StaticResource StatusToVisibilityConverter}, ConverterParameter=Idle}" Value="Visible" />
                                </MultiDataTrigger.Conditions>
                                <Setter Property="Visibility" Value="Visible" />
                            </MultiDataTrigger>
                        </Style.Triggers>
                    </Style>
                </StackPanel.Style>
                <ui:Button Appearance="Primary" Click="StartShortBreakButton_Click">短休息</ui:Button>
                <ui:Button Appearance="Secondary" Click="StartLongBreakButton_Click" Margin="8,0,0,0">长休息</ui:Button>
                <ui:Button Appearance="Secondary" Click="SkipBreakButton_Click" Margin="8,0,0,0">跳过</ui:Button>
            </StackPanel>
        </Grid>

        <!-- 底部统计摘要 -->
        <TextBlock Grid.Row="5" FontSize="11" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                   HorizontalAlignment="Center" Margin="0,16,0,0" Cursor="Hand" MouseDown="StatsSummary_MouseDown">
            <Run Text="今日 " />
            <Run Text="{Binding TodayStats.CompletedPomodoros}" Foreground="{DynamicResource TextFillColorSecondaryBrush}" FontWeight="SemiBold" />
            <Run Text=" · 专注 " />
            <Run Text="{Binding TodayStats.TotalFocusMinutes}" Foreground="{DynamicResource TextFillColorSecondaryBrush}" FontWeight="SemiBold" />
            <Run Text="分" />
        </TextBlock>
    </Grid>
</Page>
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 3: 专注完成庆祝动画

**Files:**
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml.cs`

- [ ] **Step 1: 在 TimerPage.xaml.cs 中添加完成动画**

在 `ViewModel_PropertyChanged` 的 `IsFocusCompleted` 分支中，当 `IsFocusCompleted=true` 时：
1. 圆环 ForegroundArcBrush 闪烁为绿色（#10B981）
2. TimerTextBlock 执行 Scale 1.0→1.15→1.0 弹跳动画（300ms）
3. 然后启动呼吸动画

需要给 ArcProgress 控件一个 x:Name（如 `ProgressRing`），在代码中访问其 `ForegroundArcBrush`。

```csharp
private Storyboard? _completionStoryboard;

private void PlayCompletionAnimation()
{
    if (!_viewModel.AppSettings.AnimationEnabled) return;

    StopCompletionAnimation();

    var scaleAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(400) };
    scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
    scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.15, KeyTime.FromPercent(0.4))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });

    Storyboard.SetTarget(scaleAnim, TimerTextBlock);
    Storyboard.SetTargetProperty(scaleAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

    var scaleYAnim = scaleAnim.Clone();
    Storyboard.SetTarget(scaleYAnim, TimerTextBlock);
    Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

    _completionStoryboard = new Storyboard();
    _completionStoryboard.Children.Add(scaleAnim);
    _completionStoryboard.Children.Add(scaleYAnim);
    _completionStoryboard.Begin();
}

private void StopCompletionAnimation()
{
    if (_completionStoryboard == null) return;
    _completionStoryboard.Stop();
    _completionStoryboard = null;
}
```

修改 `ViewModel_PropertyChanged` 中 `IsFocusCompleted` 分支：

```csharp
case nameof(MainViewModel.IsFocusCompleted):
    Dispatcher.BeginInvoke(() =>
    {
        if (_viewModel.IsFocusCompleted)
        {
            PlayCompletionAnimation();
            StartBreathingAnimation();
        }
        else
        {
            StopCompletionAnimation();
            StopBreathingAnimation();
        }
    });
    break;
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

## 第二批：体验完善

### Task 4: 空状态设计

**Files:**
- Modify: `LumenPomodoro/Views/Pages/StatsPage.xaml`
- Modify: `LumenPomodoro/Views/Pages/TasksPage.xaml`

- [ ] **Step 1: StatsPage 添加空状态**

在 StatsPage 的任务分布区域，当 TaskStats 为空时显示空状态提示。使用 DataTrigger 绑定 TaskStats.Count：

```xml
<!-- 在 ScrollViewer 同级添加 -->
<StackPanel Grid.Row="3" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="0,40,0,0">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Collapsed" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding TaskStats.Count}" Value="0">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>
    <TextBlock Text="&#xE73E;" FontFamily="Segoe MDL2 Assets" FontSize="36"
               HorizontalAlignment="Center" Foreground="{DynamicResource TextFillColorTertiaryBrush}" Margin="0,0,0,8" />
    <TextBlock Text="还没有完成记录" FontSize="14" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
               HorizontalAlignment="Center" />
    <TextBlock Text="完成第一个番茄钟后这里会显示统计" FontSize="12" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
               HorizontalAlignment="Center" Margin="0,4,0,0" />
</StackPanel>
```

同时给 ScrollViewer 添加相反的 DataTrigger（Count>0 时才 Visible）。

- [ ] **Step 2: TasksPage 添加空状态**

在任务列表区域，当 Tasks 为空时显示空状态：

```xml
<StackPanel Grid.Row="1" HorizontalAlignment="Center" VerticalAlignment="Center">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Collapsed" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding Tasks.Count}" Value="0">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>
    <TextBlock Text="&#xE710;" FontFamily="Segoe MDL2 Assets" FontSize="36"
               HorizontalAlignment="Center" Foreground="{DynamicResource TextFillColorTertiaryBrush}" Margin="0,0,0,8" />
    <TextBlock Text="还没有任务" FontSize="14" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
               HorizontalAlignment="Center" />
    <TextBlock Text="在下方添加你的第一个任务" FontSize="12" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
               HorizontalAlignment="Center" Margin="0,4,0,0" />
</StackPanel>
```

- [ ] **Step 3: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 5: 状态切换过渡动画

**Files:**
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml.cs`

- [ ] **Step 1: 添加面板切换淡入淡出**

为 IdlePanel、FocusPanel、PausedPanel、BreakPanel、CompletedPanel 添加 Opacity 过渡。在 `ViewModel_PropertyChanged` 的 `CurrentStatus` 分支中：

```csharp
case nameof(MainViewModel.CurrentStatus):
    Dispatcher.BeginInvoke(() =>
    {
        FadeInActivePanel();
        if (_viewModel.CurrentStatus == TimerMode.Paused)
            StartPausedPulseAnimation();
        else
            StopPausedPulseAnimation();
    });
    break;
```

```csharp
private void FadeInActivePanel()
{
    var panels = new[] { IdlePanel, FocusPanel, PausedPanel, BreakPanel, CompletedPanel };
    foreach (var panel in panels)
    {
        if (panel.Visibility == Visibility.Visible)
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            panel.BeginAnimation(UIElement.OpacityProperty, anim);
        }
        else
        {
            panel.Opacity = 0;
        }
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 6: 按钮视觉层级优化

**Files:**
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml`

- [ ] **Step 1: 调整按钮尺寸和间距**

- 主操作按钮（开始专注、继续、短休息）：`FontSize="15" Padding="20,10"`
- 次操作按钮（暂停、重置、长休息）：`FontSize="13" Padding="14,8"`
- 三级操作（跳过）：`Appearance="Transparent"` 或更小字号

- [ ] **Step 2: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

## 第三批：功能增强

### Task 7: 统计页条形图

**Files:**
- Modify: `LumenPomodoro/Views/Pages/StatsPage.xaml`
- Modify: `LumenPomodoro/ViewModels/StatsViewModel.cs`

- [ ] **Step 1: StatsViewModel 添加 MaxCount 属性**

```csharp
private int _maxCount;

public int MaxCount
{
    get => _maxCount;
    set { if (_maxCount != value) { _maxCount = value; OnPropertyChanged(); } }
}
```

在 `Refresh()` 中计算：`MaxCount = TaskStats.Any() ? TaskStats.Max(t => t.Count) : 1;`

- [ ] **Step 2: StatsPage 任务分布改为条形图**

替换当前简单的 Grid 行为带进度条的行：

```xml
<ItemsControl ItemsSource="{Binding TaskStats}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="0,0,0,12">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <Ellipse Grid.Row="0" Grid.Column="0" Width="8" Height="8" Margin="0,0,8,0" VerticalAlignment="Center">
                    <Ellipse.Fill>
                        <SolidColorBrush Color="{Binding Color}" />
                    </Ellipse.Fill>
                </Ellipse>
                <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding TaskName}" FontSize="13"
                           Foreground="{DynamicResource TextFillColorPrimaryBrush}" VerticalAlignment="Center" />
                <TextBlock Grid.Row="0" Grid.Column="2" Text="{Binding Count}" FontSize="13" FontWeight="SemiBold"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}" VerticalAlignment="Center" />

                <Rectangle Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="3" Height="4" RadiusX="2" RadiusY="2"
                           Margin="0,4,0,0" HorizontalAlignment="Left"
                           Fill="{DynamicResource AccentFillColorDefaultBrush}">
                    <Rectangle.Width>
                        <MultiBinding Converter="{StaticResource RatioToWidthConverter}">
                            <Binding Path="Count" />
                            <Binding Path="DataContext.MaxCount" RelativeSource="{RelativeSource AncestorType=Page}" />
                        </MultiBinding>
                    </Rectangle.Width>
                </Rectangle>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

注意：需要创建 `RatioToWidthConverter` 或用更简单的方式——在 `TaskStatItem` 中直接计算 `BarWidth` 属性。

更简单的方案：在 TaskStatItem 中添加 `BarWidth` 属性，在 StatsViewModel.Refresh 中计算。

- [ ] **Step 3: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 8: 设置页分组卡片化

**Files:**
- Modify: `LumenPomodoro/Views/Pages/SettingsPage.xaml`

- [ ] **Step 1: 用 CardControl 包裹各设置区块**

将每个 Section（计时、摄像头、提醒、外观、系统）的内容包裹在 `ui:CardControl` 中，增加视觉分组和间距：

```xml
<ui:CardControl Margin="0,0,0,16" Padding="16">
    <ui:CardControl.Content>
        <StackPanel>
            <TextBlock Text="计时" Style="{StaticResource SectionTitle}" />
            <!-- 原有设置项 -->
        </StackPanel>
    </ui:CardControl.Content>
</ui:CardControl>
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 9: 计时器等宽字体 + 任务颜色跟随圆环

**Files:**
- Modify: `LumenPomodoro/Themes/CustomStyles.xaml`
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml`

- [ ] **Step 1: 计时器数字改用等宽字体**

在 CustomStyles.xaml 中修改 TimerText 样式，FontFamily 改为 Consolas（Windows 内置等宽字体）：

```xml
<Style x:Key="TimerText" TargetType="TextBlock">
    <Setter Property="FontSize" Value="56" />
    <Setter Property="FontWeight" Value="Normal" />
    <Setter Property="FontFamily" Value="Consolas" />
    ...
</Style>
```

- [ ] **Step 2: 圆环颜色跟随任务颜色**

在 TimerPage.xaml 中，ArcProgress 的 ForegroundArcBrush 绑定到 SelectedTask.Color：

```xml
<controls:ArcProgress Progress="{Binding Progress}" StrokeThickness="4">
    <controls:ArcProgress.ForegroundArcBrush>
        <SolidColorBrush Color="{Binding SelectedTask.Color, FallbackValue=#0078D4}" />
    </controls:ArcProgress.ForegroundArcBrush>
    <controls:ArcProgress.BackgroundArcBrush>
        <SolidColorBrush Color="#1A808080" />
    </controls:ArcProgress.BackgroundArcBrush>
</controls:ArcProgress>
```

- [ ] **Step 3: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 10: 键盘快捷键

**Files:**
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml`
- Modify: `LumenPomodoro/Views/Pages/TimerPage.xaml.cs`

- [ ] **Step 1: 添加 InputBindings**

在 TimerPage 的 Page.InputBindings 中添加：

```xml
<Page.InputBindings>
    <KeyBinding Key="Space" Command="{x:Static local:TimerPage.ToggleCommand}" />
    <KeyBinding Key="R" Modifiers="Ctrl" Command="{x:Static local:TimerPage.ResetCommand}" />
</Page.InputBindings>
```

- [ ] **Step 2: 实现 RoutedCommand**

在 TimerPage.xaml.cs 中添加静态命令和绑定：

```csharp
public static readonly RoutedCommand ToggleCommand = new();
public static readonly RoutedCommand ResetCommand = new();

// 在构造函数中：
CommandBindings.Add(new CommandBinding(ToggleCommand, Toggle_Executed));
CommandBindings.Add(new CommandBinding(ResetCommand, Reset_Executed));

private void Toggle_Executed(object sender, ExecutedRoutedEventArgs e)
{
    switch (_viewModel.CurrentStatus)
    {
        case TimerMode.Idle: _viewModel.StartFocus(); break;
        case TimerMode.Focus:
        case TimerMode.Break: _viewModel.PauseFocus(); break;
        case TimerMode.Paused: _viewModel.ResumeFocus(); break;
    }
}

private void Reset_Executed(object sender, ExecutedRoutedEventArgs e)
{
    _viewModel.ResetFocus();
}
```

- [ ] **Step 3: 构建验证**

Run: `dotnet build LumenPomodoro/LumenPomodoro.csproj`
Expected: 0 error

---

### Task 11: 最终验证 + 提交

- [ ] **Step 1: 完整构建**

Run: `dotnet build LumenPomodoro.sln`
Expected: 0 warning, 0 error

- [ ] **Step 2: 运行测试**

Run: `dotnet test LumenPomodoro.Tests/LumenPomodoro.Tests.csproj`
Expected: 21/21 pass

- [ ] **Step 3: 提交**

```bash
git add .
git commit -m "feat(ui): 全面 UI 设计优化 — 圆环进度/完成动画/空状态/统计图表/设置分组/快捷键"
```
