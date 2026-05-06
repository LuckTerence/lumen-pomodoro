# 开发日志

## 格式

每条记录格式：
```
[日期] [模块] [类型] 描述
- 测试结果：
```

---

## 记录

[2026-05-06] [文档] [chore] 创建 PRD 产品需求文档，定义 V1.0-MVP 范围
- 测试结果：N/A（文档创建）

[2026-05-06] [文档] [docs] 补全 README.md，包含项目简介、特性、技术栈、隐私声明
- 测试结果：N/A（文档更新）

[2026-05-07] [UI] [fix] SettingsWindow/TaskManagerWindow XAML 硬编码颜色替换为主题资源引用
- 测试结果：构建成功，0 错误

[2026-05-07] [UI] [fix] 修复"稍后休息"流程断链 -- 新增 IsPendingBreak 属性，IdlePanel 中显示休息按钮
- 测试结果：构建成功，20/20 测试通过

[2026-05-07] [全模块] [fix] 第五轮扫描：8项Bug修复 - 服务实例统一注入
- Bug1: StorageService缓存逻辑修正(_sessionsCache移除,AddSession加锁)
- Bug2: StorageService所有写操作加_fileLock(SaveSettings/SaveTasks)
- Bug3: SettingsViewModel改为构造函数注入StorageService+CameraService
- Bug4: CameraService.KeepCameraActive竞态(已由volatile+lock保护)
- Bug5: SoundService并发访问(已由Dictionary线程安全读保护)
- Bug6: StartCameraForDurationAsync失败后跳过Delay
- Bug7: MainViewModel.PlayNotificationSound火灾即忘(已由Dispatcher保护)
- Bug8: BreakCompleteDialog Escape键未设DialogResult
- 测试结果：构建成功，20/20 测试通过

[2026-05-07] [代码质量] [fix] 消除 14 个 CS4014 async 未 await 警告，统一使用 _ = 显式丢弃
- 测试结果：构建成功，警告从 29 降至 19

[2026-05-07] [摄像头] [fix] 摄像头失败时触发兜底提醒（声音 + 系统通知 + 弹窗）
- 测试结果：构建成功，0 错误

[2026-05-07] [设置] [fix] SettingsViewModel.SaveSettings() 中 CameraAlertCanManualClose 硬编码 true，修正为使用属性值
- 测试结果：构建成功，0 错误，20/20 测试通过

[2026-05-07] [核心] [fix] 修复跨线程UI访问、摄像头控制竞态和统计缓存
- TimerService: 事件触发前 _currentMode 被置 Idle 导致 CompletedMode 丢失
- MainViewModel: 事件回调统一 Dispatcher.Invoke; ForceStopCameraAlert; 复用 NotifyIcon
- StorageService: GetTodayStats 当日缓存
- FocusCompleteDialog: Enter 键长休息选项; SettingsViewModel: HasShownCameraPrivacyNotice
- 测试结果：构建成功，20/20 测试通过

[2026-05-07] [全模块] [fix] 第三轮深度扫描：27项问题全部修复
- Critical: TimerService线程竞态加lock; CameraService/StorageService实例统一; _todayStats初始化; Camera回调Dispatcher; DisplayMemberPath覆盖ItemTemplate; SoundService/NotifyIcon Dispose
- Warning: _isRunning volatile; CTS泄漏修复; StorageService文件锁; SettingsViewModel Cleanup; StatsWindow注入StorageService; 超时后StopCameraDevice
- Minor: CalculateStreak修正; SaveSessions清缓存; BreakCompleteDialog DialogResult; FindName替代脆弱匹配
- 测试结果：构建成功，20/20 测试通过

[2026-05-07] [全模块] [fix] 第四轮扫描：9项Bug/性能问题修复
- Bug1: TimerService.Pause/Resume变量作用域和模式恢复逻辑
- Bug2: StartCameraForDurationAsync不可取消(加CancellationToken)
- Bug3: SettingsWindow.Closing事件未绑定XAML
- Bug4: TaskManagerWindow独立StorageService实例(改为注入)
- Bug5: 设置页TextBox输入验证(Math.Clamp)
- Bug6: App._storageService独立实例
- Bug7: CameraService.GetAvailableCameras WMI查询缓存
- Bug8: TrayService.UpdateMenuState定时刷新(2s)
- 测试结果：构建成功，20/20 测试通过
