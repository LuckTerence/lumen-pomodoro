# 开发日志

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
