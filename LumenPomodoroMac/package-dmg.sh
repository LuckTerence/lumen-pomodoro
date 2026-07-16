#!/usr/bin/env bash
# 构建 LumenPomodoroMac.app 并打出可分发的 DMG（无签名 / 可选签名）
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MAC_DIR="$ROOT/LumenPomodoroMac"
BUILD_DIR="$MAC_DIR/build"
DIST_DIR="$MAC_DIR/dist"
APP_NAME="Lumen Pomodoro"
# 与 Bundle 显示名区分：磁盘上的 .app 名
APP_BUNDLE_NAME="LumenPomodoroMac.app"
VERSION="${VERSION:-$(grep -E '<Version>' "$ROOT/Directory.Build.props" | sed -E 's/.*<Version>([^<]+)<.*/\1/' || echo "0.4.0")}"
DMG_NAME="LumenPomodoro-mac-${VERSION}"
VOL_NAME="Lumen Pomodoro"
SIGN_IDENTITY="${CODESIGN_IDENTITY:-}"

echo "==> Version: $VERSION"
echo "==> Building app..."

bash "$MAC_DIR/build-mac.sh"

# 优先 Xcode 产物，其次 SPM 可执行文件包装
APP_PATH="$BUILD_DIR/DerivedData/Build/Products/Release/LumenPomodoroMac.app"
if [[ ! -d "$APP_PATH" ]]; then
  # SPM 产物路径因 arch/triple 而异，统一 find
  SPM_BIN="$(find "$MAC_DIR/.build" -type f -name LumenPomodoroMac ! -name '*.o' ! -name '*.swiftmodule' 2>/dev/null | while read -r f; do
    [[ -x "$f" ]] && file "$f" 2>/dev/null | grep -q 'executable' && echo "$f" && break
  done | head -1)"
  if [[ -z "${SPM_BIN:-}" || ! -x "$SPM_BIN" ]]; then
    # 常见路径兜底
    for cand in \
      "$MAC_DIR/.build/release/LumenPomodoroMac" \
      "$MAC_DIR/.build/arm64-apple-macosx/release/LumenPomodoroMac" \
      "$MAC_DIR/.build/x86_64-apple-macosx/release/LumenPomodoroMac"; do
      if [[ -x "$cand" ]]; then SPM_BIN="$cand"; break; fi
    done
  fi
  if [[ -z "${SPM_BIN:-}" || ! -x "$SPM_BIN" ]]; then
    echo "ERROR: neither Xcode app nor SPM binary found under .build"
    find "$MAC_DIR/.build" -name 'LumenPomodoroMac*' 2>/dev/null | head -20 || true
    exit 1
  fi
  echo "==> Packaging SPM binary into .app bundle: $SPM_BIN"
  APP_PATH="$BUILD_DIR/$APP_BUNDLE_NAME"
  rm -rf "$APP_PATH"
  mkdir -p "$APP_PATH/Contents/MacOS" "$APP_PATH/Contents/Resources"
  cp "$SPM_BIN" "$APP_PATH/Contents/MacOS/LumenPomodoroMac"
  chmod +x "$APP_PATH/Contents/MacOS/LumenPomodoroMac"
  PLIST_SRC="$MAC_DIR/LumenPomodoroMac/Info.plist"
  if [[ -f "$PLIST_SRC" ]]; then
    cp "$PLIST_SRC" "$APP_PATH/Contents/Info.plist"
  else
    cat > "$APP_PATH/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>LumenPomodoroMac</string>
  <key>CFBundleIdentifier</key><string>com.luckterence.lumenpomodoro.mac</string>
  <key>CFBundleName</key><string>${APP_NAME}</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>LSMinimumSystemVersion</key><string>14.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSCameraUsageDescription</key>
  <string>仅用于点亮摄像头指示灯作为休息提醒，不保存或上传画面。</string>
</dict>
</plist>
PLIST
  fi
fi

# 展示名：复制到暂存目录并重命名为友好名称
STAGE="$BUILD_DIR/dmg-stage"
rm -rf "$STAGE"
mkdir -p "$STAGE"
cp -R "$APP_PATH" "$STAGE/${APP_NAME}.app"

if [[ -n "$SIGN_IDENTITY" ]]; then
  echo "==> Codesign with: $SIGN_IDENTITY"
  codesign --force --deep --options runtime --sign "$SIGN_IDENTITY" "$STAGE/${APP_NAME}.app"
  codesign --verify --deep --strict "$STAGE/${APP_NAME}.app" || true
else
  echo "==> Skip codesign (set CODESIGN_IDENTITY to enable)"
fi

# Applications 快捷方式
ln -sf /Applications "$STAGE/Applications"

mkdir -p "$DIST_DIR"
DMG_PATH="$DIST_DIR/${DMG_NAME}.dmg"
rm -f "$DMG_PATH" "$DIST_DIR/${DMG_NAME}-rw.dmg"

echo "==> Creating DMG..."
hdiutil create \
  -volname "$VOL_NAME" \
  -srcfolder "$STAGE" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

# 校验
hdiutil verify "$DMG_PATH" >/dev/null

SIZE=$(du -h "$DMG_PATH" | awk '{print $1}')
echo ""
echo "Done."
echo "  App:  $STAGE/${APP_NAME}.app"
echo "  DMG:  $DMG_PATH  ($SIZE)"
echo ""
echo "Install: open the DMG and drag «${APP_NAME}» to Applications."
echo "Unsigned builds may need: right-click → Open (Gatekeeper)."
