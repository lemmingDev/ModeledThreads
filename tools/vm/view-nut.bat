@echo off
REM Shows the demo nut: a hex M10 nut, seen from above so the thread inside is
REM in view.
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Temp\view-part.ps1 -File C:\Temp\demo-nut.ipt -EyeX 16 -EyeY -22 -EyeZ 26 -TargetX 0 -TargetY 0 -TargetZ 3.5 > C:\Temp\view-nut.out.txt 2>&1
