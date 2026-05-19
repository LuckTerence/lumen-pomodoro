# Lumen Pomodoro v0.2.0

这一版重点优化发布体积，修复小体积版本中的摄像头灯提醒，并重写公开说明。

## 下载方式

下载 `LumenPomodoro.zip`，解压后双击根目录的 `LumenPomodoro.exe`。

## 运行环境

发布包采用“小启动器 + 主程序”的结构：

- 根目录 `LumenPomodoro.exe` 是原生启动器
- `app/LumenPomodoro.exe` 是 WPF 主程序
- 首次运行会检测 .NET 9 Desktop Runtime
- 如果本机没有安装运行时，启动器会提示并自动下载安装

这样可以把发布包控制在约 16MB，避免把完整 .NET Desktop Runtime 打进应用包。

## 主要变化

- 发布包改为小体积 zip，不再使用大型自包含单文件
- 摄像头提醒改为 Windows Media Foundation 原生调用
- 修复小体积版本中摄像头指示灯无法点亮的问题
- 清理 Windows SDK 投影依赖，降低发布体积
- 修复 Inter 字体文件，替换为真实字体子集
- 重写 README，统一产品定位、隐私说明和下载方式

## 隐私说明

- 不保存照片
- 不录制视频
- 不上传摄像头数据
- 不展示摄像头画面
- 仅在本机调用摄像头硬件
- 用户确认后释放摄像头，或流程结束后自动释放

Full Changelog: `v0.1.0...v0.2.0`
