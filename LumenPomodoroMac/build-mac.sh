#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MAC_DIR="$ROOT/LumenPomodoroMac"
PROJECT="$MAC_DIR/LumenPomodoroMac.xcodeproj"
SCHEME="LumenPomodoroMac"
BUILD_DIR="$MAC_DIR/build"

bash "$MAC_DIR/generate-xcodeproj.sh"

if xcodebuild -version >/dev/null 2>&1; then
  xcodebuild \
    -project "$PROJECT" \
    -scheme "$SCHEME" \
    -configuration Release \
    -derivedDataPath "$BUILD_DIR/DerivedData" \
    CODE_SIGNING_ALLOWED=NO \
    build

  APP_PATH="$BUILD_DIR/DerivedData/Build/Products/Release/LumenPomodoroMac.app"
  if [[ -d "$APP_PATH" ]]; then
    echo ""
    echo "Build succeeded:"
    echo "  $APP_PATH"
    exit 0
  fi
fi

echo "Xcode not available, falling back to Swift Package Manager..."
cd "$MAC_DIR"
swift build -c release
BIN="$MAC_DIR/.build/release/LumenPomodoroMac"
echo ""
echo "Build succeeded:"
echo "  $BIN"
echo ""
echo "Run with: $MAC_DIR/run-dev.sh"

