# Lumen Pomodoro UI 重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Lumen Pomodoro 从手写 WPF 样式 + 多窗口架构重构为 WPF-UI FluentWindow + 单窗口导航架构，实现真实 Mica/Acrylic 玻璃效果。

**Architecture:** FluentWindow 作为主窗口，底部 NavigationView 承载 4 个 Page（TimerPage/TasksPage/StatsPage/SettingsPage），WPF-UI 控件替代所有手写 ControlTemplate，CustomStyles.xaml 仅保留业务特定样式。

**Tech Stack:** WPF-UI 3.x (`Wpf.Ui`), .NET 9, WPF, Inter 字体

---

### Task 1: 备份 + 添加 WPF-UI NuGet

**Files:**
- Modify: `LumenPomodoro/LumenPomodoro.csproj`

- [ ] **Step 1: Git 备份**

```bash
cd f:\EverythingProject\github\lumen-pomodoro
git add .
git commit -m "chore: backup before UI refactor"
```

- [ ] **Step 2: 添加 Wpf.Ui NuGet 包**

修改 `LumenPomodoro.csproj`，在 `<ItemGroup>` 中添加：

```xml
<PackageReference Include="Wpf.Ui" Version="3.*" />
```

完整 csproj：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="1.1.0" />
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
    <PackageReference Include="System.Management" Version="8.0.0" />
    <PackageReference Include="Wpf.Ui" Version="3.*" />
  </ItemGroup>

  <ItemGroup>
    <Resource Include="Fonts\*.ttf" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: 还原 NuGet 并验证构建**

```bash
cd f:\EverythingProject\github\lumen-pomodoro
dotnet restore LumenPomodoro.sln
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功，0 error

- [ ] **Step 4: 运行现有测试确认无回归**

```bash
dotnet test LumenPomodoro.sln --configuration Release --no-build
```

Expected: 21/21 通过

- [ ] **Step 5: 提交**

```bash
git add .
git commit -m "chore: add Wpf.Ui NuGet package"
```

---

### Task 2: 创建 CustomStyles.xaml

**Files:**
- Create: `LumenPomodoro/Themes/CustomStyles.xaml`

此文件仅包含 WPF-UI 无法覆盖的业务特定样式。

- [ ] **Step 1: 创建 CustomStyles.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <FontFamily x:Key="InterLight">pack://application:,,,/Fonts/#Inter Light</FontFamily>
    <FontFamily x:Key="InterRegular">pack://application:,,,/Fonts/#Inter</FontFamily>
    <FontFamily x:Key="InterSemiBold">pack://application:,,,/Fonts/#Inter SemiBold</FontFamily>

    <Style x:Key="TimerText" TargetType="TextBlock">
        <Setter Property="FontSize" Value="80" />
        <Setter Property="FontWeight" Value="Light" />
        <Setter Property="FontFamily" Value="{StaticResource InterLight}" />
        <Setter Property="Foreground" Value="{DynamicResource TextFillColorPrimaryBrush}" />
        <Setter Property="HorizontalAlignment" Value="Center" />
        <Setter Property="RenderTransformOrigin" Value="0.5,0.5" />
        <Setter Property="RenderTransform">
            <Setter.Value>
                <ScaleTransform ScaleX="1" ScaleY="1" />
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="StatNumber" TargetType="TextBlock">
        <Setter Property="FontSize" Value="40" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="FontFamily" Value="{StaticResource InterSemiBold}" />
        <Setter Property="HorizontalAlignment" Value="Center" />
    </Style>

    <Style x:Key="PageTitle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="21" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="FontFamily" Value="{StaticResource InterSemiBold}" />
        <Setter Property="Foreground" Value="{DynamicResource TextFillColorPrimaryBrush}" />
    </Style>

    <Style x:Key="SectionTitle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="14" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="FontFamily" Value="{StaticResource InterSemiBold}" />
        <Setter Property="Foreground" Value="{DynamicResource TextFillColorPrimaryBrush}" />
        <Setter Property="Margin" Value="0,0,0,10" />
    </Style>

    <Style x:Key="SettingLabel" TargetType="TextBlock">
        <Setter Property="FontSize" Value="14" />
        <Setter Property="FontFamily" Value="{StaticResource InterRegular}" />
        <Setter Property="Foreground" Value="{DynamicResource TextFillColorSecondaryBrush}" />
        <Setter Property="VerticalAlignment" Value="Center" />
    </Style>

    <Style x:Key="SlimProgressBar" TargetType="ProgressBar">
        <Setter Property="Height" Value="2" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Background" Value="{DynamicResource ControlFillColorDefaultBrush}" />
        <Setter Property="Foreground" Value="{DynamicResource AccentFillColorDefaultBrush}" />
    </Style>

</ResourceDictionary>
```

- [ ] **Step 2: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功

- [ ] **Step 3: 提交**

```bash
git add .
git commit -m "feat: add CustomStyles.xaml with business-specific styles"
```

---

### Task 3: 创建 TasksViewModel + StatsViewModel

**Files:**
- Create: `LumenPomodoro/ViewModels/TasksViewModel.cs`
- Create: `LumenPomodoro/ViewModels/StatsViewModel.cs`

- [ ] **Step 1: 创建 TasksViewModel.cs**

从 TaskManagerWindow.xaml.cs 后台代码提取任务管理逻辑：

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public class TasksViewModel : INotifyPropertyChanged
{
    private readonly StorageService _storageService;

    private ObservableCollection<TaskItem> _tasks = new();
    private string _newTaskName = string.Empty;
    private string _selectedCategory = "数学";
    private TaskItem? _editingTask;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? TasksChanged;

    public ObservableCollection<TaskItem> Tasks
    {
        get => _tasks;
        set { if (!ReferenceEquals(_tasks, value)) { _tasks = value; OnPropertyChanged(); } }
    }

    public string NewTaskName
    {
        get => _newTaskName;
        set { if (_newTaskName != value) { _newTaskName = value; OnPropertyChanged(); } }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (_selectedCategory != value) { _selectedCategory = value; OnPropertyChanged(); } }
    }

    public string[] Categories => TaskCategories.Categories;

    public TasksViewModel(StorageService storageService)
    {
        _storageService = storageService;
        LoadTasks();
    }

    public void LoadTasks()
    {
        var tasks = _storageService.LoadTasks();
        Tasks = new ObservableCollection<TaskItem>(tasks);
    }

    public void AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;

        var task = new TaskItem
        {
            Name = NewTaskName.Trim(),
            Category = SelectedCategory,
            Color = TaskCategories.GetCategoryColor(SelectedCategory)
        };

        var tasks = _storageService.LoadTasks();
        tasks.Add(task);
        _storageService.SaveTasks(tasks);

        Tasks.Add(task);
        NewTaskName = string.Empty;
        TasksChanged?.Invoke();
    }

    public void DeleteTask(string taskId)
    {
        var tasks = _storageService.LoadTasks();
        tasks.RemoveAll(t => t.Id == taskId);
        _storageService.SaveTasks(tasks);

        var item = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (item != null) Tasks.Remove(item);
        TasksChanged?.Invoke();
    }

    public void StartEdit(TaskItem task)
    {
        _editingTask = task;
    }

    public void FinishEdit(string taskId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        var tasks = _storageService.LoadTasks();
        var task = tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.Name = newName.Trim();
            _storageService.SaveTasks(tasks);
            LoadTasks();
            TasksChanged?.Invoke();
        }
        _editingTask = null;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

- [ ] **Step 2: 创建 StatsViewModel.cs**

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.ViewModels;

public class TaskStatItem
{
    public string TaskName { get; set; } = string.Empty;
    public string Color { get; set; } = "#6B7280";
    public int Count { get; set; }
}

public class StatsViewModel : INotifyPropertyChanged
{
    private readonly StorageService _storageService;

    private int _completedPomodoros;
    private int _totalFocusMinutes;
    private ObservableCollection<TaskStatItem> _taskStats = new();

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

    public ObservableCollection<TaskStatItem> TaskStats
    {
        get => _taskStats;
        set { if (!ReferenceEquals(_taskStats, value)) { _taskStats = value; OnPropertyChanged(); } }
    }

    public StatsViewModel(StorageService storageService)
    {
        _storageService = storageService;
        Refresh();
    }

    public void Refresh()
    {
        var stats = _storageService.GetTodayStats();
        CompletedPomodoros = stats.CompletedPomodoros;
        TotalFocusMinutes = stats.TotalFocusMinutes;

        var sessions = _storageService.LoadSessions();
        var today = DateTime.Today;
        var todaySessions = sessions.Where(s => s.StartTime.Date == today && s.Completed).ToList();

        var grouped = todaySessions
            .GroupBy(s => s.TaskName)
            .Select(g => new TaskStatItem
            {
                TaskName = g.Key,
                Color = GetTaskColor(g.Key, _storageService.LoadTasks()),
                Count = g.Count()
            })
            .OrderByDescending(t => t.Count)
            .ToList();

        TaskStats = new ObservableCollection<TaskStatItem>(grouped);
    }

    private static string GetTaskColor(string taskName, List<TaskItem> tasks)
    {
        var task = tasks.FirstOrDefault(t => t.Name == taskName);
        return task?.Color ?? "#6B7280";
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

- [ ] **Step 3: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "feat: add TasksViewModel and StatsViewModel"
```

---

### Task 4: 创建 TimerPage

**Files:**
- Create: `LumenPomodoro/Views/Pages/TimerPage.xaml`
- Create: `LumenPomodoro/Views/Pages/TimerPage.xaml.cs`

从 MainWindow.xaml 提取计时器 UI，改用 WPF-UI 控件。

- [ ] **Step 1: 创建 Pages 目录**

```bash
mkdir -p LumenPomodoro/Views/Pages
```

- [ ] **Step 2: 创建 TimerPage.xaml**

```xml
<Page x:Class="LumenPomodoro.Views.Pages.TimerPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      xmlns:converters="clr-namespace:LumenPomodoro.Converters"
      Title="TimerPage">

    <Page.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis" />
        <converters:StatusToVisibilityConverter x:Key="StatusToVis" />
    </Page.Resources>

    <Grid VerticalAlignment="Center" Margin="32,0">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,48"
                    Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVis}, ConverterParameter=Idle}">
            <Ellipse Width="8" Height="8" Margin="0,0,8,0" VerticalAlignment="Center">
                <Ellipse.Fill>
                    <SolidColorBrush Color="{Binding SelectedTask.Color, FallbackValue=#6B7280}" />
                </Ellipse.Fill>
            </Ellipse>
            <TextBlock FontSize="14" Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                       Cursor="Hand" MouseDown="TaskName_MouseDown">
                <Run Text="{Binding SelectedTask.Name, Mode=OneWay, FallbackValue='选择任务'}" />
                <Run Text="  ▾" FontSize="10" Foreground="{DynamicResource TextFillColorTertiaryBrush}" />
            </TextBlock>
        </StackPanel>

        <TextBlock Grid.Row="1" x:Name="TimerTextBlock" Text="{Binding RemainingTime}"
                   Style="{StaticResource TimerText}" Margin="0,0,0,24" />

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,16"
                    Visibility="{Binding IsCameraAlertActive, Converter={StaticResource BoolToVis}}">
            <Ellipse x:Name="CameraAlertDot" Width="8" Height="8" Fill="{DynamicResource AccentFillColorDefaultBrush}" Margin="0,0,8,0" />
            <TextBlock Text="摄像头提醒中" FontSize="12" Foreground="{DynamicResource TextFillColorSecondaryBrush}" VerticalAlignment="Center" />
            <ui:Button Appearance="Transparent" Click="StopCamera_Click" FontSize="12" Padding="6,2" Margin="4,0,0,0">
                关闭
            </ui:Button>
        </StackPanel>

        <Border Grid.Row="3" Margin="48,0,48,48" ClipToBounds="True">
            <ProgressBar x:Name="ProgressBar" Value="{Binding Progress}" Minimum="0" Maximum="100"
                         Style="{StaticResource SlimProgressBar}" />
        </Border>

        <Grid Grid.Row="4">
            <StackPanel x:Name="IdlePanel" Orientation="Vertical" HorizontalAlignment="Center"
                        Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVis}, ConverterParameter=Idle}">
                <StackPanel.Style>
                    <Style TargetType="StackPanel">
                        <Setter Property="Visibility" Value="{Binding CurrentStatus, Converter={StaticResource StatusToVis}, ConverterParameter=Idle}" />
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsFocusCompleted}" Value="True">
                                <Setter Property="Visibility" Value="Collapsed" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </StackPanel.Style>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,8">
                    <ui:Button Appearance="Transparent" Click="AdjustTimeDown_Click" FontSize="16" Padding="8,2">−</ui:Button>
                    <TextBlock Text="{Binding AppSettings.WorkMinutes}" FontSize="13" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                               VerticalAlignment="Center" Margin="6,0" />
                    <TextBlock Text="分钟" FontSize="13" Foreground="{DynamicResource TextFillColorTertiaryBrush}" VerticalAlignment="Center" />
                    <ui:Button Appearance="Transparent" Click="AdjustTimeUp_Click" FontSize="16" Padding="8,2">+</ui:Button>
                </StackPanel>
                <ui:Button Appearance="Primary" Click="StartFocus_Click" Padding="22,11" FontSize="17">
                    开始专注
                </ui:Button>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,12,0,0"
                            Visibility="{Binding IsPendingBreak, Converter={StaticResource BoolToVis}}">
                    <ui:Button Appearance="Primary" Click="StartShortBreak_Click">短休息</ui:Button>
                    <ui:Button Appearance="Transparent" Click="StartLongBreak_Click" Margin="8,0,0,0">长休息</ui:Button>
                    <ui:Button Appearance="Transparent" Click="SkipBreak_Click">跳过</ui:Button>
                </StackPanel>
            </StackPanel>

            <StackPanel x:Name="FocusPanel"
                        Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVis}, ConverterParameter=Focus}"
                        Orientation="Horizontal" HorizontalAlignment="Center">
                <ui:Button Appearance="Secondary" Click="Pause_Click">暂停</ui:Button>
                <ui:Button Appearance="Transparent" Click="Reset_Click" Margin="8,0,0,0">重置</ui:Button>
            </StackPanel>

            <StackPanel x:Name="PausedPanel"
                        Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVis}, ConverterParameter=Paused}"
                        Orientation="Horizontal" HorizontalAlignment="Center">
                <ui:Button Appearance="Primary" Click="Resume_Click">继续</ui:Button>
                <ui:Button Appearance="Transparent" Click="Reset_Click" Margin="8,0,0,0">重置</ui:Button>
            </StackPanel>

            <StackPanel x:Name="BreakPanel"
                        Visibility="{Binding CurrentStatus, Converter={StaticResource StatusToVis}, ConverterParameter=Break}"
                        Orientation="Horizontal" HorizontalAlignment="Center">
                <ui:Button Appearance="Primary" Click="EndBreak_Click">结束休息</ui:Button>
            </StackPanel>

            <StackPanel x:Name="CompletedPanel" Orientation="Horizontal" HorizontalAlignment="Center">
                <StackPanel.Style>
                    <Style TargetType="StackPanel">
                        <Setter Property="Visibility" Value="Collapsed" />
                        <Style.Triggers>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding IsFocusCompleted}" Value="True" />
                                    <Condition Binding="{Binding CurrentStatus, Converter={StaticResource StatusToVis}, ConverterParameter=Idle}" Value="Visible" />
                                </MultiDataTrigger.Conditions>
                                <Setter Property="Visibility" Value="Visible" />
                            </MultiDataTrigger>
                        </Style.Triggers>
                    </Style>
                </StackPanel.Style>
                <ui:Button Appearance="Primary" Click="StartShortBreak_Click">短休息</ui:Button>
                <ui:Button Appearance="Transparent" Click="StartLongBreak_Click" Margin="8,0,0,0">长休息</ui:Button>
                <ui:Button Appearance="Transparent" Click="SkipBreak_Click">跳过</ui:Button>
            </StackPanel>
        </Grid>

        <TextBlock Grid.Row="5" FontSize="12" Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                   HorizontalAlignment="Center" Margin="0,24,0,0" Cursor="Hand" MouseDown="StatsSummary_MouseDown">
            <Run Text="今日 " />
            <Run Text="{Binding TodayStats.CompletedPomodoros}" Foreground="{DynamicResource TextFillColorSecondaryBrush}" FontWeight="SemiBold" />
            <Run Text=" · 专注 " />
            <Run Text="{Binding TodayStats.TotalFocusMinutes}" Foreground="{DynamicResource TextFillColorSecondaryBrush}" FontWeight="SemiBold" />
            <Run Text="分" />
        </TextBlock>
    </Grid>
</Page>
```

- [ ] **Step 3: 创建 TimerPage.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class TimerPage : Page
{
    private readonly MainViewModel _viewModel;
    private Storyboard? _breathingStoryboard;
    private Storyboard? _cameraBreathingStoryboard;
    private Storyboard? _pausedPulseStoryboard;

    public TimerPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsFocusCompleted):
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewModel.IsFocusCompleted) StartBreathingAnimation();
                    else StopBreathingAnimation();
                });
                break;
            case nameof(MainViewModel.IsCameraAlertActive):
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewModel.IsCameraAlertActive) StartCameraBreathingAnimation();
                    else StopCameraBreathingAnimation();
                });
                break;
            case nameof(MainViewModel.CurrentStatus):
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewModel.CurrentStatus == Models.TimerMode.Paused)
                        StartPausedPulseAnimation();
                    else
                        StopPausedPulseAnimation();
                });
                break;
        }
    }

    private void StartBreathingAnimation()
    {
        if (!_viewModel.AppSettings.AnimationEnabled) return;
        StopBreathingAnimation();

        var anim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(3)
        };
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromPercent(0.5)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        Storyboard.SetTarget(anim, TimerTextBlock);
        Storyboard.SetTargetProperty(anim, new PropertyPath(TextBlock.OpacityProperty));

        _breathingStoryboard = new Storyboard();
        _breathingStoryboard.Children.Add(anim);
        _breathingStoryboard.Begin();
    }

    private void StopBreathingAnimation()
    {
        _breathingStoryboard?.Stop();
        _breathingStoryboard = null;
        TimerTextBlock.Opacity = 1.0;
    }

    private void StartCameraBreathingAnimation()
    {
        if (!_viewModel.AppSettings.AnimationEnabled) return;
        StopCameraBreathingAnimation();

        var anim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(2)
        };
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0.3, KeyTime.FromPercent(0.5)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        Storyboard.SetTarget(anim, CameraAlertDot);
        Storyboard.SetTargetProperty(anim, new PropertyPath(System.Windows.Shapes.Ellipse.OpacityProperty));

        _cameraBreathingStoryboard = new Storyboard();
        _cameraBreathingStoryboard.Children.Add(anim);
        _cameraBreathingStoryboard.Begin();
    }

    private void StopCameraBreathingAnimation()
    {
        _cameraBreathingStoryboard?.Stop();
        _cameraBreathingStoryboard = null;
    }

    private void StartPausedPulseAnimation()
    {
        if (!_viewModel.AppSettings.AnimationEnabled) return;
        StopPausedPulseAnimation();

        var scaleX = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(4)
        };
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromPercent(0.5)));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        var scaleY = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromSeconds(4)
        };
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromPercent(0.5)));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

        Storyboard.SetTarget(scaleX, TimerTextBlock);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        Storyboard.SetTarget(scaleY, TimerTextBlock);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

        _pausedPulseStoryboard = new Storyboard();
        _pausedPulseStoryboard.Children.Add(scaleX);
        _pausedPulseStoryboard.Children.Add(scaleY);
        _pausedPulseStoryboard.Begin();
    }

    private void StopPausedPulseAnimation()
    {
        _pausedPulseStoryboard?.Stop();
        _pausedPulseStoryboard = null;
    }

    private void StartFocus_Click(object sender, RoutedEventArgs e) => _viewModel.StartFocus();
    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.PauseFocus();
    private void Resume_Click(object sender, RoutedEventArgs e) => _viewModel.ResumeFocus();
    private void Reset_Click(object sender, RoutedEventArgs e) => _viewModel.ResetFocus();
    private void StartShortBreak_Click(object sender, RoutedEventArgs e) => _viewModel.StartBreak(false);
    private void StartLongBreak_Click(object sender, RoutedEventArgs e) => _viewModel.StartBreak(true);
    private void SkipBreak_Click(object sender, RoutedEventArgs e) => _viewModel.SkipBreak();
    private void EndBreak_Click(object sender, RoutedEventArgs e) => _viewModel.EndBreak();
    private void StopCamera_Click(object sender, RoutedEventArgs e) => _viewModel.StopCameraAlert();
    private void AdjustTimeUp_Click(object sender, RoutedEventArgs e) => _viewModel.AdjustWorkMinutes(5);
    private void AdjustTimeDown_Click(object sender, RoutedEventArgs e) => _viewModel.AdjustWorkMinutes(-5);

    private void TaskName_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.CurrentStatus == Models.TimerMode.Idle)
        {
            RequestTasksPage?.Invoke();
        }
    }

    private void StatsSummary_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RequestStatsPage?.Invoke();
    }

    public event Action? RequestTasksPage;
    public event Action? RequestStatsPage;
}
```

- [ ] **Step 4: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功

- [ ] **Step 5: 提交**

```bash
git add .
git commit -m "feat: add TimerPage with WPF-UI controls and animations"
```

---

### Task 5: 创建 TasksPage

**Files:**
- Create: `LumenPomodoro/Views/Pages/TasksPage.xaml`
- Create: `LumenPomodoro/Views/Pages/TasksPage.xaml.cs`

- [ ] **Step 1: 创建 TasksPage.xaml**

```xml
<Page x:Class="LumenPomodoro.Views.Pages.TasksPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="TasksPage">

    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="任务管理" Style="{StaticResource PageTitle}" Margin="0,0,0,17" />

        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding Tasks}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <ui:Card Margin="0,0,0,8" Padding="16,12">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>

                                <Rectangle Grid.Column="0" Width="10" Height="10" Fill="{Binding Color}"
                                           RadiusX="5" RadiusY="5" Margin="0,0,12,0" VerticalAlignment="Center" />

                                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                    <TextBlock Text="{Binding Name}" FontSize="14"
                                               Foreground="{DynamicResource TextFillColorPrimaryBrush}" />
                                    <TextBlock Text="{Binding Category}" FontSize="12"
                                               Foreground="{DynamicResource TextFillColorTertiaryBrush}" />
                                </StackPanel>

                                <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                                    <ui:Button Appearance="Transparent" Click="EditTask_Click" Tag="{Binding Id}" FontSize="12">
                                        编辑
                                    </ui:Button>
                                    <ui:Button Appearance="Danger" Click="DeleteTask_Click" Tag="{Binding Id}" FontSize="12">
                                        删除
                                    </ui:Button>
                                </StackPanel>
                            </Grid>
                        </ui:Card>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <ui:Card Grid.Row="2" Margin="0,17,0,0" Padding="16">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <ui:TextBox Grid.Column="0" PlaceholderText="任务名称"
                            Text="{Binding NewTaskName, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,8,0" />
                <ui:ComboBox Grid.Column="1" ItemsSource="{Binding Categories}" SelectedItem="{Binding SelectedCategory}"
                             MinWidth="80" Margin="0,0,8,0" />
                <ui:Button Grid.Column="2" Appearance="Primary" Click="AddTask_Click">
                    添加
                </ui:Button>
            </Grid>
        </ui:Card>
    </Grid>
</Page>
```

- [ ] **Step 2: 创建 TasksPage.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class TasksPage : Page
{
    private readonly TasksViewModel _viewModel;

    public TasksPage(TasksViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void Refresh()
    {
        _viewModel.LoadTasks();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddTask();
    }

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string taskId) return;
        var task = _viewModel.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        var input = Microsoft.VisualBasic.Interaction.InputBox("编辑任务名称", "编辑", task.Name);
        if (!string.IsNullOrWhiteSpace(input))
        {
            _viewModel.FinishEdit(taskId, input);
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string taskId) return;

        var result = MessageBox.Show("确定删除此任务？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteTask(taskId);
        }
    }
}
```

- [ ] **Step 3: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "feat: add TasksPage with WPF-UI controls"
```

---

### Task 6: 创建 StatsPage

**Files:**
- Create: `LumenPomodoro/Views/Pages/StatsPage.xaml`
- Create: `LumenPomodoro/Views/Pages/StatsPage.xaml.cs`

- [ ] **Step 1: 创建 StatsPage.xaml**

```xml
<Page x:Class="LumenPomodoro.Views.Pages.StatsPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="StatsPage">

    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="今日统计" Style="{StaticResource PageTitle}" Margin="0,0,0,17" />

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,17">
            <ui:Card Margin="0,0,12,0" Width="200" Padding="24">
                <StackPanel HorizontalAlignment="Center">
                    <TextBlock Text="{Binding CompletedPomodoros}" Style="{StaticResource StatNumber}"
                               Foreground="{DynamicResource AccentFillColorDefaultBrush}" />
                    <TextBlock Text="完成番茄钟" FontSize="14"
                               Foreground="{DynamicResource TextFillColorTertiaryBrush}" HorizontalAlignment="Center" />
                </StackPanel>
            </ui:Card>
            <ui:Card Width="200" Padding="24">
                <StackPanel HorizontalAlignment="Center">
                    <TextBlock Text="{Binding TotalFocusMinutes}" Style="{StaticResource StatNumber}"
                               Foreground="{DynamicResource SystemFillColorSuccessBrush}" />
                    <TextBlock Text="专注时长(分)" FontSize="14"
                               Foreground="{DynamicResource TextFillColorTertiaryBrush}" HorizontalAlignment="Center" />
                </StackPanel>
            </ui:Card>
        </StackPanel>

        <TextBlock Grid.Row="2" Text="按任务分布" Style="{StaticResource SectionTitle}" />

        <ScrollViewer Grid.Row="3" VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding TaskStats}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto" />
                                <ColumnDefinition Width="*" />
                                <ColumnDefinition Width="Auto" />
                            </Grid.ColumnDefinitions>

                            <Rectangle Grid.Column="0" Width="10" Height="10" Fill="{Binding Color}"
                                       RadiusX="5" RadiusY="5" Margin="0,0,12,0" VerticalAlignment="Center" />
                            <TextBlock Grid.Column="1" Text="{Binding TaskName}" FontSize="14"
                                       Foreground="{DynamicResource TextFillColorPrimaryBrush}" VerticalAlignment="Center" />
                            <TextBlock Grid.Column="2" FontSize="14" VerticalAlignment="Center"
                                       Foreground="{DynamicResource TextFillColorSecondaryBrush}">
                                <Run Text="{Binding Count}" />
                                <Run Text=" 轮" />
                            </TextBlock>
                        </Grid>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </Grid>
</Page>
```

- [ ] **Step 2: 创建 StatsPage.xaml.cs**

```csharp
using System.Windows.Controls;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class StatsPage : Page
{
    private readonly StatsViewModel _viewModel;

    public StatsPage(StatsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void Refresh()
    {
        _viewModel.Refresh();
    }
}
```

- [ ] **Step 3: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "feat: add StatsPage with WPF-UI controls"
```

---

### Task 7: 创建 SettingsPage

**Files:**
- Create: `LumenPomodoro/Views/Pages/SettingsPage.xaml`
- Create: `LumenPomodoro/Views/Pages/SettingsPage.xaml.cs`

- [ ] **Step 1: 创建 SettingsPage.xaml**

```xml
<Page x:Class="LumenPomodoro.Views.Pages.SettingsPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      xmlns:models="clr-namespace:LumenPomodoro.Models"
      Title="SettingsPage">

    <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
        <StackPanel Margin="24" DataContext="{Binding SettingsVM}">
            <TextBlock Text="设置" Style="{StaticResource PageTitle}" Margin="0,0,0,17" />

            <TextBlock Text="计时" Style="{StaticResource SectionTitle}" />
            <Grid Margin="0,0,0,12">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="专注时间（分钟）" Style="{StaticResource SettingLabel}" />
                <ui:TextBox Grid.Column="1" Text="{Binding WorkMinutes, UpdateSourceTrigger=PropertyChanged}" Width="70" />
            </Grid>
            <Grid Margin="0,0,0,12">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="短休息（分钟）" Style="{StaticResource SettingLabel}" />
                <ui:TextBox Grid.Column="1" Text="{Binding ShortBreakMinutes, UpdateSourceTrigger=PropertyChanged}" Width="70" />
            </Grid>
            <Grid Margin="0,0,0,12">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="长休息（分钟）" Style="{StaticResource SettingLabel}" />
                <ui:TextBox Grid.Column="1" Text="{Binding LongBreakMinutes, UpdateSourceTrigger=PropertyChanged}" Width="70" />
            </Grid>

            <TextBlock Text="摄像头" Style="{StaticResource SectionTitle}" Margin="0,16,0,0" />
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="启用摄像头提醒" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding CameraAlertEnabled}" />
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="提醒模式" Style="{StaticResource SettingLabel}" />
                <ui:ComboBox Grid.Column="1" SelectedValue="{Binding CameraAlertMode}" SelectedValuePath="Tag" MinWidth="100">
                    <ComboBoxItem Content="固定时长" Tag="{x:Static models:CameraAlertMode.FixedDuration}" />
                    <ComboBoxItem Content="直到确认" Tag="{x:Static models:CameraAlertMode.UntilConfirm}" />
                    <ComboBoxItem Content="跟随休息" Tag="{x:Static models:CameraAlertMode.FollowBreak}" />
                </ui:ComboBox>
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="固定亮灯时长（秒）" Style="{StaticResource SettingLabel}" />
                <ui:TextBox Grid.Column="1" Text="{Binding CameraFixedOnSeconds, UpdateSourceTrigger=PropertyChanged}" Width="70" />
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="休息期间亮灯" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding CameraFollowBreakEnabled}" />
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="摄像头选择" Style="{StaticResource SettingLabel}" />
                <ui:ComboBox Grid.Column="1" ItemsSource="{Binding AvailableCameras}" SelectedIndex="{Binding SelectedCameraIndex}" MinWidth="120" />
            </Grid>
            <ui:Button Appearance="Transparent" Click="TestCamera_Click" Padding="10,6" FontSize="12" Margin="0,0,0,8">
                测试摄像头
            </ui:Button>

            <TextBlock Text="提醒" Style="{StaticResource SectionTitle}" Margin="0,16,0,0" />
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="声音提醒" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding SoundEnabled}" />
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="弹窗提醒" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding PopupEnabled}" />
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="系统通知" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding SystemNotificationEnabled}" />
            </Grid>

            <TextBlock Text="外观" Style="{StaticResource SectionTitle}" Margin="0,16,0,0" />
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="主题" Style="{StaticResource SettingLabel}" />
                <ui:ComboBox Grid.Column="1" SelectedValue="{Binding Theme}" SelectedValuePath="Tag" MinWidth="100">
                    <ComboBoxItem Content="跟随系统" Tag="system" />
                    <ComboBoxItem Content="浅色" Tag="light" />
                    <ComboBoxItem Content="深色" Tag="dark" />
                </ui:ComboBox>
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="动画效果" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding AnimationEnabled}" />
            </Grid>

            <TextBlock Text="系统" Style="{StaticResource SectionTitle}" Margin="0,16,0,0" />
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="托盘运行" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding TrayEnabled}" />
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="关闭时最小化到托盘" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding CloseToTray}" />
            </Grid>
            <Grid Margin="0,0,0,10">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="开机自启" Style="{StaticResource SettingLabel}" />
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding AutoStartEnabled}" />
            </Grid>

            <ui:Button Appearance="Primary" Click="Save_Click" Padding="22,11" HorizontalAlignment="Right" Margin="0,17,0,0">
                保存
            </ui:Button>
        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 2: 创建 SettingsPage.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel settingsVM)
    {
        InitializeComponent();
        DataContext = this;
        SettingsVM = settingsVM;
    }

    public SettingsViewModel SettingsVM { get; }

    private void TestCamera_Click(object sender, RoutedEventArgs e)
    {
        SettingsVM.TestCameraAlert();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SettingsVM.SaveSettings();
        SettingsSaved?.Invoke();
    }

    public event Action? SettingsSaved;
}
```

- [ ] **Step 3: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "feat: add SettingsPage with WPF-UI controls"
```

---

### Task 8: 重写 MainWindow 为 FluentWindow + NavigationView

**Files:**
- Modify: `LumenPomodoro/Views/MainWindow.xaml`
- Modify: `LumenPomodoro/Views/MainWindow.xaml.cs`

- [ ] **Step 1: 重写 MainWindow.xaml**

```xml
<ui:FluentWindow x:Class="LumenPomodoro.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        xmlns:pages="clr-namespace:LumenPomodoro.Views.Pages"
        Title="Lumen Pomodoro"
        Width="520" Height="640"
        MinHeight="520"
        ResizeMode="CanResizeWithGrip"
        WindowStartupLocation="CenterScreen"
        ExtendsContentIntoTitleBar="True">

    <ui:FluentWindow.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/Themes/CustomStyles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </ui:FluentWindow.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:NavigationView x:Name="NavView" Grid.Row="1"
                           IsBackButtonVisible="Collapsed"
                           IsPaneToggleVisible="False"
                           PaneDisplayMode="Bottom"
                           OpenPaneLength="0"
                           SelectionChanged="NavView_SelectionChanged">

            <ui:NavigationView.MenuItems>
                <ui:NavigationViewItem Content="计时器" Icon="{ui:SymbolIcon Timer24}" Tag="Timer" />
                <ui:NavigationViewItem Content="任务" Icon="{ui:SymbolIcon TaskListSquare24}" Tag="Tasks" />
                <ui:NavigationViewItem Content="统计" Icon="{ui:SymbolIcon ChartMultiple24}" Tag="Stats" />
                <ui:NavigationViewItem Content="设置" Icon="{ui:SymbolIcon Settings24}" Tag="Settings" />
            </ui:NavigationView.MenuItems>

            <ui:NavigationView.Content>
                <Frame x:Name="ContentFrame" NavigationUIVisibility="Hidden" />
            </ui:NavigationView.Content>
        </ui:NavigationView>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 2: 重写 MainWindow.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;
using LumenPomodoro.Views.Pages;
using Wpf.Ui.Controls;

namespace LumenPomodoro.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly TrayService? _trayService;

    private TimerPage? _timerPage;
    private TasksPage? _tasksPage;
    private StatsPage? _statsPage;
    private SettingsPage? _settingsPage;

    private TasksViewModel? _tasksViewModel;
    private StatsViewModel? _statsViewModel;
    private SettingsViewModel? _settingsViewModel;

    public MainWindow()
    {
        InitializeComponent();

        var storageService = ((App)Application.Current).StorageService;
        _viewModel = new MainViewModel(storageService);
        DataContext = _viewModel;

        if (_viewModel.AppSettings.TrayEnabled)
        {
            _trayService = new TrayService(_viewModel, _viewModel.CameraService, _viewModel.StorageService);
            _trayService.AttachToWindow(this);

            _viewModel.TrayMenuNeedsUpdate += () =>
            {
                Dispatcher.BeginInvoke(() => _trayService.UpdateMenuState());
            };

            _viewModel.NotificationRequested += (title, message) =>
            {
                Dispatcher.BeginInvoke(() => _trayService.ShowNotification(title, message));
            };
        }

        Loaded += MainWindow_Loaded;

        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NavigateTo("Timer");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string tag)
    {
        switch (tag)
        {
            case "Timer":
                _timerPage ??= CreateTimerPage();
                ContentFrame.Navigate(_timerPage);
                break;
            case "Tasks":
                _tasksPage ??= CreateTasksPage();
                _tasksPage.Refresh();
                ContentFrame.Navigate(_tasksPage);
                break;
            case "Stats":
                _statsPage ??= CreateStatsPage();
                _statsPage.Refresh();
                ContentFrame.Navigate(_statsPage);
                break;
            case "Settings":
                _settingsPage ??= CreateSettingsPage();
                ContentFrame.Navigate(_settingsPage);
                break;
        }
    }

    private TimerPage CreateTimerPage()
    {
        var page = new TimerPage(_viewModel);
        page.RequestTasksPage += () => NavigateToTab("Tasks");
        page.RequestStatsPage += () => NavigateToTab("Stats");
        return page;
    }

    private TasksPage CreateTasksPage()
    {
        var storageService = ((App)Application.Current).StorageService;
        _tasksViewModel ??= new TasksViewModel(storageService);
        _tasksViewModel.TasksChanged += () =>
        {
            _viewModel.UpdateTasks(_tasksViewModel.Tasks.ToList());
        };
        return new TasksPage(_tasksViewModel);
    }

    private StatsPage CreateStatsPage()
    {
        var storageService = ((App)Application.Current).StorageService;
        _statsViewModel ??= new StatsViewModel(storageService);
        return new StatsPage(_statsViewModel);
    }

    private SettingsPage CreateSettingsPage()
    {
        var storageService = ((App)Application.Current).StorageService;
        _settingsViewModel ??= new SettingsViewModel(storageService, _viewModel.CameraService);
        var page = new SettingsPage(_settingsViewModel);
        page.SettingsSaved += () =>
        {
            _viewModel.ReloadSettings();
            _viewModel.RefreshStats();
        };
        return page;
    }

    private void NavigateToTab(string tag)
    {
        var item = NavView.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag as string == tag);
        if (item != null)
        {
            NavView.SelectedItem = item;
        }
        NavigateTo(tag);
    }

    public void HandleWake()
    {
        _viewModel.RefreshTimerOnWake();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        _trayService?.Dispose();
        base.OnClosed(e);
    }
}
```

- [ ] **Step 3: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功。如果有编译错误，根据错误信息调整（WPF-UI 的 NavigationView API 可能因版本略有不同，需要根据实际安装版本调整）。

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "feat: rewrite MainWindow as FluentWindow with NavigationView"
```

---

### Task 9: 重写 App.xaml 使用 WPF-UI 主题

**Files:**
- Modify: `LumenPomodoro/App.xaml`
- Modify: `LumenPomodoro/App.xaml.cs`

- [ ] **Step 1: 重写 App.xaml**

```xml
<ui:UiApplication x:Class="LumenPomodoro.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             StartupUri="Views/MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Light" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
            <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
        </ResourceDictionary>
    </Application.Resources>
</ui:UiApplication>
```

- [ ] **Step 2: 重写 App.xaml.cs**

```csharp
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using LumenPomodoro.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace LumenPomodoro;

public partial class App : Application
{
    public StorageService StorageService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        StorageService = new StorageService();

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        base.OnStartup(e);

        SoundService.GenerateDefaultWavFiles();

        ApplyThemeOnStartup();
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);

        try
        {
            MessageBox.Show($"发生未预期的错误：{e.Exception.Message}\n\n软件将继续运行，但部分功能可能受影响。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch { }

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);

            try
            {
                MessageBox.Show($"发生严重错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }

    private static void LogException(Exception ex)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LumenPomodoro");
            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(logDirectory, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
            Debug.WriteLine(ex);
        }
        catch
        {
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Current.Dispatcher.BeginInvoke(() =>
            {
                if (Current.MainWindow is Views.MainWindow mainWindow)
                {
                    mainWindow.HandleWake();
                }
            });
        }
    }

    private void ApplyThemeOnStartup()
    {
        var settings = StorageService.LoadSettings();
        ApplyTheme(settings.Theme);
    }

    public void ApplyTheme(string theme)
    {
        switch (theme.ToLower())
        {
            case "dark":
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            case "light":
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            default:
                var systemTheme = ApplicationThemeManager.GetSystemTheme();
                ApplicationThemeManager.Apply(systemTheme == SystemTheme.Dark
                    ? ApplicationTheme.Dark
                    : ApplicationTheme.Light);
                break;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功。如有编译错误，根据 WPF-UI 实际 API 调整（`ApplicationThemeManager` 的命名空间可能因版本不同）。

- [ ] **Step 4: 运行测试**

```bash
dotnet test LumenPomodoro.sln --configuration Release --no-build
```

Expected: 21/21 通过

- [ ] **Step 5: 提交**

```bash
git add .
git commit -m "feat: rewrite App.xaml with WPF-UI theme system"
```

---

### Task 10: 精简 MainViewModel

**Files:**
- Modify: `LumenPomodoro/ViewModels/MainViewModel.cs`

移除设置内联相关逻辑（ToggleSettings/SaveAndCloseSettings/CloseSettings/IsSettingsVisible/SettingsVM），这些现在由 SettingsPage 独立处理。同时移除 ShowFocusCompleteDialog/ShowBreakCompleteDialog 弹窗逻辑。

- [ ] **Step 1: 从 MainViewModel 中删除以下成员**

删除这些属性和方法：
- `IsSettingsVisible` 属性
- `SettingsVM` 属性
- `ToggleSettings()` 方法
- `SaveAndCloseSettings()` 方法
- `CloseSettings(bool discard)` 方法
- `ShowFocusCompleteDialog()` 方法
- `ShowBreakCompleteDialog()` 方法
- `ShouldSuggestLongBreak` 属性

- [ ] **Step 2: 修改 TimerService_TimerCompleted 方法**

将弹窗逻辑替换为纯状态设置（UI 层 TimerPage 已通过绑定自动响应状态变化）：

```csharp
private void TimerService_TimerCompleted(object? sender, TimerCompletedEventArgs e)
{
    Application.Current.Dispatcher.BeginInvoke(() =>
    {
        if (e.CompletedMode == TimerMode.Focus)
        {
            if (_currentSession != null && !_currentSession.Completed)
            {
                _currentSession.EndTime = DateTime.Now;
                _currentSession.Completed = true;
                _storageService.AddSession(_currentSession);
                TodayStats = _storageService.GetTodayStats();
                _currentSession = null;
            }

            IsFocusCompleted = true;
            IsPendingBreak = true;
            StartCameraAlert();
            PlayNotificationSound("FocusComplete");
            ShowSystemNotification("专注完成！", "该休息了！");
        }
        else if (e.CompletedMode == TimerMode.Break)
        {
            IsBreakCompleted = true;
            ForceStopCameraAlert();
            PlayNotificationSound("BreakComplete");
            ShowSystemNotification("休息完成！", "准备好开始下一轮了吗？");
        }
    });
}
```

- [ ] **Step 3: 修改 Dispose 方法**

移除 `SettingsVM?.Cleanup()` 行。

- [ ] **Step 4: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功

- [ ] **Step 5: 运行测试**

```bash
dotnet test LumenPomodoro.sln --configuration Release --no-build
```

Expected: 21/21 通过

- [ ] **Step 6: 提交**

```bash
git add .
git commit -m "refactor: simplify MainViewModel by removing dialog and settings inline logic"
```

---

### Task 11: 删除旧文件

**Files:**
- Delete: `LumenPomodoro/Views/FocusCompleteDialog.xaml`
- Delete: `LumenPomodoro/Views/FocusCompleteDialog.xaml.cs`
- Delete: `LumenPomodoro/Views/BreakCompleteDialog.xaml`
- Delete: `LumenPomodoro/Views/BreakCompleteDialog.xaml.cs`
- Delete: `LumenPomodoro/Views/SettingsWindow.xaml`
- Delete: `LumenPomodoro/Views/SettingsWindow.xaml.cs`
- Delete: `LumenPomodoro/Views/StatsWindow.xaml`
- Delete: `LumenPomodoro/Views/StatsWindow.xaml.cs`
- Delete: `LumenPomodoro/Views/TaskManagerWindow.xaml`
- Delete: `LumenPomodoro/Views/TaskManagerWindow.xaml.cs`
- Delete: `LumenPomodoro/Themes/LightTheme.xaml`
- Delete: `LumenPomodoro/Themes/DarkTheme.xaml`

- [ ] **Step 1: 删除旧窗口文件**

```bash
cd f:\EverythingProject\github\lumen-pomodoro\LumenPomodoro
del Views\FocusCompleteDialog.xaml Views\FocusCompleteDialog.xaml.cs
del Views\BreakCompleteDialog.xaml Views\BreakCompleteDialog.xaml.cs
del Views\SettingsWindow.xaml Views\SettingsWindow.xaml.cs
del Views\StatsWindow.xaml Views\StatsWindow.xaml.cs
del Views\TaskManagerWindow.xaml Views\TaskManagerWindow.xaml.cs
del Themes\LightTheme.xaml Themes\DarkTheme.xaml
```

- [ ] **Step 2: 验证构建**

```bash
dotnet build LumenPomodoro.sln --configuration Release
```

Expected: 构建成功。如果有编译错误，说明 MainViewModel 或其他文件仍引用已删除的类型，需要清理引用。

- [ ] **Step 3: 运行测试**

```bash
dotnet test LumenPomodoro.sln --configuration Release --no-build
```

Expected: 21/21 通过

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "chore: remove old dialog windows and theme files"
```

---

### Task 12: 重写 DESIGN.md

**Files:**
- Modify: `DESIGN.md`

- [ ] **Step 1: 重写 DESIGN.md 为桌面番茄钟专属设计语言**

内容应包含：
1. 产品定位与设计哲学（玻璃拟态 + Apple 极简 + Fluent Design）
2. 颜色体系（浅色/深色 Token 表）
3. 排版体系（Inter 字体 Token 表）
4. 组件规范（计时器、按钮、卡片、导航、设置项）
5. 动效规范
6. 响应式行为
7. Do's and Don'ts

参考设计文档 `docs/superpowers/specs/2026-05-08-ui-refactor-design.md` 中的第 7-9 节。

- [ ] **Step 2: 提交**

```bash
git add .
git commit -m "docs: rewrite DESIGN.md for desktop pomodoro design language"
```

---

### Task 13: 更新 dev_log.md + 最终验证

**Files:**
- Modify: `docs/dev_log.md`

- [ ] **Step 1: 在 dev_log.md 末尾添加本次重构记录**

记录内容：
- 完成时间
- 涉及模块：MainWindow, App, 所有 Page, ViewModel, Theme, DESIGN.md
- 改动摘要：引入 WPF-UI、FluentWindow + NavigationView、4 个 Page 替代 5 个窗口、Mica/Acrylic 玻璃效果、统一样式体系
- 验证结果

- [ ] **Step 2: 最终完整验证**

```bash
dotnet build LumenPomodoro.sln --configuration Release
dotnet test LumenPomodoro.sln --configuration Release --no-build
```

Expected: 构建成功，21/21 通过

- [ ] **Step 3: Release 启动验证**

```bash
cd f:\EverythingProject\github\lumen-pomodoro
Start-LumenPomodoro.cmd
```

Expected: 应用正常启动，显示 FluentWindow + 底部 Tab Bar + Mica 背景

- [ ] **Step 4: 最终提交**

```bash
git add .
git commit -m "docs: update dev_log for UI refactor completion"
```
