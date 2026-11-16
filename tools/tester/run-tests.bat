@echo off
REM Runs the automated checks and writes one report file in the report folder.
REM Start Inventor first, wait until it has loaded, then double-click this.

setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-tests.ps1"

echo.
echo Send the newest file in the report folder back to whoever gave you this.
echo.
pause
