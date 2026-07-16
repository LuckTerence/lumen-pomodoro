#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MAC_DIR="$ROOT/LumenPomodoroMac"
PROJECT="$MAC_DIR/LumenPomodoroMac.xcodeproj"
SCHEME="LumenPomodoroMac"
BUILD_DIR="$MAC_DIR/build"

bash "$MAC_DIR/generate-xcodeproj.sh"

build_with_xcode() {
  xcodebuild -version >/dev/null 2>&1 || return 1
  # 生成 shared scheme，避免 CI 找不到 scheme
  mkdir -p "$PROJECT/xcshareddata/xcschemes"
  cat > "$PROJECT/xcshareddata/xcschemes/${SCHEME}.xcscheme" <<'SCHEME'
<?xml version="1.0" encoding="UTF-8"?>
<Scheme LastUpgradeVersion = "1600" version = "1.7">
  <BuildAction parallelizeBuildables = "YES" buildImplicitDependencies = "YES">
    <BuildActionEntries>
      <BuildActionEntry buildForTesting = "YES" buildForRunning = "YES" buildForProfiling = "YES" buildForArchiving = "YES" buildForAnalyzing = "YES">
        <BuildableReference BuildableIdentifier = "primary" BlueprintIdentifier = "A60000000000000000000001" BuildableName = "LumenPomodoroMac.app" BlueprintName = "LumenPomodoroMac" ReferencedContainer = "container:LumenPomodoroMac.xcodeproj"/>
      </BuildActionEntry>
    </BuildActionEntries>
  </BuildAction>
  <LaunchAction buildConfiguration = "Release" selectedDebuggerIdentifier = "" selectedLauncherIdentifier = "Xcode.IDEFoundation.Launcher.PosixSpawn" launchStyle = "0" useCustomWorkingDirectory = "NO" ignoresPersistentStateOnLaunch = "NO" debugDocumentVersioning = "YES" debugServiceExtension = "internal" allowLocationSimulation = "YES">
    <BuildableProductRunnable runnableDebuggingMode = "0">
      <BuildableReference BuildableIdentifier = "primary" BlueprintIdentifier = "A60000000000000000000001" BuildableName = "LumenPomodoroMac.app" BlueprintName = "LumenPomodoroMac" ReferencedContainer = "container:LumenPomodoroMac.xcodeproj"/>
    </BuildableProductRunnable>
  </LaunchAction>
</Scheme>
SCHEME

  xcodebuild \
    -project "$PROJECT" \
    -scheme "$SCHEME" \
    -configuration Release \
    -derivedDataPath "$BUILD_DIR/DerivedData" \
    CODE_SIGNING_ALLOWED=NO \
    CODE_SIGN_IDENTITY=- \
    build
}

build_with_spm() {
  echo "Building with Swift Package Manager (release)..."
  cd "$MAC_DIR"
  swift build -c release
  BIN="$MAC_DIR/.build/release/LumenPomodoroMac"
  echo ""
  echo "Build succeeded:"
  echo "  $BIN"
}

if build_with_xcode; then
  APP_PATH="$BUILD_DIR/DerivedData/Build/Products/Release/LumenPomodoroMac.app"
  if [[ -d "$APP_PATH" ]]; then
    echo ""
    echo "Build succeeded:"
    echo "  $APP_PATH"
    exit 0
  fi
  echo "xcodebuild finished but .app missing; falling back to SPM..."
fi

build_with_spm
