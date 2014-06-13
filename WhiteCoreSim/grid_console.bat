@ECHO OFF


ECHO =======================================
ECHO Starting WhiteCore Grid servers . . .
ECHO =======================================
cd .\bin
.\WhiteCore.server.exe -skipconfig
cd ..
Echo.
Echo WhiteCore grid servers stopped . . .

exit
