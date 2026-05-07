@echo off
setlocal

set "ROOT=%~dp0"
set "SOLUTION=%ROOT%LumenPomodoro.sln"
set "APP_EXE=%ROOT%LumenPomodoro\bin\Release\net9.0-windows\LumenPomodoro.exe"

if exist "%APP_EXE%" (
    start "" "%APP_EXE%"
    exit /b 0
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK was not found. Please install .NET SDK 9.0 or build the app in Visual Studio.
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
