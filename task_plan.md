# 截图 UI Bug 修复计划

## 目标
修复用户截图中可确认的桌面 UI 问题，重点处理深色窗口中下拉框白底白字、统计页小窗口裁切和布局不稳。

## 当前阶段
Phase 1

## 阶段

### Phase 1: 确认根因
- [x] 读取 README、docs/dev_log、页面 XAML、全局样式和页面 code-behind
- [x] 对照截图确认主要问题集中在 ComboBox 原生浅色模板与统计页宽度约束
- **状态:** complete

### Phase 2: 手术式修复
- [x] 修复全局 ComboBox / ComboBoxItem 深色模板
- [x] 修复任务页颜色选择器、统计页周期选择器、设置页多个下拉框的显示一致性
- [x] 收紧统计页内容宽度，避免热力图在 480px 窗口下被右侧裁切
- **状态:** complete

### Phase 3: 验证与固化
- [x] dotnet build
- [x] dotnet test
- [x] 更新 docs/dev_log.md
- [x] git add + conventional commit
- **状态:** complete

## 关键假设
| 假设 | 验证方式 |
|------|----------|
| 下拉框白底白字来自原生 WPF ComboBox 默认模板未适配深色主题 | 检查 CustomStyles.xaml 与各页面 ComboBox 用法 |
| 统计页裁切来自内容 MaxWidth 过大且热力图控件缺少小窗口约束 | 检查 StatsPage.xaml 和 HeatmapCalendar 控件 |

## 错误记录
| 错误 | 尝试 | 解决方案 |
|------|------|----------|
| terminal-executor skill 不可用 | 检查当前可用 skill 列表 | 改用 PowerShell 真实执行命令并记录 |
| 默认 Debug 输出被运行中 LumenPomodoro.exe 锁定 | `dotnet build LumenPomodoro.sln`、直接 `dotnet test` | 使用独立输出目录编译，测试用 `--no-build` 跑已构建测试程序集 |
