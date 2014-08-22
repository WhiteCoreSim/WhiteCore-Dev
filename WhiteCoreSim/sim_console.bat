@ECHO OFF

ECHO =======================================
ECHO Starting WhiteCore Standalone Sim . . .
ECHO =======================================

chdir /D  %~dp0
cd .\bin
.\WhiteCore.exe -skipconfig
cd ..
Echo.
Echo WhiteCore stopped . . .

set /p nothing= Enter to continue
exit
