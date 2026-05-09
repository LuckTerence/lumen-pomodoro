@echo off
setlocal

set "ROOT=%~dp0"
set "SOLUTION=%ROOT%LumenPomodoro.sln"
set "PUBLISH_EXE=%ROOT%publish\LumenPomodoro.exe"
set "APP_EXE=%ROOT%LumenPomodoro\bin\Release\net9.0-windows10.0.22621.0\LumenPomodoro.exe"

if exist "%PUBLISH_EXE%" (
    start "" "%PUBLISH_EXE%"
    exit /b 0
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo Lumen Pomodoro is not published yet.
    echo Please run Publish-LumenPomodoro.cmd on a machine with .NET SDK 9.0, then open publish\LumenPomodoro.exe.
    pause
    exit /b 1
)

echo First launch needs a Release build...
dotnet build "%SOLUTION%" --configuration Release
if errorlevel 1 (
    echo Build failed. Please check the error above.
    pause
    exit /b 1
)

if exist "%APP_EXE%" (
    start "" "%APP_EXE%"
    exit /b 0
)

echo Build finished, but the app executable was not found:
echo %APP_EXE%
pause
exit /b 1
