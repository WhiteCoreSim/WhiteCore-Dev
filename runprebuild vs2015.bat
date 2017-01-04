@ECHO OFF

echo ========================================
echo ==== WhiteCore Configuration ===========
echo ========================================

rem ## Default "configuration" choice ((r)elease, (d)ebug)
set configuration=d

rem ## Default "run compile batch" choice (y(es),n(o))
set compile_at_end=y

rem ## Default Visual Studio edition
set vstudio=2015

rem ## Default Framework
set framework=4_5

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
if exist Compile.*.bat (
    echo Deleting previous compile batch file...
    echo.
    del WhiteCore.sln
    del Compile.*.bat
)

pause

echo Calling Prebuild for target %vstudio% with framework %framework%...
Prebuild.exe /target vs2015 /targetframework v%framework% /conditionals ISWIN;NET_%framework%

echo.
echo Creating compile batch file for your convenience...
set fpath=C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\msbuild
if %bits%==x64 (
    set args=/p:Platform=x64
	set fpath=C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\msbuild
)
if %bits%==x86 (
	set args=/p:Platform=x86
	set fpath=C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\msbuild
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
