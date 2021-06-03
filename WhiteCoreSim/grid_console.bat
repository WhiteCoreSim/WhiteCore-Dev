@ECHO OFF
rem Start the WhiteCore-Sim Grid server
rem Version 0.9.6+
rem
rem June 2021 - Always run config at startup
rem greythane @ gmail.com


ECHO =======================================
ECHO Starting WhiteCore Grid servers . . .
ECHO =======================================

chdir /D  %~dp0
cd bin
WhiteCore.Server.exe
cd ..
Echo.
Echo WhiteCore Grid servers stopped . . .

set /p nothing= Enter to continue
exit
