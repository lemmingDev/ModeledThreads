@echo off
REM Runs the thread test harness in the user's desktop session, where Inventor
REM can start. Written by the build, run by a scheduled task.
cd /d C:\Temp\ThreadHarness
del /q C:\Temp\harness.txt 2>nul
ThreadHarness.exe > C:\Temp\harness.txt 2>&1
echo EXIT=%ERRORLEVEL%>> C:\Temp\harness.txt
