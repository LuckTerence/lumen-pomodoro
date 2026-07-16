# Lumen Pomodoro

一个 Windows 番茄钟工具：专注结束时调用笔记本摄像头，让硬件指示灯亮起，用一个更醒目、更难忽略的物理信号提醒你该休息了。

适合考研复习、备考、自习、写作、编程和其他需要长时间专注的场景。

## 为什么是摄像头指示灯

传统番茄钟通常依赖声音、弹窗或系统通知，但这些提醒很容易失效：

- 声音可能被静音，或被视频、音乐盖住
- 弹窗可能被其他窗口遮挡
- 系统通知可能被勿扰模式拦截
- 手机提醒容易把短休息变成刷手机

摄像头指示灯不同。多数 Windows 笔记本在摄像头被调用时会自动点亮硬件指示灯。Lumen Pomodoro 只借用这个硬件信号：专注结束后点亮摄像头灯，让你更容易意识到该停下来休息。

## 隐私承诺

Lumen Pomodoro 不把摄像头当作采集设备使用，只把它当作本地硬件提醒信号。

- 不保存照片
- 不录制视频
- 不上传摄像头数据
- 不展示摄像头画面
- 仅在本机调用摄像头硬件
- 用户确认后释放摄像头，或流程结束后自动释放
- 摄像头连续运行超过 30 分钟会自动保护释放

首次启用摄像头提醒时，应用会显示隐私说明，并需要用户明确确认。

## 功能

### 计时
- 番茄钟：25 分钟默认，1-120 分钟可调，支持 ±5 分钟快速微调
- 短休息 / 长休息：5 分钟 / 15 分钟默认
- 长休息智能推荐：每完成 N 个番茄后金色高亮提示
- 手动确认原则：不自动进入休息，不自动循环，所有关键流程由用户手动控制

### 摄像头提醒
- 三种亮灯模式：固定时长 / 直到确认 / 跟随休息
- 三种强度等级：仅指示灯 / 指示灯+弹窗 / 指示灯+弹窗+置顶
- 摄像头指示灯状态窗口
- 30 分钟自动保护释放
- 首次使用弹出隐私声明，需用户明确同意

### 提醒方式
- 摄像头灯提醒（核心）
- 弹窗提醒（专注完成面板）
- 声音提醒（WAV 提示音）
- 系统通知（Windows 通知中心）
- 灵动岛通知（窗口最小化时浮动倒计时）
- 所有提醒方式可独立开关

### 走神检测
- 利用摄像头检测用户是否离开座位
- 离开时发送系统通知提醒
- 最多连续提醒 3 次，防止骚扰
- 严重强度下可强制置顶窗口

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
