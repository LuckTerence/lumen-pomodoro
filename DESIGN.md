# Lumen Pomodoro 设计语言

## 1. 产品定位与设计哲学

Lumen Pomodoro 是一款面向考研学生的桌面番茄钟工具，帮助用户在长时间备考中保持专注、规律休息、量化学习成果。

**设计哲学：** 玻璃拟态为基底 + Apple 式极简美学 + Fluent Design 层级体系。

**三大原则：**

1. **内容优先** — 计时器是绝对视觉核心，其余一切退让。
2. **状态驱动** — 界面随计时状态（专注/短休/长休/暂停/完成）自然变化，无需用户主动寻找当前状态。
3. **克制** — 单一交互色、大量留白、无多余装饰。每一个视觉元素都必须为功能服务。

---

## 2. 颜色体系

### Light Theme

| Token | Value | Usage |
|-------|-------|-------|
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

### Dark Theme

| Token | Value | Usage |
|-------|-------|-------|
| Primary | #2997FF | 唯一交互色 |
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

**颜色使用规则：**

- Primary 是唯一的交互色，所有可点击、可操作的元素统一使用。
- Success / Warning / Error 仅用于语义状态反馈，不作为装饰色。
- 层级通过表面材质（Mica / Acrylic / Surface）区分，不通过颜色深浅区分。

---

## 3. 排版体系

**字体：** Inter（Light / Regular / SemiBold）

| Token | Size | Weight | LineHeight | Tracking | Usage |
|-------|------|--------|------------|----------|-------|
| hero | 80px | Light (300) | 1.0 | -0.02em | 倒计时数字 |
| display | 40px | SemiBold (600) | 1.1 | -0.01em | 统计数字 |
| tagline | 21px | SemiBold (600) | 1.19 | 0 | 页面标题 |
| body | 17px | Regular (400) | 1.47 | -0.01em | 正文 |
| caption | 14px | Regular (400) | 1.43 | 0 | 辅助文字 |
| fine-print | 12px | Regular (400) | 1.0 | 0 | 页脚 |

**排版规则：**

- hero 仅用于倒计时数字，Light 字重赋予数字呼吸感，避免粗体带来的压迫感。
- display 用于统计页面的数字呈现，SemiBold 确保数字在卡片中足够醒目。
- body 保持 17px 而非 16px，延续 Apple 式阅读节奏。
- 不使用 weight 500，字重梯度为 300 / 400 / 600。

---

## 4. 窗口架构

- **窗口类型：** FluentWindow + Mica 背景
- **窗口尺寸：** 520 x 640（固定，不可拉伸）
- **导航结构：** 底部 NavigationView Tab Bar，4 个 Tab
  - 计时器
  - 任务
  - 统计
  - 设置
- **页面模型：** 4 个 Page 替代独立窗口，通过 NavigationView 切换

**布局原则：**

- 窗口内容区垂直居中，水平方向保持对称留白。
- 计时器页面：倒计时数字居中，操作按钮在数字下方，状态指示在数字上方。
- 任务页面：任务列表占满内容区，底部预留添加入口。
- 统计页面：统计卡片网格布局，数字使用 display 排版。
- 设置页面：Label + Control 网格布局，分组清晰。

---

## 5. 组件规范

### 计时器

- 倒计时数字：hero 排版（80px Inter Light），居中显示
- 进度条：2px 高度，Primary 色，围绕数字区域或沿容器边缘
- 操作按钮：状态驱动
  - 空闲态：显示"开始专注"
  - 运行态：显示"暂停"
  - 暂停态：显示"继续" + "放弃"
  - 完成态：显示"开始休息" / "开始专注"

### 按钮

使用 WPF-UI Fluent 按钮体系：

| 变体 | 用途 | 样式 |
|------|------|------|
| Primary | 主要操作（开始/继续） | Primary 填充色，白色文字 |
| Secondary | 次要操作（暂停/放弃） | ControlBg 填充，Ink 文字 |
| Danger | 破坏性操作（删除任务） | Error 填充，白色文字 |

所有按钮按下时使用 Scale 0.95 缩放反馈。

### 卡片

使用 WPF-UI CardControl：

- 背景：Surface
- 圆角：8px
- 内边距：16px
- 无阴影，通过表面材质与 Canvas 背景自然区分层级
- 卡片间距：12px

### 导航

使用 NavigationView 底部 Tab Bar：

- 4 个 Tab：计时器 / 任务 / 统计 / 设置
- 当前 Tab 使用 Primary 色图标 + 文字
- 非 Current Tab 使用 Muted 色图标 + 文字
- 切换时使用 NavigationView 内置过渡动画

### 设置项

Label + Control 网格布局：

- 左列：设置项名称（body 排版）+ 描述（caption 排版，Muted 色）
- 右列：控件（ToggleSwitch / ComboBox / NumberBox）
- 行间距：20px
- 分组之间使用 Hairline 分割线

---

## 6. 动效规范

| 场景 | 效果 | 参数 |
|------|------|------|
| 启动 | 淡入 | Opacity 0→1, 0.3s, EaseOut |
| 专注完成 | 时间呼吸 | Opacity 1→0.5→1, 3s 循环 |
| 摄像头提醒 | 圆点呼吸 | Opacity 1→0.3→1, 2s 循环 |
| 暂停 | 时间微缩放 | Scale 1→0.97→1, 4s 循环 |
| 按钮按下 | 缩放反馈 | Scale 0.95, 0.08s |
| 页面切换 | 淡入 | NavigationView 内置过渡 |

**禁止：**

- 大幅抖动
- 廉价闪烁
- 粒子特效
- 影响操作响应的复杂动画

**动效原则：** 每一段动画都必须传达状态变化的意义。呼吸动画表示"等待注意"，缩放反馈表示"操作已接收"，淡入表示"内容就绪"。无意义的装饰性动画一律不做。

---

## 7. Do's and Don'ts

### Do

- 保持单一交互色（Primary），所有可操作元素统一使用
- 大量留白，让计时器数字成为页面唯一视觉焦点
- 状态驱动 UI 变化，界面随计时状态自然切换
- 使用 Mica / Acrylic 玻璃效果营造层次感
- 动画克制且有意义，每一段动画都传达状态信息
- 使用表面材质（Mica / Surface / Canvas）区分层级

### Don't

- 不要使用多种强调色，Primary 是唯一的交互色
- 不要使用阴影区分层级，用表面材质区分
- 不要弹窗打断流程，使用内联状态提示替代
- 不要在计时器页面放置过多信息，只保留倒计时、状态和操作
- 不要使用装饰性动画，每一段动效都必须有明确的状态传达目的
