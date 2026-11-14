@echo off
rem Asks a specific Inventor instance what it has open. Written to a file so a
rem wedged instance shows up as a missing result rather than a hanging console.
C:\Temp\ThreadDoctor\ThreadDoctor.exe 2104 > C:\Temp\doctor2104.txt 2>&1
echo exit=%ERRORLEVEL% >> C:\Temp\doctor2104.txt
