# 开发日志

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
