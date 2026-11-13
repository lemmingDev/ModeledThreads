@echo off
REM Shows the nut the dialog just threaded, from the same angle as the demo nut.
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Temp\view-part.ps1 -File C:\Temp\demo-nut-plain.ipt -EyeX 16 -EyeY -22 -EyeZ 26 -TargetX 0 -TargetY 0 -TargetZ 3.5 > C:\Temp\view-nut-dialog.out.txt 2>&1
