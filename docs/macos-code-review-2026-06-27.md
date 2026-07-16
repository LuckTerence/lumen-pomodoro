# LumenPomodoroMac macOS 版本代码审查报告

> 审查日期：2026-06-27 | 审查人：Senior Developer | 项目：SwiftUI macOS (Swift 5.9, macOS 14+)

---

## 坏消息

**编译通过，可以运行**——这是一条好消息。但在深入审查后，我发现了若干架构层面的问题。部分问题与 WPF 版本同源，但 Swift 版本因其特殊性引入了一些独有的风险。

---

## P0 — 必须修复（功能正确性风险）

### 1. SettingsView 绑定不保存，用户设置会丢失
**位置**：`SettingsView.swift:10-86`

大部分 Toggle/Stepper/Picker 通过 `$viewModel.settings.xxx` 直接双向绑定到 Model，但**从未触发 `saveSettings()`**。

```swift
// 第10行：改了 workMinutes，但没有 saveSettings 调用
Stepper("工作 ...", value: $viewModel.settings.workMinutes, in: 5...120, step: 5)
// 第17行：改了 cameraAlertEnabled，同样不保存
Toggle("启用摄像头指示灯提醒", isOn: $viewModel.settings.cameraAlertEnabled)
```

仅有两个 `onChange` handler（第92行和第100行）写了保存逻辑，且都指向 blocklist。

**后果**：用户在设置页改了工作时长、提醒模式、目标等，关闭 App 后重新打开会全部丢失。

**修复方向**：给 `AppViewModel` 添加 `objectWillChange.send()` 监听，或用 `@AppStorage` 做轻量属性，或用 computed binding 每次 set 时调用 `saveSettings()`。

### 2. 强制解包可能 crash
**位置**：`InsightEngine.swift:151,159,258` 等多处

```swift
let hour = calendar.component(.hour, from: $0.endTime!)  // 已 filter completed 但 filter 可能不保证
```

虽已通过 `completedSessions(from:)` 过滤了 `endTime != nil`，但 `!` 总是脆弱点。未来代码变更可能引入遗漏。

**修复方向**：用 `guard let endTime = $0.endTime else { return }` 替代 `!`，或用 `compactMap` 先提取。

### 3. `FocusGuardService` 输入事件消耗 Accessibility API
**位置**：`FocusGuardService.swift:112-122`

```swift
let result = AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute as CFString, &focusedWindow)
```

`AXUIElementCopyAttributeValue` 在每次 poll tick 时调用。如果用户的前台 App 没有打开辅助功能权限，会持续失败；并且这个 API 在高频调用下可能有性能影响。

**修复方向**：加 `guard AXIsProcessTrusted()` 检查并优雅降级；降低 poll 频率或加错误计数限流。

---

## P1 — 架构债务（可维护性）

### 4. AppViewModel 仍然是上帝类（415 行）
**位置**：`AppViewModel.swift`

同一个问题。AppViewModel 管理了：
- 计时器生命周期
- 摄像头控制
- 任务 CRUD
- 防走神
- 灵动岛
- 日报
- 设置持久化
- 评分/笔记

**修复方向**：拆分为 `TimerViewModel`、`TaskListViewModel`、`SessionReviewViewModel`，用 `ObservableObject` 嵌套。

### 5. View 直接穿透 ViewModel 调用 Service
**位置**：`TimerView.swift:92,100`

```swift
Button("暂停") { viewModel.timerService.pause() }   // 直接调用 Service
Button("继续") { viewModel.timerService.resume() }   // 直接调用 Service
```

而 `startFocus()` 却是通过 ViewModel（`viewModel.startFocus()`）。这造成调用路径不一致：部分功能走 ViewModel → Service，部分功能直接走 Service。

**修复方向**：所有交互统一经过 ViewModel，ViewModel 的 `timerService` 设为 `private`。

### 6. 无 DI 容器，全是 Singleton 和直接实例化
**位置**：`AppViewModel.swift:23-26`

```swift
let timerService = TimerService()
let cameraService = CameraService()
let dynamicIsland = DynamicIslandService()
let focusGuard = FocusGuardService()
private let storage = StorageService.shared
```

所有依赖直接硬编码在 ViewModel 中，无法替换为 mock 进行测试。Swift 社区常用 `Resolver`、`Factory` 或手写简单容器。

**修复方向**：至少把 Services 抽成协议，通过 `init` 注入。

### 7. macOS 版本零测试
**位置**：整个 `LumenPomodoroMac/` 目录

没有 `XCTest` 文件，没有任何测试。这是迁移后最大的工程欠账。

**修复方向**：至少为核心路径（TimerService、StorageService、InsightEngine）添加 XCTest 覆盖。

### 8. 缺少 `.build/` 在 .gitignore
**位置**：`.gitignore`

SPM 的 `.build/` 目录没有被忽略。`git status` 会显示数百个缓存文件。

**修复方向**：添加 `LumenPomodoroMac/.build/` 到 .gitignore。

---

## P2 — 代码质量（改进建议）

### 9. `Settings` Codable 实现极其冗长（200 行）
**位置**：`AppModels.swift:31-198`

26 个属性，每个属性的 `decodeIfPresent` + 默认值重复 26 次。encode 也是 26 次 `try c.encode(...)`。

Swift 5.9 已有 `@Codable` macro 可用（通过 swift-syntax），但更建议直接迁移到 Swift 6 + SwiftData 或至少用 `Codable` 的默认合成（移除自定义 CodingKeys）。

**快速缓解**：由于 CodingKeys 的 rawValue 和属性名几乎一一对应（仅首字母大写差异），可以写一个 property wrapper 或 helper 减少模板。

### 10. `Task { }` 无错误处理
**位置**：`AppViewModel.swift:184,207,218,222,322,337`

```swift
Task { await stopCameraIfNeeded() }     // error 被吞
Task { await triggerCameraAlertAfterFocus() }  // error 被吞
```

所有 `Task { }` 都没有 try-catch。`startForDuration` 和 `start()` 内部会抛出 `CameraError`，但调用方完全不知道。

**修复方向**：至少把 error 记录到 `cameraErrorMessage`，或统一在 Task 内 catch。

### 11. `onChange(of: viewModel.settings)` 不精确
**位置**：`SettingsView.swift:92`

```swift
.onChange(of: viewModel.settings) { _, _ in
    viewModel.settings.focusGuardBlocklist = blocklistText
        .split(whereSeparator: \.isNewline)
        // ...
    viewModel.saveSettings()
}
```

`Settings` 是 struct，Equatable。任何设置变更都会触发这个 handler，但它只干一件事：把 blocklistText 解析后写回。其他设置变更也会走这条路径，做了无用的 blocklist 解析。

**修复方向**：拆分为独立的 `onChange(of: blocklistText)` 和 `onChange(of: viewModel.settings.workMinutes)` 等，每个 handler 做自己该做的事。

### 12. 两个 Handler 对 blocklist 的重复处理
**位置**：`SettingsView.swift:92-106`

```swift
// Handler 1：监听 viewModel.settings 变化
.onChange(of: viewModel.settings) { _, _ in
    viewModel.settings.focusGuardBlocklist = blocklistText.split(...).map(...)
    viewModel.saveSettings()
}
// Handler 2：监听 blocklistText 变化
.onChange(of: blocklistText) { _, newValue in
    viewModel.settings.focusGuardBlocklist = newValue.split(...).map(...)
    viewModel.saveSettings()    
}
```

两个 handler 解析逻辑完全重复。`Settings` Equatable 变化时会先触发 Handler 1（写 blocklist 并 save），如果 blocklist 变了——又会触发 Handler 2（再次写 blocklist 并 save）。形成了冗余链。

**修复方向**：只保留一个 `onChange(of: blocklistText)`，移除对 settings 的监听。

### 13. `Color(hex:)` 重复定义
**位置**：`TimerView.swift:200-216` 和 `TasksView.swift`（调用但定义只在 TimerView）

`Color(hex:)` 只在 TimerView 文件末尾定义，但 TasksView 也在用。如果 TimerView 不是第一个加载的 View，TasksView 会编译失败（实际上因为同一个 module 共享所有 extension，不会 fail，但不是好习惯）。

**修复方向**：把 `Color(hex:)` 提取到独立的 `Extensions/Color+Hex.swift` 文件中。

---

## 好的一面

| 亮点 | 说明 |
|------|------|
| SPM 管理依赖零外部包 | 纯 Apple 框架（SwiftUI/AVFoundation/AppKit），编译干净 |
| `@MainActor` 标注范围合理 | TimerService、CameraService、FocusGuardService 均正确标注 |
| TimerService 使用 `endDate` 而非累加秒数 | 避免 Timer 漂移，这是正确的设计 |
| 摄像头 30 分钟自动保护 | `maxRunMinutes` 安全上限，防止意外耗尽摄像头 |
| 弱引用捕获 `[weak self]` 使用规范 | Timer/FocusGuard 闭包均正确使用 weak self |
| 隐私 Sheet 体验好 | SwiftUI sheet 弹出隐私声明，比 WPF 的 MessageBox 更现代 |
| `withCheckedContinuation` 处理异步 | CameraService 正确桥接了 callback 到 async/await |

---

## 快速修复清单（建议立即处理）

| # | 修复项 | 工作量 | 风险 |
|---|--------|--------|------|
| 1 | `.build/` 加入 .gitignore | 1 分钟 | 无 |
| 2 | SettingsView 所有 binding 加 `saveSettings()` | 15 分钟 | 无 |
| 3 | InsightEngine 强制解包改为 guard-let | 10 分钟 | 无 |
| 4 | `TimerView` 统一经过 ViewModel（让 `timerService` 变 private） | 10 分钟 | 低 |
| 5 | `Task {}` 无错误处理，加 `cameraErrorMessage` fallback | 5 分钟 | 无 |
| 6 | `Color(hex:)` 提取到独立文件 | 5 分钟 | 无 |
| 7 | 删除 SettingsView 重复的 blocklist handler | 3 分钟 | 无 |
