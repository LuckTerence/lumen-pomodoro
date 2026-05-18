# Lumen Pomodoro — 给你的专注一点光

> 一个为考研人、备考党、深夜赶due的你而写的番茄钟工具。

## 它想解决什么？

你有没有过这样的经历：

- 戴上耳机专注学习，音乐一响就听不见休息提醒
- 手机一响，顺手点进去，刷了半小时短视频
- 弹窗提醒被层层窗口遮挡，等你发现时已经过去一小时
- 困得睁不开眼，却还是不知道什么时候该停下

传统番茄钟依赖声音、弹窗或系统通知，但这些提醒太容易被忽略——尤其当你真正沉浸在一道难题里时。

Lumen Pomodoro 想做一件简单的事：用一个你很难忽略的视觉信号，温柔而坚定地提醒你：该休息了。

## 核心创意：让硬件为你说话

当专注时间结束，Lumen 会调用笔记本摄像头的指示灯——那个平时只在视频会议时亮起的小灯。

- 灯亮了 = 专注结束，该起来走走了
- 硬件级提醒 = 不会被软件屏蔽，不会静音忽略
- 绝不拍照录像 = 只是借用一下指示灯开关
- 呼吸般的节奏 = 给眼睛一个理由，离开屏幕深呼吸

## 主要特性

### 番茄钟核心
- 25分钟专注 x 5分钟短休 x 15分钟长休（完全可自定义）
- 随时暂停、继续、重置——你的节奏你掌控
- 四重提醒兜底：摄像头灯 + 弹窗 + 声音 + 系统通知
- 绝不自动循环——每轮专注和休息都由你亲手确认，因为我们需要的是「有意识的休息」，不是机械的流水线

### 考研向任务管理
- 预置数学、英语、政治、专业课等分类
- 记录每个任务的番茄钟数，看见进步轨迹
- 今日统计：本日完成数、专注时长、任务分布、学习 streak
- 连续学习天数提醒——别让努力断了线

### 隐私与安全
- 摄像头数据仅在本地调用，不保存、不展示、不上传
- 运行超过 30 分钟自动保护释放（防忘记关灯）
- 首次启用时展示隐私说明，需你明确同意
- 所有配置存在本地 %APPDATA%/LumenPomodoro/，不联网

### 视觉与体验
- 玻璃拟态界面：Mica + Acrylic 半透明材质，深浅色随系统
- 系统托盘运行：最小化不打断，托盘菜单快速操作
- 开机自启可选：让专注成为每天的第一个习惯
- 灵动岛倒计时（Windows 版特别版）：即使切到其他应用也能瞥见时间

## 界面预览

四个主要页面：计时器、任务、统计、设置，通过底部导航栏切换。

| 计时器 | 任务 | 统计 | 设置 |
|--------|------|------|------|
| 大号倒计时数字居中显示，状态驱动按钮切换 | 按分类管理考研任务，预置默认任务 | 今日完成数、专注时长、任务分布 | 专注/休息时长、摄像头提醒、通知开关等 |

## 技术栈

| 类别 | 技术 |
|------|------|
| 框架 | .NET 9 + WPF (Windows Presentation Foundation) |
| UI 库 | WPF-UI (Fluent Design System) |
| 摄像头 | Windows Media Foundation (原生硬件访问) |
| 数据存储 | JSON 文件 + 内存缓存 (Newtonsoft.Json) |
| 系统托盘 | Hardcodet.NotifyIcon.Wpf |
| 字体 | Inter (Light / Regular / SemiBold) |
| 测试 | xUnit (60+ 单元测试) |

## 快速开始

### 给普通用户

1. 下载 LumenPomodoro.exe（见下方 Assets）
2. 双击运行——无需安装 .NET 运行时，自包含单文件
3. 开始你的第一个 25 分钟专注

### 给开发者

```bash
# 克隆项目
git clone https://github.com/LuckTerence/lumen-pomodoro.git
cd lumen-pomodoro

# 直接运行（需要 .NET 9 SDK）
dotnet run --project LumenPomodoro

# 运行测试
dotnet test LumenPomodoro.Tests

# 生成发布包（Windows x64 自包含）
./Publish-LumenPomodoro.cmd
```

## 配置说明

所有配置自动保存在 %APPDATA%/LumenPomodoro/：

- settings.json - 专注时长、摄像头模式、提醒开关
- tasks.json - 你的考研任务列表
- sessions.json - 历史专注记录（用于统计）

可配置项：专注时长(1-120min) | 短/长休息 | 摄像头提醒模式 | 三种提醒开关 | 托盘 | 主题 | 等等

## 隐私承诺

我们深知备考期间的数据安全感有多重要：

1. 绝不采集：摄像头画面不保存、不展示、不上传
2. 本地存储：所有数据留在你的电脑，不联网
3. 透明可控：每次启用摄像头都有明确提示，可随时关闭
4. 自动保护：运行超时自动释放，防止忘记关灯

## 为什么叫 Lumen？

Lumen（流明）是光通量的单位。

考研路漫长，有时需要一点光——
不是刺眼的强光，而是当你沉浸太久时，
那个温柔提醒你「该休息一下」的微光。

愿 Lumen 陪你走过这段专注的时光，
每一轮番茄钟，都是向梦想靠近的一步

## 贡献指南

欢迎提交 Issue 和 PR！无论是：

- Bug 报告（使用模板详细描述）
- 功能建议（先查看现有 issues）
- 代码修复（遵循现有代码风格）
- 文档改进

查看 CONTRIBUTING.md 了解详情。

## 开源协议

Apache License 2.0 — 自由使用，保留署名

## 最后的话

> "重要的不是专注的时间有多长，
> 而是每一次专注后，都记得好好对待自己。"

累了就歇会儿，Lumen 会用一盏小灯等你回来

---

[![GitHub release](https://img.shields.io/github/v/release/LuckTerence/lumen-pomodoro?style=for-the-badge)](https://github.com/LuckTerence/lumen-pomodoro/releases)
[![GitHub stars](https://img.shields.io/github/stars/LuckTerence/lumen-pomodoro?style=for-the-badge)](https://github.com/LuckTerence/lumen-pomodoro/stargazers)
[![License](https://img.shields.io/github/license/LuckTerence/lumen-pomodoro?style=for-the-badge)](LICENSE)
