@echo off
REM Runs the "press Create Thread" test in the user's session.
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Temp\press-create.ps1 > C:\Temp\press-create.out.txt 2>&1
echo EXIT=%ERRORLEVEL%>> C:\Temp\press-create.out.txt
