@ECHO OFF
rem Start the WhiteCore-Sim Region server
rem Version 0.9.5+
rem
rem August 2019 - Always run config at startup
rem greythane @ gmail.com

ECHO =======================================
ECHO Starting WhiteCore Grid Region . . .
ECHO =======================================

chdir /D  %~dp0
cd .\bin
.\WhiteCore.exe
cd ..
Echo.
Echo WhiteCore stopped . . .

set /p nothing= Enter to continue
exit
