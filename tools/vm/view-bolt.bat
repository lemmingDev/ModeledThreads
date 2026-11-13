@echo off
REM Shows the demo bolt from a little above, so the thread and the lead-in are
REM both in view.
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Temp\view-part.ps1 -File C:\Temp\demo-bolt.ipt -EyeX 20 -EyeY -30 -EyeZ 26 -TargetX 0 -TargetY 0 -TargetZ 14 > C:\Temp\view-bolt.out.txt 2>&1
