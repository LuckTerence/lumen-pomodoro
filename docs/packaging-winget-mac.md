# 发布：winget（Windows）与 DMG（macOS）

> 版本以 `Directory.Build.props` 的 `<Version>` 为准（当前建议 0.4.0+）。

---

## 1. Windows：GitHub Release + winget

### 1.1 打 zip 发布包

本地（Windows）：

```bat
Publish-LumenPomodoro.cmd --no-pause
```

产物：

- `publish/LumenPomodoro.exe` — 启动器  
- `publish/app/LumenPomodoro.exe` — WPF 主程序  

打 zip：

```powershell
Compress-Archive -Path publish\LumenPomodoro.exe,publish\app -DestinationPath LumenPomodoro.zip -Force
Get-FileHash .\LumenPomodoro.zip -Algorithm SHA256
```

或推送 tag 走 CI：

```bash
git tag v0.4.0
git push origin v0.4.0
```

`.github/workflows/release.yml` 会在 Windows runner 上生成 `LumenPomodoro.zip` 并创建 GitHub Release。

### 1.2 winget 清单

模板目录：

```text
packaging/winget/LuckTerence.LumenPomodoro/
  LuckTerence.LumenPomodoro.yaml
  LuckTerence.LumenPomodoro.installer.yaml
  LuckTerence.LumenPomodoro.locale.zh-CN.yaml
  LuckTerence.LumenPomodoro.locale.en-US.yaml
```

**每次发版必改 installer 清单：**

| 字段 | 说明 |
|------|------|
| `PackageVersion` | 与 tag 一致，如 `0.4.0` |
| `InstallerUrl` | Release 上 zip 直链 |
| `InstallerSha256` | zip 的 SHA256（大写或小写均可，建议小写） |
| `ReleaseDate` | 发布日 |

校验：

```powershell
winget validate packaging\winget\LuckTerence.LumenPomodoro
winget install --manifest packaging\winget\LuckTerence.LumenPomodoro
```

### 1.3 提交到官方源

1. Fork [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs)  
2. 按路径放入清单，例如：  
   `manifests/l/LuckTerence/LumenPomodoro/0.4.0/`  
3. 打开 PR（可用 [Windows Package Manager Manifest Creator](https://github.com/microsoft/winget-create)）  
4. 合并后用户可：

```bat
winget install LuckTerence.LumenPomodoro
```

> 依赖声明了 `Microsoft.DotNet.DesktopRuntime.8`；若启动器已改用 .NET 9，请同步改 installer 依赖。

---

## 2. macOS：App + DMG

### 2.1 一键打包

在 macOS 上：

```bash
cd LumenPomodoroMac
chmod +x build-mac.sh package-dmg.sh
./package-dmg.sh
```

环境变量：

| 变量 | 说明 |
|------|------|
| `VERSION` | 覆盖版本号（默认读 `Directory.Build.props`） |
| `CODESIGN_IDENTITY` | 如 `Developer ID Application: Your Name (TEAMID)`，有证书时签名 |

产物：

```text
LumenPomodoroMac/dist/LumenPomodoro-mac-<version>.dmg
```

### 2.2 安装与 Gatekeeper

- 未签名：用户需 **右键 → 打开**，或系统设置 → 隐私与安全性 → 仍要打开  
- 已签名 + 公证（`notarytool`）后体验最佳（需 Apple Developer 账号，本脚本未强制公证）

### 2.3 可选：挂到 GitHub Release

在 macOS runner 或本机上传：

```bash
gh release upload v0.4.0 LumenPomodoroMac/dist/LumenPomodoro-mac-0.4.0.dmg
```

多平台 CI 示例见 `.github/workflows/release.yml`（含 macOS job 时）。

---

## 3. 发版检查清单

- [ ] 更新 `Directory.Build.props` 版本  
- [ ] Windows：`Publish-LumenPomodoro.cmd` / tag CI 产出 zip  
- [ ] 记录 zip SHA256，更新 winget installer 清单  
- [ ] macOS：`package-dmg.sh` 产出 dmg  
- [ ] GitHub Release 附上 `LumenPomodoro.zip` + `LumenPomodoro-mac-*.dmg`  
- [ ] README 下载说明指向最新 Release  
- [ ] （可选）winget-pkgs PR  
