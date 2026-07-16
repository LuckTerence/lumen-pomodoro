# FocusGuard ↔ Stretchly 规则对齐表

> 文档版本：2026-07-17  
> 对标项目：[hovancik/stretchly](https://github.com/hovancik/stretchly)（休息提醒与空闲/勿扰规则行业标杆）  
> Lumen 实现：`FocusGuardService`、`PresenceDetector`、`Settings`、通知协调器  

相关文档：

- [十项目对标](./benchmark-10-projects.md)
- [跨端契约](./cross-platform-contract.md)

---

## 1. 产品定位差异（先对齐认知）

| 维度 | Stretchly | Lumen Pomodoro |
|------|-----------|----------------|
| 主目标 | 提醒你**停下来休息** | 提醒你**专注结束该休息** + 专注中少分心 |
| 时间模型 | 工作间隙自动弹出 mini/long break | 用户**手动**开始专注/休息，禁止自动循环 |
| 核心信号 | 全屏/窗口休息页 + 声音 | **摄像头指示灯** + 弹窗/声音/通知 |
| 空闲语义 | 空闲过久 → **暂停休息节奏**（你已在休息） | 空闲过久 → **分心/离开告警**（你该在学习） |
| 黑名单 | `appExclusions` 可 pause/resume 休息调度 | `FocusGuardBlocklist` 命中 → 分心告警 |

因此：**不能整段抄 Stretchly 逻辑**，应抄其「阈值、防抖、勿扰、预告、可配置性」等**工程化规则**。

---

## 2. 能力对照总表

| 能力 | Stretchly（参考） | Lumen 现状 | 对齐优先级 | 建议 |
|------|-------------------|------------|------------|------|
| 专注中键鼠空闲检测 | `naturalBreaks` + `naturalBreaksInactivityResetTime` 默认约 5min | `FocusGuardIdleSeconds` 默认 **180s** | P1 | 保持 180 可配；设置 UI 标明「空闲即判定离开」 |
| 空闲后的产品动作 | **暂停 break 调度** | **触发 DistractionDetected** | — | 语义不同，**不改为暂停计时**（考研场景需继续倒计时） |
| 空闲检测轮询间隔 | `naturalBreaksCheckInterval` 默认 **2000ms** | `FocusGuardPollSeconds` 默认 **5s** | P2 | 可允许 2–5s；过低增耗电 |
| 前台 App 黑名单 | `appExclusions` 规则 pause/resume | 子串匹配进程名/标题 | P1 | 保留子串；文档化匹配规则；Mac 补进程名 |
| 防抖 | 工程内有状态机，避免抖动 | `DebounceHits = 1`（几乎无防抖） | **P0** | 改为连续 N 次 poll 命中再告警，默认 N=2 或 3 |
| 告警上限 | 休息 postpone 有次数上限 | README：走神最多连续提醒 3 次 | **P0** | 代码确认/补齐 **MaxAlertsPerFocusSession=3** |
| 恢复专注 | 活动恢复后继续调度 | `FocusRegained` 事件 | P1 | 恢复后重置连续告警计数；可选轻提示 |
| 系统勿扰 DnD | `monitorDnd` 默认开，休息暂停 | **无** | P1 | Win：读 Focus Assist；Mac：读 DND；DnD 时降级为仅日志或静音 |
| DnD 轮询 | `monitorDndCheckInterval` 默认 2s | — | P2 | 与 FocusGuard poll 合并或 2–5s |
| 休息前预告 | mini 前 10s / long 前 30s 通知 | 无独立「即将结束」预告（完成时才提醒） | P2 | 可选：剩余 60s/30s 系统通知或灵动岛闪动 |
| 休息 postpone | 可推迟且有次数/时间窗 | 无（手动开始休息） | P3 | 不引入；与「手动确认」原则一致 |
| 严格模式 | strict 下限制跳过 | 无正式「严格模式」 | P2 | 可映射：Severe 级 + 禁止手动关摄像头灯 |
| 全屏休息窗 | 支持 fullscreen break | 无；用摄像头灯替代 | P2 | 可选设置「休息时全屏提示」，默认关 |
| 多屏 | `allScreens` / `breakContentScreen` | 主屏为主 | P3 | 有余力再做 |
| 挂起/锁屏 | `pauseForSuspendOrLock` | 计时有唤醒补偿 | P1 | 锁屏期间建议暂停 FocusGuard 轮询 |
| 音量 | `volume` 0–1 | 有开关无细粒度音量 | P3 | 可选 |
| 全局快捷键结束休息 | `CmdOrCtrl+X` 等 | 空格/Esc/任务键 | P2 | 保持现有；文档化快捷键表 |
| 会话中退出确认 | 部分版本托盘关闭确认 | 视实现 | P1 | 专注进行中退出 → 确认框（学 Pomatez） |

---

## 3. 参数默认值对齐建议

### 3.1 已有设置项

| Lumen 设置项 | 当前默认 | Stretchly 类比 | 建议默认 | 设置文案建议 |
|--------------|----------|----------------|----------|--------------|
| `FocusGuardEnabled` | true | naturalBreaks / 监控开 | **true** | 专注时检测离开与分心应用 |
| `FocusGuardIdleSeconds` | 180 | naturalBreaksInactivityResetTime ≈ 300s | **180**（可设 60–600） | 键鼠空闲超过此时长视为离开座位 |
| `FocusGuardPollSeconds` | 5 | checkInterval 2s | **5**（高级可调 2–10） | 检测间隔；越短越灵敏、越耗电 |
| `FocusGuardBlocklist` | 中文娱乐/社交默认集 | appExclusions.commands | **保留默认集** | 匹配前台进程名或窗口标题（包含即可） |
| `FocusGuardAlertLevel` | Severe | — | **Medium** 作默认更友好；重度用户改 Severe | 走神提醒强度 |
| `PresenceDetectionEnabled` | true | —（Stretchly 无摄像头） | 保持；与灯隐私分离说明 | 用摄像头判断是否在座位（不存画面） |
| `PresenceDetectionSeconds` | 5 | — | 5 | 在位判定窗口 |
| `SoundEnabled` | true | silentNotifications 反义 | true | |
| `SystemNotificationEnabled` | true | 系统通知 | true | |
| `PopupEnabled` | true | break 窗口 | true | |

### 3.2 建议新增设置项（契约 Schema 下一版本候选）

| 建议字段 | 类型 | 默认 | 来源 | 说明 |
|----------|------|------|------|------|
| `FocusGuardDebounceHits` | int | 2 | Stretchly 状态稳定 | 连续命中几次才告警 |
| `FocusGuardMaxAlertsPerSession` | int | 3 | README 已承诺 | 单次专注最多提醒次数 |
| `FocusGuardRespectDoNotDisturb` | bool | true | Stretchly monitorDnd | DnD 时不弹通知/不置顶 |
| `FocusGuardPauseOnLock` | bool | true | pauseForSuspendOrLock | 锁屏暂停检测 |
| `SessionEndPreNotifySeconds` | int | 0（关）或 30 | breakNotificationInterval | 结束前预告；0=关闭 |
| `ConfirmExitWhileFocusing` | bool | true | Pomatez/工程常识 | 专注中关闭窗口确认 |

> Schema 仍为 1 时：可用**代码常量**实现 Debounce/MaxAlerts，不必立刻升 schema；升到 2 时写入 settings。

---

## 4. FocusGuard 行为契约（应对齐的状态机）

### 4.1 何时运行

```text
仅当：
  Settings.FocusGuardEnabled == true
  AND TimerMode == Focus
  AND NOT IsPaused
否则 Stop()，并清除 distracted 状态。
```

### 4.2 单次 poll 判定

```text
1. 若 DnD 且 RespectDoNotDisturb：本 tick 不发通知（可仍更新内部状态）
2. 若空闲秒数 >= FocusGuardIdleSeconds → reason = "idle"
3. 否则取前台进程名 + 窗口标题，任一包含 blocklist 子串（OrdinalIgnoreCase）
   → reason = "blocklist:<match>"
4. 自身进程名永不命中
5. 都未命中 → reason = null（专注中）
```

### 4.3 防抖与告警上限（建议实现，对照现状）

| 步骤 | 现状 | 目标 |
|------|------|------|
| 命中累加 | `DebounceHits=1` 立即告警 | 连续 `DebounceHits`（默认 2）次非 null 才进入 distracted |
| 首次进入 distracted | 触发 `DistractionDetected` | 同左，且 `alertCount++` |
| alertCount ≥ Max | ？ | **不再弹通知/置顶**；可更新托盘徽章 |
| reason == null | 清零 consecutive，可能 `FocusRegained` | 同左；`FocusRegained` 不重置 alertCount（防刷）；新一轮 StartFocus 时重置 alertCount |
| Stop/Reset | 清状态 | 清 consecutive、distracted、alertCount |

### 4.4 与摄像头在位检测分工

| 机制 | 数据源 | 用途 |
|------|--------|------|
| FocusGuard | 键鼠空闲 + 前台窗口 | 分心应用 / 离开键盘 |
| PresenceDetector | 摄像头帧差分 | 人是否在镜头前（可选） |
| Camera LED | 摄像头设备打开 | **仅作休息提醒信号**，不是监控 |

原则：用户拒绝摄像头权限时，FocusGuard 仍应可用；Presence 与 LED 降级关闭。

---

## 5. 通知强度映射（CameraAlertLevel）

与 Stretchly「窗口强制感」类比，用于走神与完成提醒：

| Level | 完成专注（摄像头流） | 走神 FocusGuard |
|-------|----------------------|-----------------|
| Light | 尽量仅指示灯 | 系统通知或托盘 |
| Medium | 灯 + 弹窗/完成面板 | 通知 + 可选声音 |
| Severe | 灯 + 弹窗 + 置顶 | 通知 + 声音 + 主窗置顶 |

**DnD 开启时：** 所有 Level 降级为 Light 行为（或不通知），灯是否亮由 `CameraAlertEnabled` 与隐私设置单独控制（建议 DnD 下也不强开摄像头）。

---

## 6. 实现任务清单（可直接进 backlog）

### P0 — 行为正确性

- [x] `FocusGuardService`：`DebounceHits` 默认改为 2（`FocusGuardDebounceHits` 可配置）
- [x] 单次专注会话 `MaxAlerts = 3`（`FocusGuardMaxAlertsPerSession`），超出不再发 `DistractionDetected`
- [x] 单元测试：`FocusGuardEngineTests` / `FocusGuardServiceTests`（防抖、上限、恢复不重置计数）
- [x] Mac：`AXIsProcessTrusted` 为 false 时跳过窗口标题 AX 调用，仍可用空闲 + App 名匹配

### P1 — 体验与系统协作

- [x] Win Focus Assist / 通知关闭 / 锁屏启发式（`SystemAttentionState`）+ `FocusGuardRespectDoNotDisturb`
- [x] Mac 锁屏 / 专注模式菜单项 / 旧版 DND 启发式（`SystemAttentionState.swift`）
- [x] 设置页暴露：防抖、每轮上限、遵从勿扰（Win + Mac）
- [x] 专注中退出应用确认（`ConfirmExitWhileFocusing`，Win 关窗/托盘退出 + Mac terminate）
- [ ] 设置页文案与默认值微调（`FocusGuardAlertLevel` 默认是否降为 Medium 需产品拍板）

### P2 — 增强

- [x] 结束前 N 秒预告（`SessionEndPreNotifySeconds`，默认 30，0=关）
- [x] 可选全屏休息（`FullscreenBreakEnabled`，默认关）
- [x] 严格模式（`StrictModeEnabled`：禁手动关灯、禁提前结束休息、完成时置顶）

### P3 — 暂缓

- [ ] 多屏休息窗  
- [ ] 细粒度音量  
- [ ] postpone 休息（与手动原则冲突，默认不做）

---

## 7. 测试用例（最小集）

| ID | 场景 | 期望 |
|----|------|------|
| FG-01 | Focus 中 idle 超过阈值且 debounce 满足 | 1 次 DistractionDetected |
| FG-02 | 前台标题含 `bilibili` | 分心；自身进程不触发 |
| FG-03 | 在黑名单与非黑名单间每 poll 切换 | 不足 debounce 不告警 |
| FG-04 | 同会话第 4 次满足条件 | 不通知（MaxAlerts） |
| FG-05 | Pause 专注 | Guard 停止，无事件 |
| FG-06 | Break 模式 | Guard 不运行 |
| FG-07 | DnD on（实现后） | 无系统通知/不置顶 |
| FG-08 | 无 AX/API 权限（Mac） | 不崩溃，功能关闭或仅 idle |

---

## 8. 明确不从 Stretchly 复用的部分

| 项 | 原因 |
|----|------|
| Electron 工程与依赖 | 与 WPF/Swift 栈不符 |
| 自动 mini/long break 调度 | 违反 PRD 手动确认 |
| 休息 idea 文案库 | 可选，非核心 |
| Contributor 偏好云同步 | PRD 禁止云同步 |
| 整包 i18n Weblate 流程 | 可后期学流程，非 0.3 |

---

## 9. 结论

> Stretchly 给 Lumen 的最大价值是 **工程化的「检测节奏 + 防骚扰 + 系统协作」**，不是休息调度模型本身。  
> 立刻可做：防抖、每会话告警上限、权限降级；中期：DnD、锁屏、退出确认；摄像头灯仍是主提醒，Stretchly 式全屏仅作可选增强。

**维护：** 实现勾选 §6 清单项时更新本文 checkbox；默认值变更同步 [cross-platform-contract.md](./cross-platform-contract.md)。
