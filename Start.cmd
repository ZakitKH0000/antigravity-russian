@echo off
if not exist "%~dp0bin\AntigravityRussian.exe" (
    powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
    if errorlevel 1 (
        pause
        exit /b 1
    )
)
start "" "%~dp0bin\AntigravityRussian.exe"
