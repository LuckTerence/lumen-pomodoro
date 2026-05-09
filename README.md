# Lumen Pomodoro

面向考研学习的 Windows 桌面番茄钟工具。专注结束后调用笔记本摄像头点亮指示灯，用视觉方式提醒你休息。

## 它解决什么问题

传统番茄钟依赖声音、弹窗或系统通知来提醒休息，但这些方式容易被忽略：

- 声音可能被静音
- 弹窗可能被窗口遮挡
- 系统通知可能被勿扰模式拦截
- 手机提醒容易顺手刷手机

Lumen Pomodoro 利用笔记本摄像头指示灯的硬件特性——摄像头被调用时指示灯自动亮起——在专注时间结束后调用摄像头，让指示灯持续亮起，形成更醒目、更难忽略的视觉提醒。

软件不拍照、不录像、不上传摄像头数据，仅在本地调用摄像头并在用户确认或流程结束后释放。

## 核心特性

- **番茄钟计时** — 默认 25 分钟专注，可自定义；支持暂停、继续、重置
- **摄像头灯提醒** — 三种亮灯模式：
  - 固定时长：亮指定时间后自动关闭
  - 直到确认：持续亮灯直到用户手动确认
  - 跟随休息：休息倒计时期间保持亮灯
- **多种提醒方式** — 弹窗、声音、系统通知，均可独立开关，互为兜底
- **考研任务管理** — 预置数学、英语、政治、专业课等分类和默认任务，支持自定义
- **今日学习统计** — 完成番茄钟数、专注总时长、按任务分布、连续学习天数
- **系统托盘运行** — 最小化到托盘后计时继续运行，托盘菜单可快速操作
- **开机自启** — 可选开机自动启动，默认关闭
- **玻璃拟态界面** — 基于 WPF-UI 的 Mica / Acrylic 半透明风格，深浅色主题跟随系统
- **不自动循环** — 专注结束不自动进入休息，休息结束不自动开始下一轮，所有关键流程由用户手动确认

## 界面预览

四个主要页面：计时器、任务、统计、设置，通过底部导航栏切换。

| 计时器 | 任务 | 统计 | 设置 |
|--------|------|------|------|
| 大号倒计时数字居中显示，状态驱动按钮切换 | 按分类管理考研任务，预置默认任务 | 今日完成数、专注时长、任务分布 | 专注/休息时长、摄像头提醒、通知开关等 |

## 技术栈

| 类别 | 技术 |
|------|------|
| 框架 | .NET 9 + WPF |
| UI 库 | [WPF-UI](https://wpfui.lepo.co/) (Fluent Design) |
| 摄像头 | Windows Media Foundation (P/Invoke) |
| 数据存储 | JSON 文件 (Newtonsoft.Json) |
| 系统托盘 | Hardcodet.NotifyIcon.Wpf |
| 字体 | Inter (Light / Regular / SemiBold) |
| 测试 | xUnit |

## 项目结构

```
lumen-pomodoro/
├── LumenPomodoro/                 # WPF 应用源码
│   ├── Controls/                  # 自定义控件 (ArcProgress 弧形进度条)
│   ├── Converters/                # XAML 值转换器
│   ├── Fonts/                     # Inter 字体文件
│   ├── Models/                    # 数据模型 (Settings, TaskItem, FocusSession)
│   ├── Services/                  # 核心服务
│   │   ├── CameraService.cs       # 摄像头控制 (Media Foundation)
│   │   ├── SoundService.cs        # 声音播放 (WAV 生成 + SoundPlayer)
│   │   ├── StorageService.cs      # 本地数据持久化 (JSON)
│   │   ├── TimerService.cs        # 计时器核心逻辑
│   │   └── TrayService.cs         # 系统托盘
│   ├── ViewModels/                # MVVM ViewModel
│   ├── Views/                     # XAML 视图
│   │   ├── MainWindow.xaml        # 主窗口 (FluentWindow + NavigationView)
│   │   └── Pages/                 # 四个页面
│   └── Themes/                    # 自定义样式
├── LumenPomodoro.Tests/           # xUnit 单元测试
├── docs/
│   ├── PRD.md                     # 产品需求文档
│   └── dev_log.md                 # 开发日志
├── DESIGN.md                      # 设计语言规范
├── Publish-LumenPomodoro.cmd      # 生成 Windows x64 自包含发布包
└── LICENSE                        # Apache 2.0
```

## 快速开始

### 前置要求

- Windows 10 1809+ 或 Windows 11
- 开发者需要 [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)

### 启动

**普通用户** — 运行 `Publish-LumenPomodoro.cmd`，exe 会自动复制到桌面，双击即可使用。无需安装 .NET 运行时。

**开发者**

```bash
dotnet run --project LumenPomodoro
```

### 生成发布包

```bash
Publish-LumenPomodoro.cmd
```

发布输出位于 `publish/`，同时自动复制到桌面。该目录是本地构建产物，不提交到 Git；GitHub 开源时建议通过 Release 页面上传生成后的 `LumenPomodoro.exe`。

自动化环境可使用：

```bash
Publish-LumenPomodoro.cmd --no-pause
```

### 运行测试

```bash
dotnet test LumenPomodoro.Tests
```

### GitHub 开源发布

1. 提交源码，不提交 `publish/`。
2. 在本机运行 `Publish-LumenPomodoro.cmd` 生成 `publish/LumenPomodoro.exe`。
3. 推送代码到 GitHub。
4. 在 GitHub Releases 新建版本，把生成后的 `LumenPomodoro.exe` 作为附件上传。
5. Release 说明中标注：Windows x64、自包含单文件、不上传摄像头数据。

## 配置说明

所有配置存储在本地 `%APPDATA%/LumenPomodoro/` 目录下：

| 文件 | 内容 |
|------|------|
| `settings.json` | 应用设置（专注时长、摄像头模式、提醒开关等） |
| `tasks.json` | 任务列表 |
| `sessions.json` | 专注记录 |

首次启动时自动生成默认配置和考研预置任务。

### 主要可配置项

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| 专注时间 | 25 分钟 | 1-120 分钟可调 |
| 短休息 | 5 分钟 | - |
| 长休息 | 15 分钟 | - |
| 摄像头提醒 | 开启 | 核心提醒方式 |
| 摄像头提醒模式 | 直到确认 | 固定时长 / 直到确认 / 跟随休息 |
| 固定亮灯时长 | 3 分钟 | 固定时长模式下生效 |
| 休息期间亮灯 | 开启 | 跟随休息模式下生效 |
| 弹窗提醒 | 开启 | - |
| 声音提醒 | 开启 | - |
| 系统通知 | 开启 | - |
| 托盘运行 | 关闭 | - |
| 关闭按钮行为 | 退出 | 可改为最小化到托盘 |
| 开机自启 | 关闭 | - |
| 主题 | 跟随系统 | - |

## 隐私声明

- 不保存、不展示、不上传摄像头画面
- 摄像头仅用于触发硬件指示灯
- 所有数据存储在本地，不联网
- 摄像头运行超过 30 分钟自动保护释放
- 首次启用摄像头提醒时展示隐私说明，需用户确认

## 系统要求

- 操作系统：Windows 10 1809+ 或 Windows 11（Mica 背景效果需 Windows 11 22000+）
- 摄像头：笔记本内置摄像头或 USB 摄像头（仅摄像头灯提醒功能需要，不影响其他功能）
- 运行时：.NET 9.0 桌面运行时

## License

[Apache License 2.0](LICENSE)
