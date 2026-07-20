# 跨端数据与行为契约

> 文档版本：2026-07-20 · SchemaVersion **2**  
> 参考：Flowkeeper `doc/data-model` / `events` 结构、YAPA-2 Shared 分层、Open Pomodoro Format  
> 实现源：`LumenPomodoro/Models/*`、`LumenPomodoroMac/.../AppModels.swift`、`StorageService`

相关文档：

- [十项目对标](./benchmark-10-projects.md)
- [FocusGuard 规则对齐](./focus-guard-stretchly-alignment.md)
- [PRD](./PRD.md)

---

## 1. 目标与非目标

### 1.1 目标

1. Win（WPF）与 Mac（SwiftUI）使用**同一套业务语义**与**可互拷的 JSON 文件**。
2. 计时状态迁移、完成判定、导出字段**两端一致**。
3. 平台能力（摄像头、托盘、通知、菜单栏）通过接口隔离，**契约只约束行为与数据，不约束 UI 框架**。

### 1.2 非目标

- 不做云同步、账号、实时多设备协作。
- 不要求 UI 像素级一致。
- 不强制两端共享同一二进制/同一语言（0.4 以契约 + 测试为准；0.5 再评估 Avalonia/Tauri/KMP）。

---

## 2. 存储布局

### 2.1 根目录

| 平台 | 路径 |
|------|------|
| Windows | `%APPDATA%/LumenPomodoro/` |
| macOS | `~/Library/Application Support/LumenPomodoro/`（实现须与此约定一致；若现状不同，迁移到此路径或文档同步修正） |

### 2.2 文件

| 文件 | 内容 | 序列化 |
|------|------|--------|
| `_schema.json` | 全局 schema 版本与迁移元信息 | JSON，缩进 |
| `settings.json` | 用户设置 | JSON；属性名 **PascalCase**（与 C# 默认一致） |
| `tasks.json` | 任务列表 | JSON 数组 |
| `sessions.json` | 专注会话列表 | JSON 数组 |
| `dailyplan.json` | 今日计划（峰值时段排程，A2） | JSON；按日期重置 |

### 2.3 `_schema.json`

```json
{
  "schema_version": 1,
  "updated_at": "2026-07-17T12:00:00.0000000+08:00"
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `schema_version` | int | 当前数据版本；与 `Settings.SchemaVersion` 对齐 |
| `updated_at` | string | ISO-8601 |

**迁移规则：**

- 启动时若 `schema_version < CurrentSchemaVersion`，按序执行 `Vn → Vn+1`。
- 当前 `CurrentSchemaVersion = 2`；V0→V1 仅写元数据，无字段破坏性变更；V1→V2 新增 `dailyplan.json`（今日计划，峰值时段排程 A2）。
- 未知更高版本：只读降级或提示升级 App，禁止静默写坏数据。

---

## 3. 数据模型

### 3.1 TaskItem（`tasks.json` 元素）

| 字段 (JSON) | 类型 | 必填 | 默认 | 说明 |
|-------------|------|------|------|------|
| `Id` | string | 是 | UUID | 主键 |
| `Name` | string | 是 | `""` | 显示名 |
| `Category` | string | 是 | `""` | 分类（如考研科目） |
| `Color` | string | 是 | `"#3B82F6"` | 十六进制颜色 |
| `CreatedAt` | string (DateTime) | 是 | now | ISO-8601 本地或带偏移，两端解析需兼容 |

### 3.2 FocusSession（`sessions.json` 元素）

| 字段 (JSON) | 类型 | 必填 | 默认 | 说明 |
|-------------|------|------|------|------|
| `Id` | string | 是 | UUID | 主键 |
| `TaskId` | string | 是 | `""` | 关联任务；可空串表示未绑定 |
| `TaskName` | string | 是 | `""` | 冗余显示名，避免任务改名后历史丢失 |
| `StartTime` | DateTime | 是 | now | 开始时间 |
| `EndTime` | DateTime? | 否 | null | 结束时间；未完成可为 null |
| `FocusMinutes` | int | 是 | 25 | **计划**专注分钟（不是实际经过分钟） |
| `Completed` | bool | 是 | false | 是否计为完成番茄 |
| `Notes` | string? | 否 | null | 笔记；UI 建议上限 200 字 |
| `QualityScore` | int | 是 | 0 | 0=未评；1–5=星级 |

**完成判定（契约）：**

- `Completed == true` 时，`EndTime` **必须**非 null。
- 统计「完成番茄数」只计 `Completed == true`。
- 实际专注分钟数：优先 `(EndTime - StartTime)` 换算；若缺 EndTime 则不计入完成时长。

### 3.3 Settings（`settings.json`）

两端字段应对齐。平台专有字段见 §3.4。

#### 3.3.1 计时

| 字段 | 类型 | 范围 | 默认 |
|------|------|------|------|
| `SchemaVersion` | int | ≥1 | 1 |
| `WorkMinutes` | int | 1–120 | 25 |
| `ShortBreakMinutes` | int | 1–60 | 5 |
| `LongBreakMinutes` | int | 1–120 | 15 |
| `LongBreakInterval` | int | 1–20 | 4 |

#### 3.3.2 摄像头提醒（高级可选，非主卖点）

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `CameraAlertEnabled` | bool | false | 默认关；首次开启需隐私确认 |
| `CameraAlertMode` | enum | `UntilConfirm` | 见下表 |
| `CameraFixedOnSeconds` | int | 180 | 10–3600；FixedDuration 用 |
| `CameraFollowBreakEnabled` | bool | true | FollowBreak 时是否跟休息 |
| `CameraIndex` | int | 0 | 0–99 |
| `CameraAlertCanManualClose` | bool | true | 是否允许手动关灯 |
| `CameraAlertLevel` | enum | `Medium` | Light / Medium / Severe |
| `HasShownCameraPrivacyNotice` | bool | false | 隐私声明是否已确认 |
| `HasCompletedOnboarding` | bool | false | 首次产品引导是否完成 |

**CameraAlertMode 字符串（跨端统一 PascalCase）：**

| 值 | 语义 |
|----|------|
| `FixedDuration` | 亮灯固定秒数后自动释放 |
| `UntilConfirm` | 亮到用户确认 |
| `FollowBreak` | 跟随休息阶段 |

**CameraAlertLevel：** `Light` | `Medium` | `Severe`  
（灯 / 灯+弹窗 / 灯+弹窗+置顶 等映射由 UI 层解释，但枚举名固定。）

**硬性约束：**

- 不保存照片、不录像、不上传画面。
- 连续占用摄像头超过 **30 分钟**必须自动释放。

#### 3.3.3 在位 / 防走神

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `PresenceDetectionEnabled` | bool | true | 摄像头在位检测（Win 已有；Mac 若未实现则忽略并文档标明） |
| `PresenceDetectionSeconds` | int | 5 | 1–300 |
| `FocusGuardEnabled` | bool | true | 前台+空闲防走神 |
| `FocusGuardBlocklist` | string[] | 见实现默认 | 子串匹配，大小写不敏感 |
| `FocusGuardIdleSeconds` | int | 180 | 10–3600；空闲超阈值判离开 |
| `FocusGuardPollSeconds` | int | 5 | 1–60 |
| `FocusGuardDebounceHits` | int | 2 | 1–10；连续 poll 命中后才告警 |
| `FocusGuardMaxAlertsPerSession` | int | 3 | 1–20；单次专注最多通知次数 |
| `FocusGuardRespectDoNotDisturb` | bool | true | DnD 降级（实现按端推进，字段已预留） |
| `FocusGuardAlertLevel` | enum | `Severe` | 走神提醒强度 |

默认黑名单（Win 基线；Mac 可追加平台进程名如 `Safari`，但导出时应保留用户自定义项）：

```text
bilibili, youtube, 抖音, douyin, 微博, weibo, 知乎, zhihu,
WeChat, Weixin, 微信, QQ, TikTok, Steam, 网易云音乐, 爱奇艺, 腾讯视频, 优酷
```

#### 3.3.4 目标与提醒

| 字段 | 默认 |
|------|------|
| `DailyGoalMinutes` | 120 |
| `WeeklyGoalMinutes` | 600 |
| `DailyTargetPomodoros` | 8 |
| `SoundEnabled` | true |
| `PopupEnabled` | true |
| `SystemNotificationEnabled` | true |

#### 3.3.5 系统体验

| 字段 | 默认 | 说明 |
|------|------|------|
| `TrayEnabled` | false | **Win**：托盘 |
| `CloseToTray` | false | **Win** |
| `AutoStartEnabled` | false | **Win** 开机自启 |
| `MenuBarEnabled` | true | **Mac** 菜单栏（编码键名） |
| `LaunchAtLogin` | false | **Mac**；解码时兼容 `AutoStartEnabled` |
| `Theme` | `"system"` | `system` / `light` / `dark` |
| `AnimationEnabled` | true | Win 有；Mac 缺则默认 true |
| `LastSelectedTaskId` | null | |
| `ExamDate` | null | |
| `ExamName` | `"考研"` | |
| `LastReportShownDate` | null | 日报防重复 |
| `InsightsEnabled` | true | |
| `DailyReportEnabled` | true | |
| `ExamCountdownEnabled` | true | |
| `DynamicIslandEnabled` | true | 灵动岛主交互，默认开 |
| `DynamicIslandWhenFocused` | `"minimize"` | `keep` / `minimize` / `hide` — 主窗前台时岛行为 |
| `ConfirmExitWhileFocusing` | true | 专注/休息/暂停中退出需确认 |
| `SessionEndPreNotifySeconds` | 30 | 0=关闭；剩余 ≤ 该秒数时预告一次 |
| `FullscreenBreakEnabled` | false | 休息时全屏遮罩倒计时 |
| `StrictModeEnabled` | false | 禁手动关灯、禁提前结束休息；完成时强制置顶 |
| （预设）`ApplyStrictFocusPreset` | — | 一键：严格 + 全屏休息 + 岛 keep；摄像头灯默认关（可选） |
| `Language` | `"system"` | `system` / `zh` / `en` |

### 3.3.6 DailyPlan（`dailyplan.json`，峰值时段排程 A2）

| 字段 (JSON) | 类型 | 必填 | 默认 | 说明 |
|-------------|------|------|------|------|
| `Date` | string (Date) | 是 | today | 计划所属日期；跨天自动失效，下一次保存落盘为今天 |
| `Blocks` | `PlannedBlock[]` | 是 | `[]` | 当日时段块列表 |

**PlannedBlock：**

| 字段 (JSON) | 类型 | 必填 | 默认 | 说明 |
|-------------|------|------|------|------|
| `Id` | string | 是 | UUID | 主键（删除用） |
| `TaskName` | string | 是 | `""` | 关联科目名 |
| `Hour` | int | 是 | 0 | 计划时段（0–23） |
| `DurationMinutes` | int | 是 | 25 | 计划专注分钟（默认取单次工作分钟） |

**约束：**

- `dailyplan.json` 由 V1→V2 迁移初始化（空计划）；旧端无此文件时不报错。
- 读取时若 `Date != 今天`，返回今日空计划（跨天重置语义）。
- 写入前 `Date` 归正为今天。
- 两端 `PlannedBlock` 字段对齐；Swift 侧用 `CodingKeys` 映射 PascalCase（`Id`/`TaskName`/`Hour`/`DurationMinutes`）。

### 3.4 平台专有字段映射

| 语义 | Windows JSON | macOS JSON | 互拷规则 |
|------|--------------|------------|----------|
| 开机自启 | `AutoStartEnabled` | `LaunchAtLogin`（兼写 `AutoStartEnabled`） | 导入时任一 true → 两端 true |
| 托盘/菜单栏常驻 | `TrayEnabled` | `MenuBarEnabled` | **不互拷**（语义相近但 UX 不同） |
| 关到托盘 | `CloseToTray` | （无） | Mac 忽略 |
| 在位检测 | `PresenceDetection*` | 可选 | Mac 无实现时读入保留、运行忽略 |
| 摄像头索引 | `CameraIndex` | 同名字段 | 设备列表不同，导入后可能需用户重选 |

### 3.5 日期与 JSON 约定

| 项 | 约定 |
|----|------|
| 属性命名 | **PascalCase**（C# 默认；Swift 用 `CodingKeys` 映射） |
| 枚举 | 字符串，PascalCase（`UntilConfirm` 而非 `untilConfirm`） |
| DateTime | 优先 ISO-8601；解析容忍无偏移的本地时间 |
| 布尔 / 数字 | JSON 原生类型 |
| 未知字段 | 读取时忽略，写回时不强制保留（向前兼容靠 schema 版本） |

---

### 3.6 日期归属规则（Day Attribution）【2026-07 新增·已收敛】

> 历史分歧（已修复）：Win 端原本以 `EndTime` 归日，Mac 端以 `StartTime` 归日，导致同一 `sessions.json` 两端「连胜天数」与「今日番茄数」不一致。

**契约规则：** 一个 `FocusSession` 归属于它**开始当天**（`StartTime.Date`），而非结束当天（`EndTime.Date）。

- 连胜（`CalculateStreak` / `calculateStreak`）：按 `StartTime.Date` 取去重日集合，从今天/昨天起向过去连续计数。
- 「今日」统计（`GetTodayStats` / `todayStats`）：以 `StartTime.Date == 今天` 判定。
- 跨零点会话（今晚开始、明早结束）计入**开始当天**，两端一致。
- `Completed == true` 时 `StartTime` 必非空（见 §3.2），故归日无需空判断。

**验收：** 同一 `sessions.json` 样本 → 两端 `CurrentStreak` 与「今日番茄数」数值一致（见 `LumenPomodoro.Tests/Services/GoldenStatsTests.cs`；Mac 侧 XCTest 见 Task #7）。

**已知遗留（后续收敛，未列入本次）：** Win 端 `InsightEngine` 的其余按日分组（热力图、小时分布、周趋势、目标进度、对比、效率）仍沿用 `EndTime`；为彻底一致，后续应将这些分组也统一为 `StartTime`。

## 4. 计时状态机（行为契约）

### 4.1 模式枚举

| 模式 | 说明 |
|------|------|
| `Idle` | 空闲，未在计时 |
| `Focus` | 专注倒计时中 |
| `Break` | 休息倒计时中（短休/长休不拆枚举，由本次时长区分） |
| `Paused` | 暂停（保留暂停前模式） |

### 4.2 合法迁移

```text
Idle  --StartFocus-->  Focus
Focus --Pause------->  Paused
Paused --Resume----->  Focus | Break（恢复到暂停前）
Focus --Complete---->  Idle（弹出完成流，不自动休息）
Focus --Reset------->  Idle
Idle  --StartBreak-->  Break
Break --Complete---->  Idle（不自动下一轮专注）
Break --Reset------->  Idle
任意运行态 --Reset--> Idle
```

### 4.3 产品硬规则（PRD）

| 规则 | 契约 |
|------|------|
| 自动进入休息 | **禁止** |
| 自动开始下一番茄 | **禁止** |
| 完成专注后 | 用户手动：短休 / 长休 / 跳过 |
| 长休建议 | 每完成 `LongBreakInterval` 个番茄后 UI 高亮建议，非强制 |
| 专注中防走神 | 仅 `Focus` 且未暂停时运行 FocusGuard |
| 休息中防走神 | 默认关闭 |

### 4.4 计时精度

| 项 | 约定 |
|----|------|
| 显示 | 剩余整秒 |
| 实现 | 允许亚秒 tick（如 250ms）+ 唤醒补偿 |
| 补偿 | 忽略 &lt;2s 抖动；&gt;24h 视为时钟异常不扣光 |

---

## 5. 领域事件（逻辑事件，非 UI）

参考 Flowkeeper events：两端可用不同语言实现，但**事件语义**一致，便于测试与日志。

| 事件 | 载荷（最小） | 触发点 |
|------|--------------|--------|
| `TimerStarted` | mode, totalSeconds, taskId? | StartFocus / StartBreak |
| `TimerTick` | remaining, total, mode | 每秒逻辑 tick |
| `TimerPaused` | modeBefore | Pause |
| `TimerResumed` | mode | Resume |
| `TimerCompleted` | mode, plannedMinutes | 倒计时归零 |
| `TimerReset` | fromMode | Reset |
| `SessionSaved` | sessionId, completed | 持久化会话 |
| `CameraAlertStarted` | mode, level | 开始亮灯 |
| `CameraAlertStopped` | reason | 用户确认 / 超时 / 保护释放 |
| `DistractionDetected` | reason | FocusGuard |
| `FocusRegained` | — | FocusGuard |
| `SettingsChanged` | — | 设置保存成功 |

测试可用「事件序列断言」代替 UI 自动化。

---

## 6. 服务边界（YAPA Shared 风格）

### 6.1 建议模块

```text
LumenPomodoro.Core（逻辑，理想态 / 0.4）
  Models, Timer rules, Insight pure functions, Schema migration
  无 WPF / 无 AppKit

LumenPomodoro（Win）
  Views, Tray, MF Camera, Win FocusGuard, Storage 路径

LumenPomodoroMac
  SwiftUI, AVFoundation Camera, AX FocusGuard, Storage 路径
```

### 6.2 平台接口（概念）

| 接口 | 职责 | 可移植性 |
|------|------|----------|
| `ITimerService` | 倒计时状态机 | 高（纯逻辑可共享） |
| `IStorageService` | JSON 读写 + 迁移 | 中（路径不同） |
| `ICameraService` | 亮灯/释放/保护超时 | 低（原生） |
| `IFocusGuardService` | 空闲+黑名单 | 低（原生） |
| `ISoundService` / `ITrayService` / 通知 | 反馈通道 | 低 |
| `IInsightEngine` | 纯函数洞察 | 高 |
| `IExportService` | CSV/JSON/（可选 OPF） | 高 |

**原则：** 能进 Core 的不进平台壳；平台壳禁止绕过 ViewModel 改业务不变量。

---

## 7. 导出契约

### 7.1 现有

| 格式 | 内容 |
|------|------|
| CSV | 会话表导出 |
| JSON | 会话/或完整备份 |

### 7.2 建议增量：Open Pomodoro 兼容（可选）

导出时增加一种映射（导入可后置）：

| Open Pomodoro 概念 | Lumen 字段 |
|--------------------|------------|
| 开始时间 | `StartTime` |
| 时长 | 完成则用实际区间，否则 `FocusMinutes` |
| 描述/标签 | `TaskName` / `Category` |
| 备注 | `Notes` |

具体行格式以实现 PR 为准；契约要求：**导出可文档化、字段稳定**。

---

## 8. 兼容性与互拷验收

### 8.1 最小验收用例

1. Win 写入 `settings.json` + 2 条 `sessions` + 1 个 `task` → 拷到 Mac 数据目录 → Mac 启动后数值一致。  
2. Mac 修改 `WorkMinutes` 与黑名单 → 拷回 Win → 生效。  
3. `Completed=true` 且无 `EndTime` 的脏数据：统计忽略或修复策略两端相同（推荐：视为未完成）。  
4. Golden：同一 `sessions.json` 样本 → 两端 Insight 峰值时段文案/数值一致。

### 8.2 当前已知差异（须收敛）

| 差异 | Win | Mac | 目标 |
|------|-----|-----|------|
| 连胜 / 今日归日 | `EndTime`（旧）→ `StartTime`（2026-07 已统一） | `StartTime` | **已收敛为 StartTime**（见 §3.6） |
| 在位检测设置 | 有 | 模型可能缺失 | Mac 保留字段或明确 ignore |
| 托盘 vs 菜单栏 | Tray* | MenuBar* | 文档化，不强制同字段 |
| 设置落盘 | ViewModel 保存 | SettingsView `settingsBinding` 整结构赋值 + `saveSettings` | 已加固（2026-07） |
| AnimationEnabled | 有 | 解码可缺省 | 统一默认 true |
| 洞察可执行动作 | `Insight.Action`（`SuggestedAction`：StartFocus/ScheduleBlock/AdjustDuration/OpenSettings） | `Insight.action`（同结构） | 双端一致；弱科目(TaskCompletion)返回 `StartFocus` 动作，黄金时段(PeakHour)返回 `ScheduleBlock` 动作。**`SuggestedAction` 本身运行时计算、不入 JSON；但 `ScheduleBlock` 的落盘结果写入 `dailyplan.json`（schema V2）** |
| 动作去重与反馈（A3） | `InsightEngine.SuppressActedActions`：今日已排程/已专注则隐藏对应动作；点击后 `MainViewModel` 弹应用内提示 | `InsightEngine.suppressActedActions`：同逻辑；点击后 Dynamic Island + 系统通知 | 双端一致：避免同一动作反复提示（去重），并对点击给出反馈 |

---

## 9. 版本演进

| Schema | 变更 |
|--------|------|
| 1 | 初版：本文件描述的全部字段 |
| 2 | 新增 `dailyplan.json`（今日计划，峰值时段排程 A2）；`DailyPlan`/`PlannedBlock` 模型（§3.3.6） |

变更流程：

1. 改模型 → 升 `CurrentSchemaVersion`  
2. 写 `MigrateVnToVn1`（Win + Mac）  
3. 更新本文件与 golden fixtures  
4. 发布说明写明「是否需要备份」

---

## 10. ADR 摘要：多端实现策略（0.5 决策前默认）

| 选项 | 决策（当前） |
|------|----------------|
| 双原生 + 本契约 | **默认采用** |
| Avalonia 统一 UI | 候选，Core 抽出后再评估 |
| Tauri / KMP 重写 | 候选，成本高，非 0.3/0.4 范围 |

**选择理由：** 已有成熟 WPF 与 Swift 半成品；摄像头/托盘强依赖原生；PRD 本地桌面优先。

---

## 11. 维护约定

- 任何新增 `Settings` / `FocusSession` / `TaskItem` 字段：先改本文件，再改两端代码与测试。  
- 行为变更（如允许自动循环）：先改 PRD + 本状态机表，禁止静默改产品原则。
