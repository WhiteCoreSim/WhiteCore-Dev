@ECHO OFF

echo ====================================
echo ==== WhiteCore ========================
echo ====================================
echo.

rem ## Default Course of Action (WhiteCore,server,config,quit)
set choice=WhiteCore

rem ## Auto-restart on exit/crash (y,n)
set auto_restart=y

rem ## Pause on crash/exit (y,n)
set auto_pause=y

echo Welcome to the WhiteCore launcher.
if %auto_restart%==y echo I am configured to automatically restart on exit.
if %auto_pause%==y echo I am configured to automatically pause on exit.
echo You can edit this batch file to change your default choices.
echo.
echo You have the following choices:
echo	- WhiteCore: Launches WhiteCore
echo	- server: Launches WhiteCore Grid services
echo	- quit: Quits
echo.

:action
set /p choice="What would you like to do? (WhiteCore, server, config, quit) [%choice%]: "
if %choice%==WhiteCore (
	set app="WhiteCore.exe"
	goto launchcycle
)
if %choice%==server (
	set app="WhiteCore.Server.exe"
	goto launchcycle
)
if %choice%==quit goto eof
if %choice%==q goto eof
if %choice%==exit goto eof

echo "%choice%" isn't a valid choice!
goto action


:launchcycle
echo.
echo Launching %app%...
%app%
if %auto_pause%==y pause
if %auto_restart%==y goto launchcycle

:eof
pause
