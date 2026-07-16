# Lumen Pomodoro v0.4.2

**摄像头指示灯番茄钟** · 本地优先 · Windows + macOS

专注结束点亮硬件指示灯，备考时也能被提醒休息——不拍照、不录像、不上传。

## 下载

| 平台 | 文件 | 说明 |
|------|------|------|
| **Windows x64** | `LumenPomodoro.zip` | 解压后运行 `LumenPomodoro.exe`（自包含） |
| **macOS 14+** | `LumenPomodoro-mac-0.4.2.dmg` | 拖到「应用程序」；若拦截请右键打开 |

## 本版修复与优化

- **CI / 发版**：Windows 发布 restore 带 `win-x64`，修复 `NETSDK1047`；测试项目补全 `Settings` 引用  
- **Windows**：ViewModel 事件正确解绑（Dispose 不再泄漏/假解绑）；Fire-and-forget 告警清理  
- **macOS**：专注完成后评分/笔记会写回会话；摄像头失败弹出系统设置入口  
- **性能**：Mac 会话缓存 + 连胜计算只读盘一次；统计侧 Observable 字段访问修正  
- **稳定性**：FocusGuard 重启更稳健；SDK 用 `global.json` 固定 8.x 工具链  

## 功能回顾（v0.4 系列）

- 首次引导与三场景预设（轻松 / 标准 / 严格）  
- FocusGuard 防抖、上限、勿扰；全屏休息；严格模式  
- 结束前预告、计时中退出确认  

## 隐私

- 不保存照片 · 不录制视频 · 不上传画面  
- 摄像头仅用于点亮本机指示灯  

## 反馈

- [摄像头灯问题](https://github.com/LuckTerence/lumen-pomodoro/issues/new?template=camera_led.yml)  
- [防走神问题](https://github.com/LuckTerence/lumen-pomodoro/issues/new?template=focus_guard.yml)  

完整说明见仓库 `docs/release-v0.4.2.md`、`docs/core-path-checklist.md`。
