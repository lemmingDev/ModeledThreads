@echo off
for %%p in (12772 9068 2184) do (
  echo ==== %%p
  C:\Temp\ThreadDoctor\ThreadDoctor.exe %%p 2>&1 | findstr /C:"Inventor:" /C:"Documents:" /C:"'" /C:"No part" /C:"No Inventor"
) > C:\Temp\doctor-list.txt 2>&1
