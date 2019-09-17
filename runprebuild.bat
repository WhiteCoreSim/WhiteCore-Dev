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
set vstudio=2015

rem ## Default Framework
set framework=4_0

rem ## Default architecture (86 (for 32bit), 64)
:CheckArch
set bits=x86
if exist "%PROGRAMFILES(X86)%" (set bits=x64)
if %bits% == x64 (
	echo Found 64bit architecture
    set args=/p:Platform=x64
)
if %bits% == x86 (
	echo Found 32 bit architecture
	set args=/p:Platform=x86
)

rem ## Determine native framework
:CheckOS
set framework=4_5
if %framework%==4_5 set %vstudio%=2012

for /f "tokens=4-5 delims=. " %%i in ('ver') do set VERSION=%%i.%%j
if %version% == 10.0 (
	set framework=4_7_2
	echo Windows 10
	rem ## As of April update 2018
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
	echo hmmm... mutter, mutter Windows XP
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
set /p framework="Choose your .NET framework (4.0, 4.5, 4.6, 4.7)? [%framework%]: "
if %framework%==4.0 goto final
if %framework%==4.5 goto final
if %framework%==4.6 goto final
if %framework%==4.7 goto final
echo "%framework%" isn't a valid choice!
goto framework

:final

if exist Compile.*.bat (
    echo Deleting previous compile batch file...
    echo.
    del Compile.*.bat
)


echo.
echo Setting up for build
set FileName=Compile.WhiteCore.bat
setlocal ENABLEEXTENSIONS
set VALUE_NAME=MSBuildToolsPath
rem try find vs2019,20177
if "%PROCESSOR_ARCHITECTURE%"=="x86" set PROGRAMS=%ProgramFiles%
if defined ProgramFiles(x86) set PROGRAMS=%ProgramFiles(x86)%

for %%e in (Enterprise Professional Community) do (

    if exist "%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\MSBuild.exe" (
        set fpath="%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\"
		rem set vstudio=2017
		goto :found
    )

    if exist "%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\MSBuild.exe" (
        set fpath="%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\"
		rem set vstudio=2019
		goto :found
    )
)

rem We have to use grep or find to locate the correct line, because reg query spits
rem out 4 lines before Windows 7 but 2 lines after Windows 7.
rem We use grep if it's on the path; otherwise we use the built-in find command
rem from Windows. (We must use grep on Cygwin because it overrides the "find" command.)

for %%X in (grep.exe) do (set FOUNDGREP=%%~$PATH:X)
if defined FOUNDGREP (
  set FINDCMD=grep
) else (
  set FINDCMD=find
)

rem try vs2015
FOR /F "usebackq tokens=1-3" %%A IN (`REG QUERY "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSBuild\ToolsVersions\14.0" /v %VALUE_NAME% 2^>nul ^| %FINDCMD% "%VALUE_NAME%"`) DO (
	set fpath=%%C
	goto :found
)

echo msbuild for at least VS2015 not found, please install a (Community) edition of VS2019, VS2017 or VS2015
echo Not creating %FileName%
if exist %FileName% (
	del %FileName%
	)
goto :done

:found
	echo Creating solution 
	echo Calling Prebuild for target VS%vstudio% with framework %framework%...
	Prebuild.exe /target vs%vstudio% /targetframework v%framework% /conditionals ISWIN;NET_%framework%
	echo.
    echo Found msbuild at %fpath%
	echo.
	echo Configuring for %bits% architecture using %framework% .NET framework
	echo.
	echo.
	set /p nothing= Enter to continue

    echo Creating build files

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

	echo Creating %FileName%
	echo %fpath%\msbuild WhiteCore.sln %args% %cfg% > %FileName% /p:DefineConstants="ISWIN;NET_%framework%
	
	echo.
	set /p compile_at_end="Done, %FileName% created. Compile now? (y,n) [%compile_at_end%]"
	if %compile_at_end%==y (
		%FileName%
		pause
)
