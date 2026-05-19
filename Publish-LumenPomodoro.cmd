@echo off
setlocal

set "ROOT=%~dp0"
set "PUBLISH_DIR=%ROOT%publish"
set "APP_DIR=%PUBLISH_DIR%\app"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK not found.
    pause
    exit /b 1
)

echo === Step 1/2: Publish main app (framework-dependent) ===
dotnet publish "%ROOT%LumenPomodoro\LumenPomodoro.csproj" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    --output "%APP_DIR%"

if errorlevel 1 (
    echo Main app publish failed.
    pause
    exit /b 1
)

echo.
echo === Step 2/2: Publish launcher (Native AOT) ===

where vswhere.exe >nul 2>nul
if errorlevel 1 (
    echo vswhere.exe is required for AOT publish.
    echo Download from: https://github.com/microsoft/vswhere/releases
    pause
    exit /b 1
)

dotnet publish "%ROOT%Launcher\Launcher.csproj" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    -p:PublishAot=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    --output "%PUBLISH_DIR%"

if errorlevel 1 (
    echo Launcher publish failed.
    pause
    exit /b 1
)

echo.
echo === Done ===
echo.
echo Output:
dir "%PUBLISH_DIR%\*.exe" | findstr "exe"
dir "%APP_DIR%\LumenPomodoro.exe" | findstr "exe"
echo.
echo Total size:
powershell -Command "$app=(Get-Item '%APP_DIR%\LumenPomodoro.exe').Length; $launcher=(Get-Item '%PUBLISH_DIR%\LumenPomodoro.exe').Length; Write-Host ('Launcher: {0:N0} KB' -f ($launcher/1KB)); Write-Host ('App:     {0:N0} KB' -f ($app/1KB)); Write-Host ('Total:   {0:N0} KB' -f (($launcher+$app)/1KB))"
echo.
echo Users run: %PUBLISH_DIR%\LumenPomodoro.exe
echo (auto-installs .NET Runtime if needed)

if /i not "%~1"=="--no-pause" pause
exit /b 0
