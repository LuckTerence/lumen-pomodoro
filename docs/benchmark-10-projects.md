# 对标学习：10 个 GitHub 同赛道项目

> 文档版本：2026-07-17  
> 目的：指导 Lumen Pomodoro 多端迭代时**优先复用成熟设计与依赖**，避免重复造轮子。  
> 范围：桌面番茄钟 / 专注计时 / 休息提醒 / 本地优先生产力工具。

相关文档：

- [跨端数据与行为契约](./cross-platform-contract.md)
- [FocusGuard ↔ Stretchly 规则对齐](./focus-guard-stretchly-alignment.md)
- [产品 PRD](./PRD.md)

---

## 1. 选型原则

| 原则 | 说明 |
|------|------|
| 能抄设计 | 状态机、托盘信息架构、空闲/勿扰规则、数据模型文档结构 |
| 能抄依赖 | 已成熟的托盘/通知/发布渠道；禁止自研同类基础能力 |
| 不整仓迁移 | 不因对标而推翻现有 WPF 主产品 |
| 保留差异点 | **摄像头指示灯**是赛道内几乎无人做的卖点，继续自研薄封装 |

**明确不做（与 PRD 一致）：** 云同步、账号体系、手机强同步、社区/排行榜。

---

## 2. 十项目总表

| # | 项目 | Stars（约） | 技术栈 | 主要价值 | 与 Lumen 相关度 |
|---|------|------------:|--------|----------|----------------|
| 1 | [YetAnotherPomodoroApp/YAPA-2](https://github.com/YetAnotherPomodoroApp/YAPA-2) | 570 | C# / WPF | 同栈分层、托盘、历史热力图、CLI | ★★★★★ |
| 2 | [vladelaina/Catime](https://github.com/vladelaina/Catime) | 4.5k | 纯 C / Win32 | 极致体积、托盘交互、winget/scoop | ★★★★☆ |
| 3 | [Splode/pomotroid](https://github.com/Splode/pomotroid) | 5.3k | Tauri + Svelte | 真多端桌面、小体积 | ★★★★☆ |
| 4 | [zidoro/pomatez](https://github.com/zidoro/pomatez) | 4.9k | Electron / TS | 任务列表、全屏休息、严格模式 | ★★★★☆ |
| 5 | [ivoronin/TomatoBar](https://github.com/ivoronin/TomatoBar) | 3.4k | Swift / 菜单栏 | Mac 极简菜单栏体验 | ★★★★★（Mac） |
| 6 | [hovancik/stretchly](https://github.com/hovancik/stretchly) | 高星 | Electron | 空闲/DnD/休息预告/postpone | ★★★★★（规则） |
| 7 | [super-productivity/super-productivity](https://github.com/super-productivity/super-productivity) | 20k | Electron / TS | 任务×番茄×统计、local-first | ★★★☆☆ |
| 8 | [flowkeeper-org/fk-desktop](https://github.com/flowkeeper-org/fk-desktop) | 250 | Python + Qt6 | 领域文档（data-model/events） | ★★★★★（文档） |
| 9 | [adrcotfas/goodtime](https://github.com/adrcotfas/goodtime) | 1.8k | Kotlin / Compose | 隐私向、多端 KMP 思路 | ★★★☆☆ |
| 10 | [nsh07/Tomato](https://github.com/nsh07/Tomato) | 1.4k | Compose Multiplatform | 一套 UI 打 Android+Desktop | ★★★☆☆ |

**补充（不计入 10，但建议浏览）：**

| 项目 | 用途 |
|------|------|
| [open-pomodoro/openpomodoro-cli](https://github.com/open-pomodoro/openpomodoro-cli) | 会话数据互通格式（Open Pomodoro Format） |
| [gnome-pomodoro/gnome-pomodoro](https://github.com/gnome-pomodoro/gnome-pomodoro) | Linux 原生专注/屏蔽思路 |
| [zxch3n/PomodoroLogger](https://github.com/zxch3n/PomodoroLogger) | 番茄 + 看板 + 日志可视化 |

---

## 3. 逐项：解决什么 / 依赖 / 可复用 / 我们的不足

### 3.1 YAPA-2（最高优先级 · 同栈）

| 维度 | 内容 |
|------|------|
| **解决什么** | Windows 极简番茄钟：托盘、主题、历史 contribution 图、Jumplist、命令行控制 |
| **成熟依赖** | WPF；Squirrel.Windows（安装更新）；自有 Shared 层 |
| **值得复用** | `YAPA.Shared` 与主题/宿主分离；`/start` `/pause` 等 CLI；任务栏 Jumplist；GitHub 式热力图交互 |
| **我们的不足** | Domain 尚未抽成真正可跨 UI 的 Shared；无 Jumplist/CLI；主题体系仍绑死主窗口 |
| **落地建议** | 迭代 0.4：抽出 `LumenPomodoro.Core`（纯逻辑 + 模型），WPF/未来 Avalonia 共用 |

### 3.2 Catime

| 维度 | 内容 |
|------|------|
| **解决什么** | Windows 超轻量倒计时/番茄 + 托盘动画 |
| **成熟依赖** | 纯 C + Win32；winget / scoop / 微软商店分发 |
| **值得复用** | 托盘左键设时、右键菜单信息架构；配置路径约定；字体子集减体积；发布渠道 |
| **我们的不足** | 发布包与 winget 未完善；托盘默认关闭（`TrayEnabled=false`） |
| **落地建议** | 学交互与分发，**不要**用 C 重写；考虑默认开启托盘或首次引导 |

### 3.3 Pomotroid

| 维度 | 内容 |
|------|------|
| **解决什么** | 好看、可配置的多端桌面番茄钟 |
| **成熟依赖** | Tauri、Svelte；跨平台打包流水线 |
| **值得复用** | 「一套前端 + 原生壳」分层；小体积发布；设置信息架构 |
| **我们的不足** | 双端双写（WPF + Swift），无统一 UI 壳 |
| **落地建议** | 作为 **0.5 技术路线候选项**（Tauri），不立刻迁移 |

### 3.4 Pomatez

| 维度 | 内容 |
|------|------|
| **解决什么** | 专注 + 休息 + 内置任务 + 全屏 break + 严格模式 |
| **成熟依赖** | Electron / TypeScript |
| **值得复用** | 任务列表与会话绑定 UX；全屏休息作「高强度提醒」；会话中关闭确认 |
| **我们的不足** | 提醒强度主要靠摄像头灯，全屏休息是可选补强；严格模式未产品化 |
| **落地建议** | 对照设置项：可选「休息全屏」；会话中退出二次确认 |

### 3.5 TomatoBar

| 维度 | 内容 |
|------|------|
| **解决什么** | macOS 菜单栏番茄钟，极简配置与声音 |
| **成熟依赖** | Swift / AppKit·SwiftUI 菜单栏模式 |
| **值得复用** | 「菜单栏 3 秒可操作」交互；状态图标倒计时；最少设置面 |
| **我们的不足** | Mac 版偏完整窗口 App，菜单栏未成为主入口 |
| **落地建议** | Mac 0.3：菜单栏优先，主窗口次之 |

### 3.6 Stretchly

| 维度 | 内容 |
|------|------|
| **解决什么** | 跨平台「该休息了」提醒；空闲/DnD 感知；短休/长休节奏 |
| **成熟依赖** | Electron；系统 idle API；多语言 Weblate |
| **值得复用** | **产品规则**（见 [focus-guard-stretchly-alignment.md](./focus-guard-stretchly-alignment.md)）：natural breaks、DnD、预告秒数、postpone 次数、多屏 |
| **我们的不足** | 空闲语义偏「分心告警」而非「暂停节奏」；无系统勿扰联动；防抖过弱（`DebounceHits=1`） |
| **落地建议** | **只复用规则与默认值表**，实现继续用现有 FocusGuard / 平台 API |

### 3.7 Super Productivity

| 维度 | 内容 |
|------|------|
| **解决什么** | 高级 Todo + 番茄 + 时间追踪 + 第三方 issue 集成，local-first |
| **成熟依赖** | Electron、本地存储、可选同步 |
| **值得复用** | 任务↔时间盒数据关系；统计维度拆分；隐私/本地优先叙事 |
| **我们的不足** | 任务系统刻意保持轻量（PRD 明确不做复杂任务管理） |
| **落地建议** | **只读产品边界**：防止功能膨胀；不要集成 Jira 等 |

### 3.8 Flowkeeper

| 维度 | 内容 |
|------|------|
| **解决什么** | 正统番茄法桌面端；强调「做一件事做好」 |
| **成熟依赖** | Python + Qt6；完整 `doc/` 设计文档 |
| **值得复用** | `data-model.md` / `events.md` / `strategies.md` 文档结构；事件驱动边界 |
| **我们的不足** | 有 PRD，缺跨端契约与事件清单（本轮已补 contract） |
| **落地建议** | 契约与 Core 模块文档**照此骨架维护** |

### 3.9 Goodtime

| 维度 | 内容 |
|------|------|
| **解决什么** | 番茄 + Flow 技巧；开源、无广告、隐私 |
| **成熟依赖** | Kotlin / Compose（移动为主，桌面扩展） |
| **值得复用** | 隐私声明写法；多模式计时产品表述 |
| **我们的不足** | 不做手机端（PRD）；隐私叙事已有，可再统一 Win/Mac |
| **落地建议** | 文案与商店描述参考；技术栈不跟 |

### 3.10 Tomato (nsh07)

| 维度 | 内容 |
|------|------|
| **解决什么** | Material 3、数据导向的 Android + Desktop 番茄钟 |
| **成熟依赖** | Kotlin Multiplatform + Compose Multiplatform |
| **值得复用** | KMP 分层（common 逻辑 + 平台 UI）；数据导向统计 |
| **我们的不足** | 当前是 C# + Swift 双栈，不是 KMP |
| **落地建议** | 仅作 0.5「统一代码」ADR 对照项 |

---

## 4. 可复用轮子汇总

### 4.1 设计 / 规则（推荐直接复用）

| 来源 | 复用内容 | 落地点 |
|------|----------|--------|
| YAPA-2 | Shared 核心 vs 壳 | `LumenPomodoro.Core` 规划 |
| Flowkeeper | 领域文档骨架 | `cross-platform-contract.md` |
| Stretchly | 空闲/DnD/预告/postpone | FocusGuard / 设置默认值 |
| TomatoBar | 菜单栏优先 | Mac 信息架构 |
| Pomatez | 任务+严格/全屏休息 | 可选设置 |
| Open Pomodoro Format | 会话导出格式 | ExportService 增量 |

### 4.2 依赖 / 渠道（推荐接入，禁止自研）

| 能力 | 推荐 | 状态 |
|------|------|------|
| Win 托盘 | Hardcodet.NotifyIcon.Wpf | 已用 |
| Win UI | WPF-UI | 已用 |
| MVVM / DI | CommunityToolkit.Mvvm + MS.DI | 已用 |
| 日志 | Serilog | 已用 |
| 测试 | xUnit | 已用 |
| 发布 | winget / scoop / GitHub Releases | 部分有 Releases，可补 winget |
| 跨端统一 UI（远期） | Avalonia 或 Tauri 或 KMP | 未选，见契约 ADR 段 |

### 4.3 必须自研（有充分理由）

| 模块 | 理由 |
|------|------|
| 摄像头指示灯（MF / AVFoundation） | 无「只亮灯不录像」成熟专用库；核心卖点 |
| 走神黑名单匹配策略 | 中文考研场景默认黑名单与产品绑定 |
| 洞察引擎规则 | 业务差异；应用 golden test 锁两端一致 |
| 灵动岛类浮层 | 各端系统能力不同，薄封装即可 |

---

## 5. 学习与落地节奏（建议）

### 必 clone（本地读代码）

```bash
git clone https://github.com/YetAnotherPomodoroApp/YAPA-2.git
git clone https://github.com/hovancik/stretchly.git
git clone https://github.com/Splode/pomotroid.git
git clone https://github.com/zidoro/pomatez.git
git clone https://github.com/ivoronin/TomatoBar.git
```

### 在线读即可

- Flowkeeper 设计文档：https://github.com/flowkeeper-org/fk-desktop/tree/main/doc  
- Catime README（体积与托盘）  
- Open Pomodoro Format 说明  

### 优先级

```text
P0  YAPA-2 分层 + Stretchly 规则对齐 + TomatoBar Mac 形态
P1  Flowkeeper 契约文档（已落地）+ Pomatez 任务/严格模式
P2  Pomotroid / KMP / Avalonia 多端路线 ADR（先文档后编码）
P2  Catime 分发渠道与托盘 UX 微调
```

---

## 6. 与 Lumen 迭代的映射

| 迭代 | 对标输入 | 交付 |
|------|----------|------|
| **0.3 Mac 可发** | TomatoBar、Mac 审查 P0 | 设置落盘、菜单栏优先、核心路径 parity |
| **0.4 契约** | Flowkeeper、Open Pomodoro、YAPA Shared | 本仓库契约文档 + Core 边界 + golden tests |
| **0.5 多端决策** | Pomotroid / Avalonia / Tomato(KMP) | 一篇 ADR：继续双原生 or 统一壳 |

---

## 7. 结论

> 对标的目标是 **复用规则、分层与发布经验**，不是重写为 Electron/Tauri/KMP。  
> 摄像头灯继续自研；计时与数据用契约钉死；FocusGuard 向 Stretchly 规则看齐；Mac 向 TomatoBar 的菜单栏体验看齐。

维护约定：新增对标项目或完成一项「落地建议」时，更新本表「状态」与日期。
