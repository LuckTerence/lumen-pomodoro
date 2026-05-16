# 贡献指南

感谢你对 Lumen Pomodoro 的关注。

## 开发环境

- Windows 10 1809+ 或 Windows 11
- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)

## 本地开发

```bash
# 克隆仓库
git clone https://github.com/YOUR_USERNAME/lumen-pomodoro.git
cd lumen-pomodoro

# 运行
dotnet run --project LumenPomodoro

# 运行测试
dotnet test LumenPomodoro.Tests

# 生成发布包
Publish-LumenPomodoro.cmd
```

## 提交 PR

1. Fork 本仓库并创建分支
2. 确保 `dotnet build` 和 `dotnet test` 通过
3. 提交 PR，填写模板中的检查项
4. PR 标题遵循 [Conventional Commits](https://www.conventionalcommits.org/)，如 `fix(timer): 修复暂停后恢复时间不正确`

## 项目结构

```
LumenPomodoro/
├── Controls/        # 自定义控件
├── Converters/      # XAML 值转换器
├── Models/          # 数据模型
├── Services/        # 核心服务（摄像头、计时器、存储等）
├── ViewModels/      # MVVM ViewModel
├── Views/           # XAML 视图
└── Themes/          # 自定义样式
```

## 行为准则

请参阅 [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)（如有）。
