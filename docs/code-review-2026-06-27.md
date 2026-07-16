# Lumen Pomodoro 代码审查报告

> 审查日期：2026-06-27 | 审查人：Senior Developer

---

## 一、总体评价

**评级：B+（良好，有明确的提升空间）**

项目整体架构清晰，DI/MVVM 模式运用得当，错误处理和持久化机制考虑周全。当前代码可以支撑产品的正常运行，但在可维护性、可测试性和工程规范方面存在一些需要注意的问题。以下是详细分析和可落地的改进方案。

---

## 二、亮点（值得保持）

| # | 亮点 | 说明 |
|---|------|------|
| 1 | **DI 容器规范** | 使用 MS DI，Singleton 服务 + Transient ViewModel，生命周期管理合理 |
| 2 | **原子文件写入** | `AtomicWriteAllText` 用 `.tmp` + `File.Replace` 防止 JSON 损坏 |
| 3 | **备份恢复机制** | Settings/Tasks/Sessions 均有 `.bak` 备份 + 自动恢复 |
| 4 | **Schema 版本化** | `_schema.json` + 迁移管线，面向未来的数据升级 |
| 5 | **三层全局异常处理** | Dispatcher + AppDomain + TaskScheduler，覆盖所有异常路径 |
| 6 | **唤醒补偿** | TimerService 的 `CorrectAfterWake` 处理系统休眠后的计时偏差 |
| 7 | **Controller 分离** | TimerController/CameraAlertController 将复杂业务逻辑从 ViewModel 中拆出 |
| 8 | **隐私合规** | 首次摄像头使用弹隐私声明，摄像头不拍照不录像不上传 |

---

## 三、需改进的问题（按优先级排列）

### 🔴 P0 — 架构债务

#### 3.1 无 ViewModelBase，大量重复代码
**位置**：所有 ViewModel 文件

每个 ViewModel 都独立实现 `INotifyPropertyChanged` + `OnPropertyChanged` 模板：
```csharp
// 在 MainViewModel、SettingsViewModel、StatsViewModel、TasksViewModel 中各出现一次
public event PropertyChangedEventHandler? PropertyChanged;
protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
{
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

**改进方案**：引入 `CommunityToolkit.Mvvm` NuGet 包（微软官方，轻量级），使用 `[ObservableProperty]` 源生成器。
```csharp
// 改进后
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private TimerMode _currentStatus = TimerMode.Idle;
    // 自动生成 CurrentStatus 属性和通知，减少 90% 样板代码
}
```

#### 3.2 MainViewModel 过于臃肿（上帝类）
**位置**：`MainViewModel.cs`（698 行，50+ 属性，20+ 方法）

MainViewModel 承载了过多职责：
- 计时器管理
- 任务选择
- 摄像头提醒
- 防走神
- 评分/笔记
- 统计刷新
- 通知调度
- 窗口激活

**改进方案**：拆分为协作的多个 ViewModel：
- `TimerViewModel`：计时器显示 + 开始/暂停/恢复/重置
- `TaskSelectorViewModel`：任务选择和切换
- `SessionReviewViewModel`：评分、笔记、摘要
- `FocusGuardViewModel`：防走神状态
- `MainViewModel`：仅作协调器，组合上述子 ViewModel

#### 3.3 无 ICommand，View 代码后置直接调用 ViewModel 方法
**位置**：所有 `*.xaml.cs` 文件

```csharp
// 当前模式（TimerPage.xaml.cs）
void StartButton_Click(object sender, RoutedEventArgs e) => ViewModel.StartFocus();
```

这导致：
- XAML 无法直接绑定命令
- 按钮的 IsEnabled 状态无法通过 CanExecute 自动管理
- 无法做单元测试覆盖 UI 交互逻辑

**改进方案**：使用 `CommunityToolkit.Mvvm` 的 `[RelayCommand]`：
```csharp
[RelayCommand(CanExecute = nameof(CanStartFocus))]
private void StartFocus()
{
    // ...
}
private bool CanStartFocus() => SelectedTask != null && CurrentStatus == TimerMode.Idle;
```
XAML 中直接用 `Command="{Binding StartFocusCommand}"`。

#### 3.4 服务定位器反模式
**位置**：`App.xaml.cs:25` + 多处使用

```csharp
public static T GetRequiredService<T>() where T : notnull
    => ((App)Current).Services.GetRequiredService<T>();
```

View 代码中大量使用 `App.GetRequiredService<T>()` 绕过构造函数注入。

**改进方案**：所有依赖通过构造函数注入，移除静态访问器，或在启动时显式解析并传递。

### 🟡 P1 — 工程质量

#### 3.5 事件链路过长
```csharp
// MainViewModel 构造函数中的事件代理链
_notifications.TrayMenuNeedsUpdate += () => TrayMenuNeedsUpdate?.Invoke();
_notifications.NotificationRequested += (t, m) => NotificationRequested?.Invoke(t, m);
// ... 6 层代理
```

NotificationCoordinator → MainViewModel → View 的链路过长，且每个事件都要手动代理。

**改进方案**：使用 `MediatR` 或 `CommunityToolkit.Mvvm` 的 `WeakReferenceMessenger` 实现消息总线，替代事件链。

#### 3.6 Dispatcher 分散引用
**位置**：`CameraAlertController.cs`、`MainViewModel.cs`

```csharp
Application.Current?.Dispatcher?.BeginInvoke(() => { ... }); // 出现 10+ 次
```

**改进方案**：注入 `IDispatcherService` 接口，或使用 `CommunityToolkit.Mvvm` 提供的线程安全属性更新。

#### 3.7 主题切换未实现
**位置**：`App.xaml.cs:172-174`

```csharp
public void ApplyTheme(string theme)
{
    ApplicationThemeManager.Apply(ApplicationTheme.Dark); // 参数 theme 被忽略
}
```

主题参数未使用，且没有任何地方调用此方法实现真正的主题切换。

#### 3.8 Fire-and-Forget 缺乏生命周期管理
**位置**：`CameraAlertController.cs:224-239`

`FireAndForgetAsync` 不返回 Task，无法取消操作。当用户快速切换状态时，旧的异步摄像头操作可能仍在执行。

**改进方案**：使用 `CancellationTokenSource` + `CancellationToken` 管理异步生命周期。

### 🟢 P2 — 细节优化

#### 3.9 测试覆盖不足
- 大部分测试仅验证"方法被调用了"（`Verify(t => t.Method(), Times.Once)`）
- 缺少边界条件测试（空输入、极值、并发）
- 缺少集成测试
- 2 个测试因 DispatcherTimer 需要 UI 线程被 Skip

**改进方案**：
- 将 TimerService 的 tick 逻辑提取为可测试的纯函数
- 添加行为验证测试（不仅是交互验证）
- 使用 `xunit.runner.ui` 或自定义 SynchronizationContext 解决 UI 线程依赖

#### 3.10 StorageService GetTodayStats 每次返回新对象
```csharp
return new DailyStats
{
    CompletedPomodoros = _cachedTodayStats.CompletedPomodoros,
    // ... 手动复制每个字段
};
```

**改进方案**：给 `DailyStats` 添加 `Clone()` 方法或实现 `ICloneable`。

#### 3.11 Hardcoded 字符串
- `"摄像头提醒失败"`、`"检测到你已离开"` 等中文硬编码在代码中
- 应全部移至 `LocalizedStrings` 资源文件

#### 3.12 缺少 Nullable Reference Types
- 项目未启用全局 nullable
- 大量 `?` 后缀是手动加的，容易遗漏

---

## 四、改进路线图

### 第 1 周：基础设施升级
- [ ] 引入 `CommunityToolkit.Mvvm` NuGet 包
- [ ] 创建 `ViewModelBase` 基类（或迁移到 `ObservableObject`）
- [ ] 一个 ViewModel 试点的 `[ObservableProperty]` + `[RelayCommand]` 迁移
- [ ] 启用全局 `<Nullable>enable</Nullable>`

### 第 2 周：架构重构
- [ ] 实现消息总线替代事件代理链
- [ ] 重构 MainViewModel，拆出 TimerViewModel
- [ ] 注入 `IDispatcherService` 消除 `Application.Current.Dispatcher`
- [ ] 修复主题切换

### 第 3 周：质量加固
- [ ] 添加 `CancellationToken` 到异步方法
- [ ] 提升测试覆盖率到 60%+
- [ ] 添加行为验证测试
- [ ] Hardcoded 字符串国际化

### 第 4 周：持续改进
- [ ] 解决 DispatcherTimer 测试问题
- [ ] 添加集成测试
- [ ] 性能 profile（启动时间、内存）
- [ ] 代码规范文档

---

## 五、快速 Win（今天就能做的改进）

以下是可以在不破坏现有功能的前提下快速完成的小改进：

| # | 改进 | 工作量 | 收益 |
|---|------|--------|------|
| 1 | Settings.ApplyTheme 实现真正的主题切换 | 10 分钟 | 用户体验 |
| 2 | 提取重复的 Topmost 3 秒逻辑为工具方法 | 15 分钟 | 消除重复 |
| 3 | DailyStats 添加 Clone 方法 | 10 分钟 | 可维护性 |
| 4 | App.xaml.cs 中的 DispatcherUnhandledException 空 MessageBox 变量 | 2 分钟 | 代码整洁 |
| 5 | 添加 .editorconfig 统一代码风格 | 5 分钟 | 团队协作 |
