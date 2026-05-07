# Lumen Pomodoro

面向考研学习的 Windows 桌面番茄钟工具。专注结束后调用笔记本摄像头点亮指示灯，用视觉方式提醒你休息。

## 核心特性

- 番茄钟专注倒计时（默认 25 分钟，可自定义）
- 摄像头灯提醒：固定时长 / 直到确认 / 跟随休息三种模式
- 弹窗、声音、系统通知等辅助提醒，均可独立开关
- 考研任务管理（数学、英语、政治、专业课等分类）
- 今日学习统计（完成番茄钟数、专注总时长、按任务分布）
- 系统托盘运行 + 开机自启
- 玻璃拟态界面，Mica / Acrylic 半透明风格

## 技术栈

- C# + .NET + WPF
- LiteDB（本地数据存储）
- NAudio（声音播放）
- Windows 原生摄像头 API

## 项目结构

```
lumen-pomodoro/
├── docs/
│   ├── PRD.md          # 产品需求文档
│   └── dev_log.md      # 开发日志
├── LumenPomodoro/       # WPF 应用源码
├── LumenPomodoro.Tests/ # xUnit 测试
├── LICENSE
└── README.md
```

## 开发进度

当前阶段：V1.0 MVP 可用性收口中。详见 [PRD 产品需求文档](docs/PRD.md) 与 [开发日志](docs/dev_log.md)。

## 隐私声明

- 不保存、不展示、不上传摄像头画面
- 摄像头仅用于触发硬件指示灯
- 所有数据存储在本地，不联网

## License

MIT
