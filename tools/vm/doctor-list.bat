@echo off
echo starting > C:\Temp\doctor-list.txt
for %%p in (4028 12772 9068 2184 14752) do (
  echo ==== %%p >> C:\Temp\doctor-list.txt
  C:\Temp\ThreadDoctor\ThreadDoctor.exe %%p >> C:\Temp\doctor-list.txt 2>&1
)
