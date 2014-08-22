@ECHO OFF


ECHO =======================================
ECHO Starting WhiteCore Grid servers . . .
ECHO =======================================

chdir /D  %~dp0
cd bin
WhiteCore.Server.exe -skipconfig
cd ..
Echo.
Echo WhiteCore grid servers stopped . . .

set /p nothing= Enter to continue
exit
