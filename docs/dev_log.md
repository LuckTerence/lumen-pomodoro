# 开发日志

## [2026-05-08] Bug 修复 — COM 双重释放、摄像头残留、VB Runtime 移除、Registry 失败感知

**涉及模块**: CameraService, SettingsPage, SettingsViewModel, TasksPage

**修改文件数**: 4 个

### 修复摘要

1. **CameraService COM 双重释放 [高]** — `EnumerateDevices()` 和 `EnumerateDeviceActivates()` 在 `Marshal.GetObjectForIUnknown` 将 IntPtr 转为 RCW 后，finally 块仍对同一 IntPtr 调用 `Marshal.Release()`，造成双重释放。`CaptureLoop` 中 `Marshal.ReleaseComObject(activate)` 后，finally 也重复释放同一指针。修复：不再在 finally 中释放已转交 RCW 的 IntPtr，改为将指针置 null；激活失败时才主动释放。
2. **摄像头测试后残留 [高]** — `SettingsPage` 未实现 `IDisposable`，用户测试摄像头后切换页面，`Cleanup()` 永远不被调用，摄像头继续后台运行。修复：`SettingsPage` 实现 `IDisposable`，`Unloaded` 事件中调用 `_viewModel.Cleanup()`。
3. **VB Runtime 依赖 [中]** — `TasksPage` 使用 `Microsoft.VisualBasic.Interaction.InputBox` 弹窗，引入 VB 依赖。修复：改用 WPF 原生 `Window` + `TextBox` + `Button` 实现。
4. **Registry 自启失败静默 [中]** — `UpdateAutoStart()` 操作注册表失败时只有 `Debug.WriteLine`，用户开启自启但实际无效无感知。修复：增加 `UnauthorizedAccessException` 专项处理 + `MessageBox` 明确告知用户。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-08] TimerPage 垂直居中修复 + CameraService Marshal.Release 编译错误

**涉及模块**: TimerPage, CameraService

### 修复摘要

1. **TimerPage 内容偏上不居中 [UI]** — WPF-UI `NavigationViewContentPresenter` 默认 `IsDynamicScrollViewerEnabled=true`，页面内容被 `DynamicScrollViewer` 包裹，ScrollViewer 给子元素无限高度，`VerticalAlignment="Center"` 完全失效。修复：在 `Page` 根元素添加 `ScrollViewer.CanContentScroll="False"`，NavigationViewContentPresenter 切换到无 ScrollViewer 模板，`VerticalAlignment="Center"` 正常生效。
2. **CameraService 两处编译错误 [编译]** — `Marshal.Release(Marshal.GetObjectForIUnknown(...))` 类型错误，`Marshal.Release` 需要 `IntPtr` 而非 `object`。修复：改为 `Marshal.Release(activates[i])` 直接释放指针。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。

## [2026-05-08] WPF-UI FluentWindow + NavigationView 架构迁移

**涉及模块**: MainWindow, App, TimerPage, TasksPage, StatsPage, SettingsPage, MainViewModel, TasksViewModel, StatsViewModel, SettingsViewModel, CustomStyles, DESIGN.md, TrayService

### 改动摘要

1. **引入 WPF-UI 4.3.0 库** — 实现 Mica/Acrylic 玻璃效果，替代手写半透明背景。
2. **FluentWindow + NavigationView 底部 Tab Bar** — 替代多窗口架构，单一窗口内导航切换。
3. **4 个 Page 替代 5 个独立窗口** — TimerPage/TasksPage/StatsPage/SettingsPage 替代原 MainWindow/TaskManagerWindow/StatsWindow/SettingsWindow/FocusCompleteDialog。
4. **专注完成/休息完成改为内联状态过渡** — 不再弹窗，在 TimerPage 内直接过渡。
5. **WPF-UI Fluent 控件替代手写 ControlTemplate** — 消除约 500 行重复 XAML。
6. **CustomStyles.xaml 统一管理业务特定样式** — 集中维护非 Fluent 标准的业务样式。
7. **重写 DESIGN.md** — 改为桌面番茄钟专属设计语言。
8. **新增 TasksViewModel 和 StatsViewModel** — 职责拆分，MainViewModel 不再承载任务和统计逻辑。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-08] 全项目 Bug 扫描修复 — 14 项

**涉及模块**: SettingsViewModel, MainViewModel, MainWindow, CameraService, StorageService, TaskManagerWindow, StorageServiceTests

**修改文件数**: 7 个

### P0 — 严重问题修复

1. **SettingsViewModel.Cleanup 误杀摄像头提醒** — 引入 `_cameraStartedByTest` 标记，Cleanup 仅停止由测试按钮启动的摄像头，不再无条件停止共享 CameraService。
2. **IdlePanel 与 CompletedPanel 叠加显示** — PopupEnabled=false 时专注完成不再设 CurrentStatus=Idle；XAML 中 IdlePanel 增加 DataTrigger 在 IsFocusCompleted 时隐藏，CompletedPanel 改用 MultiDataTrigger 同时检查 IsFocusCompleted 和 CurrentStatus。
3. **SelectedTask 删除后引用失效** — UpdateTasks 增加 ID 匹配检查，当前选中任务不在新列表时自动重选；移除冗余的 SaveTasks 调用（调用方已保存）。

### P1 — 高优先级修复

4. **SaveSessionsWithTransaction 改为 private** — 消除外部无锁调用风险，测试改用 AddSession 间接验证。
5. **AutoStartEnabled/Theme setter 即时副作用** — 移除 setter 中的 UpdateAutoStart/ApplyTheme 调用，延迟到 SaveSettings 时统一执行。
6. **双重托盘图标** — 移除 MainViewModel 中的 _notifyIcon 长驻实例，改为 NotificationRequested 事件委托给 TrayService；无订阅者时创建临时 NotifyIcon 用后即弃。
7. **KeepCameraActiveAsync 超时/断连不 Dispose _cameraDevice** — 超时分支增加 Dispose 调用；意外断开分支增加 Dispose 并置 null。
8. **_notifyIcon Dispose 前未设 Visible=false** — 已随 P1-6 重构消除，不再有长驻 _notifyIcon。

### P2 — 中优先级修复

9. **StartCameraAsync 旧 CTS 只 Cancel 不 Dispose** — 增加 oldCts.Dispose() 调用。
10. **ForceStopCameraAlert 异步时序** — 移除立即设 IsCameraAlertActive=false，改为由 CameraStatusCallback 在 Stop 完成后自然更新。
11. **TaskManager 编辑对话框 SelectAll 无效** — 改为 textBox.Loaded 事件中调用 SelectAll。
12. **GetTodayStats TOCTOU 三段式加锁** — 合并为单次 lock(_fileLock) 块，消除中间状态窗口。

### P3 — 低优先级修复

13. **SettingsWindow.Closing 误杀摄像头** — 随 P0-1 修复（Cleanup 已改为条件停止）。
14. **StartCameraAsync 先设 _isRunning 再初始化** — _isRunning=true 移到初始化成功后，失败路径统一在 lock 下设 false。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-07] 根目录启动入口

**涉及模块**: Start-LumenPomodoro.cmd, README

### 改动摘要

1. 新增根目录 `Start-LumenPomodoro.cmd`，双击即可启动 Release 版本应用。
2. 启动入口会优先使用已有 `LumenPomodoro.exe`，缺少构建产物时自动执行 Release 构建。
3. README 增加快速启动说明，避免用户进入深层 `bin/Release` 目录手动打开。

### 验证结果

- `dotnet build LumenPomodoro.sln --configuration Release`：通过，0 warning / 0 error。
- `dotnet test LumenPomodoro.sln --configuration Release --no-build`：通过，21/21。
- 根目录执行 `Start-LumenPomodoro.cmd`：已有 Release 产物时可启动应用进程并显示窗口。
- 临时移走 `LumenPomodoro.exe` 后再次执行 `Start-LumenPomodoro.cmd`：脚本可自动 Release 构建并启动应用窗口。

## [2026-05-07] 按 Apple DESIGN.md 重构界面语言

**涉及模块**: DESIGN.md, LightTheme, DarkTheme, MainWindow, SettingsWindow

### 改动摘要

1. 引入根目录 `DESIGN.md` 作为界面设计规范来源。
2. 主题色切换为 Apple 风格单一 Action Blue，并同步近黑、羊皮纸、Pearl、Hairline 等表面色。
3. 主窗口与设置页主按钮改为胶囊按钮，次级按钮改为紧凑 utility 样式。
4. 去除控件阴影和进度条装饰渐变，保留更克制的 Apple 式界面层级。
5. 放大主窗口与设置页尺寸、增加留白，降低控件密度。

### 验证结果

- `dotnet build LumenPomodoro.sln --configuration Release`：通过，0 warning / 0 error。
- `dotnet test LumenPomodoro.sln --configuration Release --no-build`：通过，21/21。
- Release 启动验证：主窗口正常显示，未写入启动错误日志。

## [2026-05-07] 图标化按钮与品牌栏收口

**涉及模块**: MainWindow, SettingsWindow

### 改动摘要

1. 主窗口增加轻量品牌栏，使用 Segoe MDL2 图标标识应用。
2. 主窗口主要操作改为图标+文字：开始、暂停、继续、重置、短休息、长休息、跳过、结束休息、关闭提醒。
3. 设置页标题、关闭、测试摄像头、保存、取消补齐图标，统一交互识别。
4. 任务管理按钮更换为更贴近“列表/任务”的图标。

### 验证结果

- `dotnet build LumenPomodoro.sln --configuration Release`：通过，0 warning / 0 error。
- `dotnet test LumenPomodoro.sln --configuration Release --no-build`：通过，21/21。

## [2026-05-07] 深色主题 UI 与窗口拖动修复

**涉及模块**: MainWindow, SettingsWindow

### 改动摘要

1. 为主窗口按钮、任务下拉框补齐深色模板，修复系统默认控件造成的黑白块和黑字问题。
2. 为设置页按钮、输入框、下拉框、开关、滚动条补齐深色模板，修复深色主题下的白底控件和浅色滚动条。
3. 设置页加入滚动容器，避免底部设置项在固定窗口高度下不可达。
4. 主窗口和设置页支持在非控件区域拖动窗口。

### 验证结果

- `dotnet build LumenPomodoro.sln --configuration Release`：通过，0 warning / 0 error。
- `dotnet test LumenPomodoro.sln --configuration Release --no-build`：通过，21/21。
- Release 启动验证：主窗口正常显示，未写入启动错误日志。

## [2026-05-07] 启动失败修复

**涉及模块**: App, MainWindow

### 改动摘要

1. 修复 `App.OnStartup` 初始化顺序，确保 `StorageService` 在 `StartupUri` 创建主窗口前可用。
2. 修复 `MainWindow.xaml` 中非法动画时间 `0.8s`，改为 WPF 支持的 `0:0:0.8`。
3. 调整 `QuadraticEase` 资源声明顺序，避免 `StaticResource` 前向引用风险。
4. 为全局异常处理增加 `%AppData%\LumenPomodoro\error.log` 记录，后续启动异常可直接定位完整堆栈。

### 验证结果

- `dotnet build LumenPomodoro.sln --configuration Release`：通过，0 warning / 0 error。
- `dotnet test LumenPomodoro.sln --configuration Release --no-build`：通过，21/21。
- Release 启动验证：主窗口句柄存在，窗口标题为 `Lumen Pomodoro`。

## [2026-05-07] 上线前复扫修复

**涉及模块**: SettingsWindow, MainViewModel, StorageService, StorageServiceTests, README

### 改动摘要

1. 修复设置页开关控件使用 `DoubleAnimation` 动画 `Margin` 的运行时风险，改为匹配 `Thickness` 属性的 `ThicknessAnimation`。
2. 修复 `CameraFollowBreakEnabled` 设置只保存但未参与休息亮灯判断的问题；跟随休息模式下会同时尊重总开关与“休息期间亮灯”开关。
3. 为 `StorageService` 增加可选数据目录参数，测试改用临时目录并在结束后清理，避免测试读写用户正式 `%AppData%\LumenPomodoro` 数据。
4. 移除 xUnit 对 `DateTime` 值类型的无效 `Assert.NotNull`，消除测试分析器警告。
5. 更新 README 项目结构与当前阶段描述，避免文档与真实工程结构不一致。

### 验证结果

- `dotnet build LumenPomodoro.sln --configuration Release`：通过，0 warning / 0 error。
- `dotnet test LumenPomodoro.sln --configuration Release --no-build`：通过，21/21。

## [2026-05-07] 全项目 Bug/性能修复 — 30+ 问题

**涉及模块**: TimerService, StorageService, CameraService, SoundService, TrayService, MainViewModel, SettingsViewModel, App, MainWindow, TaskManagerWindow, SettingsWindow, FocusCompleteDialog, BreakCompleteDialog, TimerServiceTests, TaskItem/TaskCategories

**修改文件数**: 15 个

### P0 — 严重问题修复

1. **TimerService 线程安全** — StartFocus/StartBreak/Timer_Elapsed 事件在锁外触发时读取的字段可能已被修改。修复：在锁内快照状态，锁外触发事件。
2. **TimerService._remainingSeconds 负数** — _remainingSeconds-- 后才检查 <=0，Timer 快速连续触发时可能变负。修复：Math.Max(0, _remainingSeconds - 1)。
3. **TimerService.Pause/Resume 锁内触发事件** — 事件订阅者回调可能导致死锁。修复：移至锁外。
4. **StorageService.SaveSessions 无锁** — 直接 File.WriteAllText 无 _fileLock 保护，可导致 JSON 损坏。修复：加 lock(_fileLock)。
5. **StorageService.LoadSessions 读无锁** — 读操作未锁保护，与写操作并发时可能读到半写数据。修复：所有公共读方法加锁。
6. **StorageService.GetTodayStats 缓存线程不安全** — _cachedTodayStats/_cacheDate 跨线程无同步。修复：加锁。
7. **CameraService CTS 释放竞态** — 旧 CTS Cancel 后立即 Dispose，可能在 token 使用中。修复：先创建新 CTS，再取消旧的。
8. **TaskManagerWindow 占位符绑定反向** — BooleanToVisibilityConverter + Text 绑定逻辑完全反转。修复：改用 DataTrigger + 默认 Collapsed。
9. **BreakCompleteDialog 返回值被忽略** — ShowDialog() 返回值未检查，ShouldStartNext 从未被读取。修复：在 ShowBreakCompleteDialog 中检查并调用 StartFocus()。

### P1 — 高优先级修复

10. **TimerService/SoundService 未实现 IDisposable** — 有 Dispose() 但未声明接口。修复：添加 IDisposable。
11. **TimerService.Dispose 不清事件** — 事件持有 MainViewModel 强引用阻止 GC。修复：Dispose 时置 null。
12. **MediaFoundationCamera._internalToken 未释放** — Start() 创建 CTS，Stop() 从不 Dispose。修复：添加 IDisposable。
13. **CameraService COM 对象泄漏** — CaptureLoop 错误路径 mediaSource 可能不释放；WMI ManagementBaseObject 未 Dispose。
14. **CameraService._isRunning 写入不一致** — KeepCameraActiveAsync 无锁写入。修复：统一在 lock 下修改。
15. **全局空 catch 块** — SoundService/CameraService/MainViewModel/SettingsViewModel 中空 catch 静默吞掉异常。修复：Debug.WriteLine。
16. **Fire-and-forget 异步调用异常丢失** — 丢弃 Task 异常完全忽略。修复：统一 FireAndForget 包装。
17. **MainViewModel.Dispose 中 fire-and-forget** — 改用同步 GetAwaiter().GetResult()。
18. **MainViewModel 未取消订阅 TimerService 事件** — 修复：Dispose 时取消订阅。
19. **SoundService.Volume 属性无效** — SoundPlayer 不支持音量控制。修复：添加文档注释。

### P2 — 中优先级修复

20. **SettingsWindow 数值 TextBox 缺少 UpdateSourceTrigger** — 修复：添加 UpdateSourceTrigger=PropertyChanged。
21. **CaptureLoop 30fps 不必要** — 改为 WaitOne(1000) 即 1fps。
22. **InitializeCameraDevice Thread.Sleep(500) 阻塞** — 改为 async Task + await Task.Delay。
23. **App.ApplyTheme 先删后加导致空窗** — 先添加新主题再移除旧主题。
24. **多 StorageService 实例** — App 提供共享实例。
25. **MainWindow 多次调用 LoadSettings** — 使用 ViewModel 的 AppSettings。

### P3 — 低优先级修复

26. **Property setter 无相等性检查** — 添加 if (_field != value) 检查。
27. **SettingsViewModel 未实现 IDisposable** — 添加接口。
28. **TrayService 事件处理器未取消订阅** — Dispose 中逐一取消。
29. **FocusCompleteDialog.SetLongBreakSuggestion 空引用** — 添加 IsInitialized 检查。
30. **GetCategoryColor 重复定义** — 提取到 TaskCategories.GetCategoryColor()。
31. **TimerComplete 测试 61 秒** — 改为 2.5 秒验证。
32. **_theme 字段 CS8618** — 声明默认值 "system"。

**测试结果**: 18/18 通过 (2.2s)

## [2026-05-07] DESIGN.md 全面对齐修复

**涉及模块**: LightTheme, DarkTheme, MainWindow, SettingsWindow, StatsWindow, TaskManagerWindow, FocusCompleteDialog, BreakCompleteDialog, LumenPomodoro.csproj

**修改文件数**: 9 个 + Fonts 目录

### 改动摘要

1. **嵌入 Inter 字体** — 下载 Inter Light/Regular/SemiBold TTF 到 `Fonts/`，csproj 添加 Resource 引用，两个 Theme 定义 `InterLight`/`InterRegular`/`InterSemiBold` FontFamily 资源。
2. **颜色 Token 对齐 DESIGN.md** — SecondaryTextColor → #7a7a7a (ink-muted-48)，TertiaryTextColor → #cccccc (body-muted)，BorderColor → #e0e0e0 (hairline)，ControlBackgroundColor → #d2d2d7 (surface-chip-translucent)。新增 SurfacePearlColor/SurfacePearlBrush。
3. **CornerRadius 统一到 `{rounded.*}` scale** — GlassPanel 统一 18 (lg)，PrimaryButton/CircleButton 9999 (pill/full)，ComboBox/StatCard 18 (lg)，TextBox 9999 (pill)，ScrollBar 5 (xs)。
4. **移除所有 DropShadowEffect** — StatsWindow、TaskManagerWindow、FocusCompleteDialog、BreakCompleteDialog 共 4 处阴影全部移除。
5. **按钮交互改为 scale(0.95)** — PrimaryButton/SecondaryButton/CircleButton 的 IsPressed 触发器从 Opacity 0.86 改为 ScaleTransform 动画 (Storyboard EnterActions/ExitActions)。
6. **SecondaryButton 重做为 button-secondary-pill** — 透明背景、PrimaryBrush 前景、1px PrimaryBrush 边框、pill 圆角、17px/400。
7. **Font Weight 修正** — 消除所有 weight 500 (Medium)，统计数字 Bold→SemiBold，对话框标题 Bold→SemiBold。
8. **Spacing 对齐 token** — 16→17 (md)，36→32 (xl)，10→8 (xs)。
9. **Timer FontFamily** — Segoe UI Light → InterLight。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-08] P1 动效补齐 + P2 健壮性与体验打磨

**涉及模块**: MainWindow, MainWindow.xaml.cs, TimerService, MainViewModel, StorageService, StatsWindow, App

**修改文件数**: 7 个

### 改动摘要

**P1 动效：**
1. **专注完成呼吸动画** — TimerText Opacity 3s 呼吸循环（1.0→0.5→1.0），由 `IsFocusCompleted` 触发。
2. **摄像头提醒中呼吸动画** — CameraAlertDot Opacity 2s 呼吸循环（1.0→0.3→1.0），由 `IsCameraAlertActive` 触发。
3. **暂停状态动效** — TimerText ScaleTransform 4s 微缩放（1.0→0.97→1.0），由 `CurrentStatus==Paused` 触发。
4. **启动淡入+上浮** — 已有 `MainWindow_IsVisibleChanged` 中的 Opacity/TranslateTransform 动画。
5. **休息切换平滑过渡** — 已有状态切换时的 Opacity 动画。
6. **托盘恢复淡入** — 同上 `IsVisibleChanged` 逻辑。

**P2 健壮性：**
7. **睡眠恢复时间修正** — TimerService 新增 `_lastTickTime` 字段和 `CorrectAfterWake()` 方法，唤醒后按实际流逝时间修正倒计时；App.xaml.cs 通过 `SystemEvents.PowerModeChanged` 监听唤醒事件，调用 `MainWindow.HandleWake()`。
8. **摄像头自动释放后 UI 提示** — `CameraErrorCallback` 中检测"保护释放"关键词，设置 `IsFocusCompleted = true` 使主界面显示完成状态。

**P3 体验优化：**
9. **任务颜色标签 UI 展示** — 主界面任务名前增加颜色圆点（Ellipse）；统计页面每个任务卡片增加颜色圆点（`GetTaskColor` 方法）。
10. **首次启动预置默认考研分类任务** — `StorageService.LoadTasks()` 在文件不存在或为空时调用 `GetDefaultTasks()` 并立即 `SaveTasks()` 写入文件。
11. **主界面快速调整专注时长** — Idle 状态下"开始专注"按钮上方增加 −/+ 调整控件，每次 ±5 分钟，范围 1-120。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-07] P0 功能闭环

**涉及模块**: SettingsViewModel, MainWindow, MainWindow.xaml.cs

**修改文件数**: 3 个

### 改动摘要

1. **开机自启路径修复** — `UpdateAutoStart()` 从 `System.Windows.Forms.Application.ExecutablePath` 改为 `Environment.ProcessPath`，避免对 Windows Forms 的依赖；注册表值用引号包裹路径以处理空格；增加 key null 检查。
2. **内联设置补齐摄像头配置** — 在 MainWindow 内联设置面板的"计时"和"提醒"之间新增"摄像头"分组，包含：启用摄像头提醒开关、提醒模式选择（固定时长/直到确认/跟随休息）、固定亮灯时长输入、休息期间亮灯开关、摄像头选择下拉框、测试摄像头按钮。
3. **设置面板高度调整** — 展开设置时窗口高度从 680 调整为 740，容纳新增的摄像头配置项。
4. **测试摄像头事件处理** — MainWindow.xaml.cs 新增 `TestCameraButton_Click`，委托给 `SettingsVM.TestCameraAlert()`。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-07] 设置内联化 + UI 视觉优化

**涉及模块**: MainViewModel, MainWindow, MainWindow.xaml.cs

**修改文件数**: 3 个

### 改动摘要

1. **设置内联** — 点击齿轮按钮不再弹出 SettingsWindow，而是在 MainWindow 内切换 TimerView / SettingsView。窗口高度从 600 动态扩展到 720 以容纳设置内容。
2. **MainViewModel 新增** — `IsSettingsVisible` (INPC), `SettingsVM` (SettingsViewModel 实例), `ToggleSettings()`, `SaveAndCloseSettings()`, `CloseSettings(bool discard)`。SettingsVM 在展开时创建、关闭时 Dispose，与 MainViewModel 通过 StorageService 解耦。
3. **精简设置项** — 内联面板展示：计时(3项)、提醒(3项)、外观(2项)、系统(3项)。摄像头高级选项暂不在内联面板中暴露。
4. **UI 元素统一** — Timer 区的暂停/重置/跳过等次级按钮统一使用 SmallSecondaryButton 样式（14px, pill, 16x8 padding），而非全尺寸 SecondaryButton。ManageTasksButton 改用 CircleButton 样式。
5. **设置面板样式** — 新增 SettingsSectionTitle、SettingsLabel、SettingsToggle、SettingsInput、SettingsCombo、SmallSecondaryButton 共 6 个样式资源。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-07] 摄像头错误暴露 + 移除默认托盘行为

**涉及模块**: CameraService, Settings, MainViewModel, MainWindow

**修改文件数**: 4 个

### 改动摘要

1. **摄像头错误码暴露** — `MediaFoundationCamera.CaptureLoop` 之前在 COM 调用失败时静默 return（`if (hr < 0) return;`），用户只看到"摄像头意外断开"的误导信息。新增 `HResultToString` 方法将 HRESULT 翻译为中文可读描述（E_ACCESSDENIED/E_NOTFOUND 等），通过 error 回调逐级传递到 UI 弹窗。
2. **MediaFoundationCamera 接受 error 回调** — 构造函数新增 `Action<string>? errorCallback` 参数，`CameraService.InitializeCameraDevice` 创建实例时传入 `_errorCallback`，使 CaptureLoop 中的错误能正确传递到 `MainViewModel.CameraErrorCallback`。
3. **移除默认托盘行为** — `Settings.TrayEnabled` 和 `Settings.CloseToTray` 默认值从 `true` 改为 `false`。关闭窗口直接退出应用，不再隐藏到托盘。
4. **条件化 TrayService** — `MainWindow` 构造函数仅在 `TrayEnabled=true` 时创建 `TrayService` 实例和订阅托盘菜单更新事件。`MainViewModel` 的 2 秒托盘更新定时器同理条件化启动，避免无意义的定时器开销。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。

## [2026-05-07] 极简 Apple 风格 MainWindow 完全重构

**涉及模块**: MainWindow, MainWindow.xaml.cs, StorageServiceTests

**修改文件数**: 3 个

### 改动摘要

1. **Hero 时间显示** — 剩余时间从 76px 提升到 80px InterLight，成为视觉核心。品牌栏（Lumen 图标+文字）完全移除。
2. **极简布局** — 窗口从 460x600 缩减至 420x520。布局改为三行：窗口控制（右上角）→ 主内容区（垂直居中）→ 极简页脚。
3. **每状态精简按钮** — Idle: 开始专注 (Primary); Focus: 暂停+重置; Paused: 继续+重置; Break: 结束休息。次级操作（长休息/跳过）改为 TextLink 纯文字按钮。
4. **极简页脚** — 一行文字 `今日 3 · 专注 75分` + 齿轮设置按钮，取代原来的双数字统计卡+设置按钮。
5. **任务选择器条件显示** — 仅 Idle 状态可见，专注/休息时自动隐藏。
6. **窗口控制重做** — 最小化/关闭按钮从 CircleButton 改为 Path 几何图形绘制的极简图标，无背景。
7. **新增 TextLinkButton 样式** — 无边框无背景纯文字按钮，hover 时轻微底色。
8. **修复预存测试** — `Settings_Model_ShouldHaveDefaultValues` 中 `TrayEnabled`/`CloseToTray` 断言从 True 改为 False，匹配实际默认值。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。

## [2026-05-07] MainWindow 第二次根本性重构 — Apple Timer

**涉及模块**: MainWindow, MainWindow.xaml.cs

**修改文件数**: 2 个

### 改动摘要

1. **去掉 GlassPanel 边框** — Window Background 直接 Transparent，内层 Border CornerRadius 从 18 降到 12，无 border line，消除"卡片感"。
2. **时间 96px InterLight** — 从 80px 提升到 96px，视觉核心更震撼。
3. **去掉状态标签** — "专注中"/"待开始" 文字完全删除，状态通过按钮和上下文传达。
4. **进度条 2px** — 从 4px 减到 2px，几乎隐形。
5. **任务选择器简化** — 从 ComboBox 改为 TextBlock 显示当前任务名 + ▾ 箭头，点击打开 TaskManagerWindow。仅 Idle 可见。
6. **按钮紧凑化** — PrimaryButton padding 32x12 → 22x11（DESIGN.md button-primary 规范）。
7. **窗口控制极简化** — 28x28 → 24x24，描边 1.5px → 1px，默认 TertiaryText 极淡色。
8. **页脚统计文字** — 12px → 11px (DESIGN.md fine-print)，数字颜色从 Primary/Success 改为 SecondaryText 更克制。
9. **设置齿轮** — CircleButton 改为 24x24 透明 Path 图标，更轻量。
10. **动画简化** — 去掉 Border translateY 动画，改为纯窗口 Opacity fade-in。
11. **垂直间距重分配** — 任务选择器下方留白 48px，时间下方留白 24px，进度条下方留白 48px。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。
- Release 启动：进程正常启动并显示窗口。

**涉及模块**: TaskManagerWindow, StatsWindow, FocusCompleteDialog, BreakCompleteDialog, SettingsWindow

**修改文件数**: 7 个

### 改动摘要

1. **统一窗口控制** — 所有二级窗口关闭按钮改为 Path 几何图形绘制的极简 X，透明背景，28x28，与 MainWindow 一致。移除所有 Segoe MDL2 图标字体引用。
2. **标题统一 tagline** — TaskManagerWindow、StatsWindow 标题从 24px 降为 21/SemiBold（DESIGN.md tagline）。FocusCompleteDialog、BreakCompleteDialog 保持 21/SemiBold。
3. **按钮样式统一** — 所有窗口使用 PrimaryButton（pill, 14px）、TextLinkButton（无边框纯文字）、DangerLinkButton（错误色文字）三套样式。移除 inline Button.Resources + CornerRadius 方案。
4. **文字按钮替代图标按钮** — TaskManagerWindow 的编辑/删除从 Segoe MDL2 图标按钮改为纯文字链接（"编辑"/"删除"）。
5. **StatsWindow 对齐** — 任务统计卡片 CornerRadius 12→18，任务名字重 Medium→移除（默认 400），计数文字 13→14。
6. **SettingsWindow 完全重写** — 布局从 Label+StackPanel 改为 Grid 行对齐（label+input/toggle），Toggle 宽度 48→44，ComboBox 增加 pill 模板，段落分组对齐 MainWindow 内联设置。
7. **FocusCompleteDialog/BreakCompleteDialog** — 按钮从 inline pill 定义改为 Style 引用，图标缩小（64→56, 32→28）。

### 验证结果

- `dotnet build`：通过，0 warning / 0 error。
- `dotnet test`：通过，21/21。
