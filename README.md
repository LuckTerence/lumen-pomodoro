# Lumen Pomodoro

**灵动岛式专注番茄钟**（Windows + macOS）：屏幕顶部一颗胶囊显示倒计时，点一下就能暂停/继续——切到别的窗口也还在。

适合考研、备考、自习、写作、编程等需要长时间专注的场景。本地优先，无账号、无云同步。

## 核心体验：灵动岛

传统番茄钟依赖声音、弹窗和系统通知，备考时很容易被静音或淹没；手机提醒又容易刷飞。

Lumen 把主交互做成类似苹果「灵动岛」的顶栏胶囊：

1. **Compact**：模式 + 倒计时  
2. **Expanded**：点击展开 → 暂停 / 继续 / 结束休息 / 打开主窗  
3. **Transient**：完成、即将结束、走神等事件短暂弹出  

**15 秒演示：** 开始专注 → 切到其它窗口 → 看顶部岛继续走时 → 点岛可暂停。

摄像头**指示灯是可选增强**（设置中开启），不是默认卖点。

## 隐私承诺

- 数据只存在本机；不做账号与云同步  
- 默认不调用摄像头  
- 若你开启摄像头灯：不拍照、不录像、不上传、不展示画面；超时自动释放  

## 功能

### 计时与岛
- 番茄钟：25 分钟默认，可调；手动确认，不自动循环
- 灵动岛主交互（默认开启）；主窗前台可淡化 / 保持 / 隐藏
- 首次引导：岛是什么 → 本地隐私 → 场景预设
- 全屏休息与严格模式（场景预设一键切换）

### 摄像头灯（可选）
- 固定时长 / 直到确认 / 跟随休息
- 隐私声明 + 系统设置跳转诊断

### 其它提醒
- 声音、系统通知、完成面板
- 走神检测（可走岛 Transient）

### 任务管理
- 预置考研 5 科目任务模板（数学/英语/政治/专业课/复盘）
- 支持自定义任务，按分类和颜色标签管理
- 专注前选择任务，完成后自动绑定记录

### 专注质量
- 1-5 星专注评分
- 专注笔记（上限 200 字）
- 每轮专注结束后可评分+写笔记

### 统计与洞察
- 主界面：今日番茄数/目标、专注时长、连续学习天数、考试倒计时
- 统计页面：日/周/月切换，热力图、小时分布、任务饼图、周趋势
- 洞察引擎：峰值时段、最优日、趋势分析、科目均衡预警、里程碑
- 每日复盘报告：启动时展示昨日学习回顾
- 目标追踪：每日/每周分钟目标、每日番茄数目标

### 系统体验
- 托盘运行 + 右键菜单
- 可选开机自启
- 快捷键：空格(开始/暂停)、Esc(重置)、1-9(切换任务)
- 主题跟随系统（深色/浅色）
- 数据本地存储：`%APPDATA%/LumenPomodoro/`
- 数据导出：CSV / JSON

## 下载与运行

### Windows

1. 打开 [Releases](https://github.com/LuckTerence/lumen-pomodoro/releases)
2. 下载 `LumenPomodoro.zip`
3. 解压后双击根目录的 `LumenPomodoro.exe`
4. 开始专注

发布包采用「小启动器 + 主程序」：

- 根目录 `LumenPomodoro.exe`：原生启动器，检测并引导安装 .NET Desktop Runtime
- `app/LumenPomodoro.exe`：WPF 主程序

也可（合并进 winget 源后）：

```bat
winget install LuckTerence.LumenPomodoro
```

清单模板见 [`packaging/winget/`](packaging/winget/LuckTerence.LumenPomodoro/)。

### macOS

1. 从 [Releases](https://github.com/LuckTerence/lumen-pomodoro/releases) 下载 `LumenPomodoro-mac-*.dmg`
2. 打开 DMG，将 **Lumen Pomodoro** 拖到「应用程序」
3. 若 Gatekeeper 拦截：右键应用 → 打开

本地打 DMG：

```bash
cd LumenPomodoroMac && ./package-dmg.sh
```

打包与 winget 说明见 [docs/packaging-winget-mac.md](docs/packaging-winget-mac.md)。

## 开发

需要 .NET 8 SDK。

```bash
git clone https://github.com/LuckTerence/lumen-pomodoro.git
cd lumen-pomodoro

dotnet run --project LumenPomodoro
dotnet test LumenPomodoro.Tests
./Publish-LumenPomodoro.cmd
```

## 技术栈

| 类别 | 技术 |
|------|------|
| 框架 | .NET 8 + WPF |
| UI | WPF-UI (lepo.co) |
| 摄像头 | Windows Media Foundation |
| 存储 | System.Text.Json + 本地 JSON 文件 |
| 托盘 | Hardcodet.NotifyIcon.Wpf |
| 日志 | Serilog |
| 测试 | xUnit |
| 架构 | MVVM + DI（构造函数注入） |

## 配置文件

应用数据保存在 `%APPDATA%/LumenPomodoro/`：

- `settings.json` — 专注时长、提醒开关、功能开关、主题等设置
- `tasks.json` — 任务列表（含分类和颜色标签）
- `sessions.json` — 专注记录（含评分和笔记）
- `_schema.json` — 数据版本元信息（用于迁移）

## 文档

多端迭代与对标资料见 [docs/](docs/README.md)：

| 文档 | 内容 |
|------|------|
| [十项目对标](docs/benchmark-10-projects.md) | GitHub 同赛道项目学习与复用清单 |
| [跨端契约](docs/cross-platform-contract.md) | Win/Mac 数据、状态机、导出约定 |
| [FocusGuard 对齐](docs/focus-guard-stretchly-alignment.md) | 防走神规则 vs Stretchly + 实现 backlog |
| [PRD](docs/PRD.md) | 产品需求 |

## 许可证

[Apache License 2.0](LICENSE)

欢迎提交 Issue 和 Pull Request。贡献前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

---

[![GitHub release](https://img.shields.io/github/v/release/LuckTerence/lumen-pomodoro?style=for-the-badge)](https://github.com/LuckTerence/lumen-pomodoro/releases)
[![License](https://img.shields.io/github/license/LuckTerence/lumen-pomodoro?style=for-the-badge)](LICENSE)
