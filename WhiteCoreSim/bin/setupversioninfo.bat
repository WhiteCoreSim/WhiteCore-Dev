@echo off
setLocal EnableDelayedExpansion
IF NOT EXIST ..\.git GOTO DIE

for /f "tokens=* delims= " %%a in (..\.git\logs\HEAD) do (
set var=%%a
)
for /f "tokens=1-7" %%a in ("%var%") do (
  set commit= %%b
  set chars=!commit:~1,7!
  echo %DATE%-!chars!^ 1> .version
)

:DIE
