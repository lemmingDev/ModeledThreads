@echo off
REM Modeled Threads - one-click installer for Windows.
REM Double-click this file to install the add-in for Autodesk Inventor 2025-2027.
REM Requires: Inventor closed. The add-in uses .NET Framework 4.8,
REM which ships with Windows, so there is nothing else to install.

setlocal
set "BUNDLE=%~dp0bundle\ModeledThreads.bundle"
set "TARGET=%APPDATA%\Autodesk\ApplicationPlugins\ModeledThreads.bundle"

if not exist "%BUNDLE%\PackageContents.xml" (
    echo ERROR: Bundle not found. Keep this file in the ModeledThreads folder.
    pause
    exit /b 1
)

tasklist /FI "IMAGENAME eq Inventor.exe" 2>NUL | find /I "Inventor.exe" >NUL
if %ERRORLEVEL%==0 (
    echo WARNING: Autodesk Inventor is running. Close it first, then run this installer again.
    pause
    exit /b 1
)

echo Installing Modeled Threads to:
echo   %TARGET%
echo.

if not exist "%APPDATA%\Autodesk\ApplicationPlugins" mkdir "%APPDATA%\Autodesk\ApplicationPlugins"

if exist "%TARGET%" rmdir /s /q "%TARGET%"

xcopy "%BUNDLE%" "%TARGET%\" /E /I /Y /Q

if not exist "%TARGET%\Contents\ModeledThreads.dll" (
    echo ERROR: Installation failed - the DLL is not there after the copy.
    pause
    exit /b 1
)

echo.
echo Installation complete.
echo.
echo Next steps:
echo   1. Start Autodesk Inventor (2025, 2026 or 2027).
echo   2. If an "Add-in Manager Security Alert" appears, click "Launch Add-in Manager",
echo      pick "Modeled Threads", click "Unblock", then OK. (Once per user.)
echo   3. Open a part, pick a cylindrical face and use the "Modeled Threads" ribbon tab.
echo.
pause
