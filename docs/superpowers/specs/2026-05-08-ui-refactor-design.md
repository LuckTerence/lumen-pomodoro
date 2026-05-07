# Lumen Pomodoro UI 重构设计文档

## 1. 背景

当前 DESIGN.md 是 Apple.com 网页设计规范，与桌面番茄钟产品不匹配。PRD 要求"玻璃拟态、Mica/Acrylic 半透明风格、Fluent Design 视觉语言"，但当前实现只有纯色背景和手写控件模板。

核心问题：
- 无真实 Mica/Acrylic 玻璃效果
- 按钮样式在 5 个窗口中重复定义（约 500 行重复 XAML）
- 主窗口 420x520 太挤，设置面板靠高度切换硬塞
- 弹窗式对话框（专注完成/休息完成）打断流程
- 主题切换为手动实现，不稳定

## 2. 重构目标

1. 引入 WPF-UI 库实现真实 Mica/Acrylic + Fluent Design 控件
2. 单窗口 + 底部 Tab Bar 导航，所有功能内聚
3. 统一样式体系，消除重复 XAML
4. 专注完成/休息完成改为内联状态过渡，不弹窗
5. 重写 DESIGN.md 为桌面番茄钟专属设计语言

## 3. 技术选型

| 项目 | 选择 | 理由 |
|------|------|------|
| UI 框架 | WPF-UI (`Wpf.Ui`) | 最成熟的 WPF Fluent Design 方案 |
| 窗口基类 | `FluentWindow` | 内置 Mica/Acrylic、现代窗口框架 |
| 导航 | `NavigationView` (底部 Tab) | 4 页面导航，适合 500-560px 宽度 |
| 主题 | WPF-UI 内置深/浅/系统 | 替代手动主题切换 |
| 控件 | WPF-UI Fluent 控件 | 替代手写 ControlTemplate |

## 4. 窗口架构

```
FluentWindow (Mica 背景, 520x640)
├── NavigationView (底部 Tab Bar)
│   ├── Tab: 计时器 (TimerIcon) → TimerPage
│   ├── Tab: 任务 (TaskIcon) → TasksPage
│   ├── Tab: 统计 (StatsIcon) → StatsPage
│   └── Tab: 设置 (SettingsIcon) → SettingsPage
└── 内容区 (Frame 承载各 Page)
```

窗口尺寸：520 x 640，允许用户调整高度（最小 520），宽度固定。

## 5. 页面设计

### 5.1 TimerPage

```
┌─────────────────────────────┐
│  [任务名 ▾]     [摄像头状态]  │  ← 顶栏
│                              │
│                              │
│          25:00               │  ← 80px Inter Light
│                              │
│       ───────────           │  ← 2px 进度条
│                              │
│   [开始专注]   [−5] [+5]     │  ← 按钮区
│                              │
│  今日 3 · 专注 75分           │  ← 底部统计摘要
└─────────────────────────────┘
```

状态切换（不弹窗）：
- Idle → 显示任务选择 + 开始专注 + 时长调整
- Focus → 显示暂停 + 重置
- Paused → 显示继续 + 重置
- FocusCompleted → 时间呼吸动画 + 短休息/长休息/跳过按钮
- Break → 显示结束休息
- BreakCompleted → 显示开始下一轮

### 5.2 TasksPage

```
┌─────────────────────────────┐
│  任务管理                     │  ← tagline 21/600
│                              │
│  ┌─────────────────────────┐│
│  │ ● 考研数学    数学  编辑 删除││  ← 任务卡片
│  ├─────────────────────────┤│
│  │ ● 英语阅读    英语  编辑 删除││
│  └─────────────────────────┘│
│                              │
│  [任务名称输入] [分类▾] [添加] │  ← 新增表单
└─────────────────────────────┘
```

### 5.3 StatsPage

```
┌─────────────────────────────┐
│  今日统计                     │  ← tagline 21/600
│                              │
│  ┌──────────┐┌──────────┐  │
│  │    3     ││    75    │  │  ← 统计卡片 (display 40/600)
│  │ 完成番茄钟││ 专注时长(分)│  │
│  └──────────┘└──────────┘  │
│                              │
│  按任务分布                    │  ← caption-strong 14/600
│  ● 考研数学  2 轮             │
│  ● 英语阅读  1 轮             │
└─────────────────────────────┘
```

### 5.4 SettingsPage

```
┌─────────────────────────────┐
│  设置                         │  ← tagline 21/600
│                              │
│  计时                         │  ← section title
│  专注时间（分钟）    [25    ]  │
│  短休息（分钟）      [5     ]  │
│  长休息（分钟）      [15    ]  │
│                              │
│  摄像头                       │
│  启用摄像头提醒      [开关]    │
│  提醒模式            [下拉]    │
│  固定亮灯时长（秒）  [180   ]  │
│  休息期间亮灯        [开关]    │
│  摄像头选择          [下拉]    │
│  [测试摄像头]                 │
│                              │
│  提醒                         │
│  声音提醒            [开关]    │
│  弹窗提醒            [开关]    │
│  系统通知            [开关]    │
│                              │
│  外观                         │
│  主题                [下拉]    │
│  动画效果            [开关]    │
│                              │
│  系统                         │
│  托盘运行            [开关]    │
│  关闭时最小化到托盘  [开关]    │
│  开机自启            [开关]    │
└─────────────────────────────┘
```

## 6. 样式体系

### 6.1 WPF-UI 控件替代映射

| 当前自定义 | WPF-UI 替代 | 备注 |
|-----------|------------|------|
| PrimaryButton (手写 pill) | `ui:Button` Appearance=Primary | 原生 Fluent pill |
| SmallSecondaryButton | `ui:Button` Appearance=Secondary | 次级按钮 |
| TextLinkButton | `ui:Button` Appearance=Transparent | 透明文字按钮 |
| DangerLinkButton | `ui:Button` Appearance=Danger | 危险操作 |
| SettingsToggle (手写) | `ui:ToggleSwitch` | Fluent 开关 |
| SettingsInput (手写 pill) | `ui:TextBox` | Fluent 输入框 |
| SettingsCombo (手写) | `ui:ComboBox` | Fluent 下拉框 |
| GlassPanel (手写 Border) | Mica 背景 + `ui:Card` | Fluent 卡片 |
| 手动 LightTheme/DarkTheme | WPF-UI 主题系统 | 内置深/浅/系统 |

### 6.2 保留的自定义样式（CustomStyles.xaml）

仅 WPF-UI 无法覆盖的业务特定样式：

1. **TimerText** -- 80px Inter Light，呼吸/脉冲动画
2. **CameraAlertDot** -- 摄像头状态呼吸动画
3. **ProgressBar** -- 2px 极简进度条（可能需微调 WPF-UI ProgressBar）
4. **TaskColorDot** -- 任务颜色圆点 Ellipse
5. **StatNumber** -- 40px Inter SemiBold 统计数字

## 7. 颜色体系

### 浅色主题

| Token | 色值 | 用途 |
|-------|------|------|
| Primary | #0066CC | 唯一交互色 |
| PrimaryHover | #0071E3 | 悬停态 |
| Ink | #1D1D1F | 主文字 |
| Muted | #7A7A7A | 次要文字 |
| Tertiary | #CCCCCC | 极淡文字 |
| Surface | #FFFFFF | 卡片/面板 |
| Canvas | #F5F5F7 | 页面背景 |
| ControlBg | #D2D2D7 | 控件背景 |
| Hairline | #E0E0E0 | 分割线 |
| Success | #34C759 | 完成状态 |
| Warning | #FF9F0A | 提醒 |
| Error | #FF3B30 | 错误 |

### 深色主题

| Token | 色值 | 用途 |
|-------|------|------|
| Primary | #2997FF | 唯一交互色（深色表面更亮） |
| PrimaryHover | #40A6FF | 悬停态 |
| Ink | #F5F5F7 | 主文字 |
| Muted | #CCCCCC | 次要文字 |
| Tertiary | #7A7A7A | 极淡文字 |
| Surface | #2C2C2E | 卡片/面板 |
| Canvas | #1C1C1E | 页面背景 |
| ControlBg | #3A3A3C | 控件背景 |
| Hairline | #48484A | 分割线 |
| Success | #30D158 | 完成状态 |
| Warning | #FFD60A | 提醒 |
| Error | #FF453A | 错误 |

## 8. 排版体系

| Token | 大小 | 字重 | 行高 | 字距 | 用途 |
|-------|------|------|------|------|------|
| hero | 80px | Light (300) | 1.0 | -0.02em | 倒计时数字 |
| display | 40px | SemiBold (600) | 1.1 | -0.01em | 统计数字 |
| tagline | 21px | SemiBold (600) | 1.19 | 0 | 页面标题 |
| body | 17px | Regular (400) | 1.47 | -0.01em | 正文 |
| caption | 14px | Regular (400) | 1.43 | 0 | 辅助文字 |
| fine-print | 12px | Regular (400) | 1.0 | 0 | 页脚 |

字体族：Inter（Light/Regular/SemiBold），作为 SF Pro 开源替代。

## 9. 动效体系

| 场景 | 动效 | 参数 |
|------|------|------|
| 启动 | 淡入 | Opacity 0→1, 0.3s, EaseOut |
| 专注完成 | 时间呼吸 | Opacity 1→0.5→1, 3s 循环 |
| 摄像头提醒 | 圆点呼吸 | Opacity 1→0.3→1, 2s 循环 |
| 暂停 | 时间微缩放 | Scale 1→0.97→1, 4s 循环 |
| 按钮按下 | 缩放反馈 | Scale 0.95, 0.08s |
| 页面切换 | 淡入 | NavigationView 内置过渡 |
| 托盘恢复 | 淡入 | Opacity 0→1, 0.25s |

禁止：大幅抖动、廉价闪烁、粒子特效、影响操作响应的复杂动画。

## 10. 文件变更清单

### 删除

| 文件 | 原因 |
|------|------|
| Views/FocusCompleteDialog.xaml(.cs) | 专注完成改为内联状态 |
| Views/BreakCompleteDialog.xaml(.cs) | 休息完成改为内联状态 |
| Views/SettingsWindow.xaml(.cs) | 设置改为导航页面 |
| Views/StatsWindow.xaml(.cs) | 统计改为导航页面 |
| Views/TaskManagerWindow.xaml(.cs) | 任务改为导航页面 |
| Themes/LightTheme.xaml | WPF-UI 自带主题 |
| Themes/DarkTheme.xaml | WPF-UI 自带主题 |

### 新建

| 文件 | 内容 |
|------|------|
| Views/Pages/TimerPage.xaml(.cs) | 计时器页面 |
| Views/Pages/TasksPage.xaml(.cs) | 任务管理页面 |
| Views/Pages/StatsPage.xaml(.cs) | 统计页面 |
| Views/Pages/SettingsPage.xaml(.cs) | 设置页面 |
| ViewModels/TasksViewModel.cs | 任务管理 ViewModel |
| ViewModels/StatsViewModel.cs | 统计 ViewModel |
| Themes/CustomStyles.xaml | 自定义样式（倒计时、动画等） |

### 修改

| 文件 | 变更 |
|------|------|
| MainWindow.xaml(.cs) | 改为 FluentWindow + NavigationView |
| App.xaml(.cs) | 改用 WPF-UI 主题系统 |
| MainViewModel.cs | 移除设置内联逻辑，专注计时核心 |
| SettingsViewModel.cs | 调整绑定目标到 SettingsPage |
| LumenPomodoro.csproj | 添加 Wpf.Ui NuGet |
| DESIGN.md | 完全重写为桌面番茄钟设计语言 |

### 不变

| 文件/目录 | 原因 |
|-----------|------|
| Services/* | 业务逻辑层不变 |
| Models/* | 数据模型不变 |
| Converters/* | 转换器保留 |
| Fonts/* | Inter 字体保留 |

## 11. ViewModel 职责划分

| ViewModel | 职责 |
|-----------|------|
| MainViewModel | 计时器核心逻辑、摄像头控制、状态管理 |
| TasksViewModel | 任务 CRUD、分类管理 |
| StatsViewModel | 今日统计数据、任务分布 |
| SettingsViewModel | 设置读写、摄像头测试 |

MainViewModel 通过事件/回调与其他 ViewModel 协作，不直接依赖。

## 12. 迁移策略

分 4 步执行，每步可独立验证：

1. **引入 WPF-UI + 改造 MainWindow** -- FluentWindow + NavigationView + 空页面
2. **迁移 TimerPage** -- 从 MainWindow 提取计时器 UI 到 TimerPage
3. **迁移 TasksPage + StatsPage + SettingsPage** -- 从弹窗改为导航页面
4. **清理 + 重写 DESIGN.md** -- 删除旧文件，更新文档

每步完成后运行 `dotnet build` + `dotnet test` 确认无回归。
