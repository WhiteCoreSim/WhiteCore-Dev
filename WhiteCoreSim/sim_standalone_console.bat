@ECHO OFF
rem Start the WhiteCore-Sim Standalone (same as the Region startup)
rem Version 0.9.6+
rem
rem June 2021 - Always run config at startup
rem greythane @ gmail.com

ECHO =======================================
ECHO Starting WhiteCore Standalone Sim . . .
ECHO =======================================

chdir /D  %~dp0
cd .\bin
.\WhiteCore.exe
cd ..
Echo.
Echo WhiteCore stopped . . .

set /p nothing= Enter to continue
exit
