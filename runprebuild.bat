@ECHO OFF

echo ========================================
echo ==== WhiteCore Configuration ===========
echo ========================================
echo.
echo If you wish to customize the configuration, re-run with the switch '-p'
echo   e.g.   runprebuild -p
echo.

rem ## Default "configuration" choice ((r)elease, (d)ebug)
set configuration=d

rem ## Default "run compile batch" choice (y(es),n(o))
set compile_at_end=y

rem ## Default Visual Studio edition
set vstudio=2010

rem ## Default Framework
set framework=4_0

rem ## Default architecture (86 (for 32bit), 64)
:CheckArch
set bits=x86
if exist "%PROGRAMFILES(X86)%" (set bits=x64)
if %bits% == x64 (
	echo Found 64bit architecture
)
if %bits% == x86 (
	echo Found 32 bit architecture
)

rem ## Determine native framework
:CheckOS
set framework=4_5
for /f "tokens=4-5 delims=. " %%i in ('ver') do set VERSION=%%i.%%j
if %version% == 10.0 (
	set framework=4_5
	echo Windows 10
)
if %version% == 6.3 (
	set framework=4_5
	echo Windows 8.1 or Server 2012 R2
)
if %version% == 6.2 (
	set framework=4_5
	echo Windows 8 or Server 2012
)
if %version% == 6.1 (
	set framework=4_0
	echo Windows 7 or Server 2008 R2
)
if %version% == 6.0 (
	set framework=3_5
	echo hmmm... Windows Vista or Server 2008
)
if %version% == 5.2 (
	set framework=3_5
	echo hmmm... Windows XP x64 or  Server 2003
)
if %version% == 5.1 (
	set framework=3_5
	echo hmmm... Windows XP
)


rem ## If not requested, skip the prompting
if "%1" =="" goto final
if %1 == -p goto prompt
if %1 == --prompt goto prompt
goto final

:prompt
echo I will now ask you four questions regarding your build.
echo However, if you wish to build for:
echo		%bits% Architecture
echo		.NET %framework%
echo		Visual Studio %vstudio%
if %compile_at_end% == y echo And you would like to compile straight after prebuild...
echo.
echo Simply tap [ENTER] three times.
echo.
echo Note that you can change these defaults by opening this
echo batch file in a text editor.
echo.

:bits
set /p bits="Choose your architecture (x86, x64) [%bits%]: "
if %bits%==86 goto configuration
if %bits%==x86 goto configuration
if %bits%==64 goto configuration
if %bits%==x64 goto configuration
echo "%bits%" isn't a valid choice!
goto bits

:configuration
set /p configuration="Choose your configuration ((r)elease or (d)ebug)? [%configuration%]: "
if %configuration%==r goto framework
if %configuration%==d goto framework
if %configuration%==release goto framework
if %configuration%==debug goto framework
echo "%configuration%" isn't a valid choice!
goto configuration

:framework
set /p framework="Choose your .NET framework (4_0, 4_5, 4.6)? [%framework%]: "
if %framework%==4_0 goto final
if %framework%==4_5 goto final
if %framework%==4_6 goto final
echo "%framework%" isn't a valid choice!
goto framework

:final
echo.
echo Configuring for %bits% architecture using %framework% .NET framework
echo.
echo.

if exist Compile.*.bat (
    echo Deleting previous compile batch file...
    echo.
    del Compile.*.bat
)
if %framework%==4_5 set %vstudio%=2012

echo Calling Prebuild for target %vstudio% with framework %framework%...
Prebuild.exe /target vs%vstudio% /targetframework v%framework% /conditionals ISWIN;NET_%framework%

echo.
echo Creating compile batch file for your convenience...
if %bits%==x64 (
    set args=/p:Platform=x64
	set fpath=%SystemDrive%\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\msbuild
)
if %bits%==x86 (
	set args=/p:Platform=x86
	set fpath=%SystemDrive%\WINDOWS\Microsoft.NET\Framework\v4.0.30319\msbuild
)
if %configuration%==r  (
    set cfg=/p:Configuration=Release
    set configuration=release
)
if %configuration%==d  (
	set cfg=/p:Configuration=Debug
	set configuration=debug
)
if %configuration%==release set cfg=/p:Configuration=Release
if %configuration%==debug set cfg=/p:Configuration=Debug
set filename=Compile.VS%vstudio%.net%framework%.%bits%.%configuration%.bat

echo %fpath% WhiteCore.sln %args% %cfg% > %filename% /p:DefineConstants="ISWIN;NET_%framework%"

echo.
set /p compile_at_end="Done, %filename% created. Compile now? (y,n) [%compile_at_end%]"
if %compile_at_end%==y (
    %filename%
    pause
)
