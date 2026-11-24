@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Launch.ps1"
echo.
echo Diagnostic log: %LOCALAPPDATA%\HudWall\launcher.log
pause
