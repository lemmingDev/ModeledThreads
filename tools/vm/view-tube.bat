@echo off
REM Shows the tube from a little above, so both threads are in view.
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Temp\view-part.ps1 -File C:\Temp\demo-tube.ipt -EyeX 24 -EyeY -34 -EyeZ 30 -TargetX 0 -TargetY 0 -TargetZ 15 > C:\Temp\view-tube.out.txt 2>&1
