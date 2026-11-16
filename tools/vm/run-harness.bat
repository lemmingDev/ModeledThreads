@echo off
rem Runs the geometry harness and keeps its whole output for reading from the host.
cd /d C:\Temp\harness
ThreadHarness.exe > C:\Temp\harness.txt 2>&1
echo exit %errorlevel%>> C:\Temp\harness.txt
