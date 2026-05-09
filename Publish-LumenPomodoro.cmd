@echo off
setlocal

set "NO_PAUSE="
if /i "%~1"=="--no-pause" set "NO_PAUSE=1"

set "ROOT=%~dp0"
set "PROJECT=%ROOT%LumenPomodoro\LumenPomodoro.csproj"
set "PUBLISH_DIR=%ROOT%publish"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK was not found. Please install .NET SDK 9.0.
    if not defined NO_PAUSE pause
    exit /b 1
)

dotnet publish "%PROJECT%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    --output "%PUBLISH_DIR%"

if errorlevel 1 (
    echo Publish failed. Please check the error above.
    if not defined NO_PAUSE pause
    exit /b 1
)

echo.
echo Published successfully:
echo %PUBLISH_DIR%\LumenPomodoro.exe
echo.

set "DESKTOP_EXE=%USERPROFILE%\Desktop\LumenPomodoro.exe"
copy "%PUBLISH_DIR%\LumenPomodoro.exe" "%DESKTOP_EXE%" >nul
if errorlevel 1 (
    echo Failed to copy to desktop. You can find the app at:
    echo %PUBLISH_DIR%\LumenPomodoro.exe
) else (
    echo Copied to desktop: %DESKTOP_EXE%
)

if not defined NO_PAUSE pause
exit /b 0
