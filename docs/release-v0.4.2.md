# Lumen Pomodoro v0.4.2

在 v0.4.1 产品能力基础上的 **CI 修好 + 双端稳定性/性能小修**。

## 产物

| 平台 | 文件 |
|------|------|
| Windows x64 | `LumenPomodoro.zip`（自包含单文件） |
| macOS 14+ | `LumenPomodoro-mac-0.4.2.dmg` |

打 tag `v0.4.2` 后由 GitHub Actions 自动构建上传。

## 变更摘要

1. 修复 Windows Release 发布 `NETSDK1047`（restore 未带 win-x64）  
2. 修复 CI 测试编译失败（`FocusGuardServiceTests` 缺 `using Models`）  
3. Mac：评分/笔记事后持久化  
4. Mac：Storage 会话缓存与 streak 性能  
5. Win：MainViewModel 事件 Dispose 正确解绑  
6. FocusGuard 重复 Start 不再丢弃/双 Timer  
7. `global.json` 固定 .NET 8 SDK  

## 验收

见 `docs/core-path-checklist.md`。
