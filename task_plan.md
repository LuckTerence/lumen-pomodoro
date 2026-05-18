# 截图 UI Bug 修复计划

## 目标
修复用户截图中可确认的桌面 UI 问题，重点处理开始按钮与圆环中心轴不一致、深色窗口中下拉框白底白字/突兀，以及统计页小窗口裁切和布局不稳。

## 当前阶段
Phase 3

## 阶段

### Phase 1: 确认根因
- [x] 读取 README、docs/dev_log、页面 XAML、全局样式和页面 code-behind
- [x] 对照截图确认主要问题集中在 ComboBox 原生浅色模板与统计页宽度约束
- [x] 对照新截图确认开始按钮偏移来自计时核心区和按钮区没有共享固定中心参照
- **状态:** complete

### Phase 2: 手术式修复
- [x] 修复全局 ComboBox / ComboBoxItem 深色模板
- [x] 修复任务页颜色选择器、统计页周期选择器、设置页多个下拉框的显示一致性
- [x] 收紧统计页内容宽度，避免热力图在 480px 窗口下被右侧裁切
- [x] 将计时页核心区、操作按钮区和底部信息统一到 240px 宽度参照
- **状态:** complete

### Phase 3: 验证与固化
- [x] dotnet build 独立输出目录验证
- [x] dotnet test
- [x] 更新 docs/dev_log.md
- [x] git add + conventional commit
- **状态:** in_progress

## 关键假设
| 假设 | 验证方式 |
|------|----------|
| 下拉框白底白字来自原生 WPF ComboBox 默认模板未适配深色主题 | 检查 CustomStyles.xaml 与各页面 ComboBox 用法 |
| 统计页裁切来自内容 MaxWidth 过大且热力图控件缺少小窗口约束 | 检查 StatsPage.xaml 和 HeatmapCalendar 控件 |
| 开始按钮偏左来自按钮区按自身宽度居中，而不是按圆环宽度居中 | 检查 TimerPage.xaml 中核心计时 StackPanel / Grid 宽度 |

## 错误记录
| 错误 | 尝试 | 解决方案 |
|------|------|----------|
| terminal-executor skill 不可用 | 检查当前可用 skill 列表 | 改用 PowerShell 真实执行命令并记录 |
| 默认 Debug 输出被运行中 LumenPomodoro.exe 锁定 | `dotnet build LumenPomodoro.sln`、直接 `dotnet test` | 使用独立输出目录编译，测试用 `--no-build` 跑已构建测试程序集 |
| 本轮中间目录被权限/临时产物卡住 | `dotnet build -o .tmp-build`、自定义 BaseIntermediateOutputPath | 提升权限清理 `.tmp-obj` / `obj` / `C:\tmp\lumen-*`，再用 `-o C:\tmp\lumen-build -p:UseAppHost=false` 编译 |
