# Lumen Pomodoro

Lumen Pomodoro 是一个 Windows 番茄钟工具。它在专注结束时调用笔记本摄像头，让硬件指示灯亮起，用一个更醒目、更难忽略的物理信号提醒你该休息了。

适合考研复习、备考、自习、写作和其他需要长时间专注的场景。

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

- 番茄钟：支持专注、短休、长休，自定义时长
- 摄像头灯提醒：专注结束后点亮笔记本摄像头指示灯
- 多重提醒：摄像头灯、弹窗、声音、系统通知
- 任务管理：按科目或任务记录番茄钟
- 数据统计：今日完成数、专注时长、任务分布、连续学习天数
- 本地存储：配置和记录保存在 `%APPDATA%/LumenPomodoro/`
- 桌面体验：WPF 界面、系统托盘、桌面倒计时提示

## 下载与运行

1. 打开 [Releases](https://github.com/LuckTerence/lumen-pomodoro/releases)
2. 下载 `LumenPomodoro.zip`
3. 解压后双击根目录的 `LumenPomodoro.exe`
4. 开始专注

当前发布包采用“小启动器 + 主程序”的结构：

- 根目录 `LumenPomodoro.exe`：原生启动器，负责检测运行环境
- `app/LumenPomodoro.exe`：WPF 主程序

如果电脑没有安装 .NET 9 Desktop Runtime，启动器会提示并自动下载安装。这样可以把发布包控制在约 16MB，避免把完整运行时打进安装包。

## 开发

需要 .NET 9 SDK。

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
| 框架 | .NET 9 + WPF |
| UI | WPF-UI |
| 摄像头 | Windows Media Foundation |
| 存储 | System.Text.Json + 本地 JSON 文件 |
| 托盘 | Hardcodet.NotifyIcon.Wpf |
| 日志 | Serilog |
| 测试 | xUnit |

## 配置文件

应用数据保存在 `%APPDATA%/LumenPomodoro/`：

- `settings.json`：专注时长、提醒开关、主题等设置
- `tasks.json`：任务列表
- `sessions.json`：专注记录

## 许可证

[Apache License 2.0](LICENSE)

欢迎提交 Issue 和 Pull Request。贡献前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

---

[![GitHub release](https://img.shields.io/github/v/release/LuckTerence/lumen-pomodoro?style=for-the-badge)](https://github.com/LuckTerence/lumen-pomodoro/releases)
[![License](https://img.shields.io/github/license/LuckTerence/lumen-pomodoro?style=for-the-badge)](LICENSE)
