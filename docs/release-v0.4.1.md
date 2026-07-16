# Lumen Pomodoro v0.4.1

双端可用版本：Windows 主产品 + macOS 正式能力对齐，聚焦「摄像头灯休息提醒」与本地备考体验。

## 下载

| 平台 | 文件 |
|------|------|
| Windows x64 | `LumenPomodoro.zip` |
| macOS 14+ | `LumenPomodoro-mac-0.4.1.dmg` |

打 tag `v0.4.1` 后由 GitHub Actions 自动构建上传。

### Windows 安装

1. 解压 zip  
2. 运行根目录 `LumenPomodoro.exe`（启动器会检测 .NET Desktop Runtime）  
3. 首次启动完成引导（灯 / 隐私 / 场景预设）

```bat
winget install LuckTerence.LumenPomodoro
```

（需清单合并进 winget 源后；模板见 `packaging/winget/`）

### macOS 安装

1. 打开 dmg，拖到「应用程序」  
2. 若拦截：右键 → 打开  
3. 引导中按需授权摄像头 / 通知 / 辅助功能  

```bash
cd LumenPomodoroMac && ./package-dmg.sh
```

## 本版亮点

- **首次引导**：为什么用灯、隐私、轻松/标准/严格场景  
- **FocusGuard**：防抖、每会话提醒上限、遵从勿扰  
- **全屏休息 + 严格模式**；场景一键预设  
- **结束前预告**、**计时中退出确认**  
- **摄像头灯可读状态**；失败可打开系统隐私设置  
- **Mac 菜单栏**：倒计时、开始/暂停、休息、场景预设  
- **跨端契约文档**与发布脚手架（winget / dmg / CI）

## 隐私

- 不拍照、不录像、不上传  
- 数据仅本机 JSON  
- 无账号、无云同步  

## 验收清单

见 [core-path-checklist.md](./core-path-checklist.md)。

## 升级说明

从 v0.2.x 升级：设置文件会自动兼容新增字段（缺省即默认值）。建议备份 `%APPDATA%/LumenPomodoro/` 或 `~/Library/Application Support/LumenPomodoro/`。
