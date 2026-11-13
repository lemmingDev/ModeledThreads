@echo off
REM Threads the tube the way a user would: the outside first, then the inside,
REM each through the dialog, one after the other.
REM
REM Step 1 opens the part; the caller then runs the COM and press steps.

start "" "C:\Program Files\Autodesk\Inventor 2027\Bin\Inventor.exe" "C:\Temp\demo-tube-plain.ipt"
