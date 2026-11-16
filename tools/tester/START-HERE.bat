@echo off
REM One click: installs the add-in, opens Inventor, runs the checks and leaves
REM the add-in installed. Close Inventor first if it is open.

setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-here.ps1"

echo.
echo Send the newest file in the report folder back to whoever gave you this.
echo.
pause
