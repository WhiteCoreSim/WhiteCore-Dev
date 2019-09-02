@ECHO OFF
rem  Coverity scan automated build for WhiteCore
rem 
rem This assumes that the Coverity standalone build package has been extracted into the directory
rem  above the main repo.  
rem
rem   repos (or where you keep everything)
rem        |--- cov-analysis
rem        | -- WhiteCore-Dev
rem
rem  The batch filename 'Compile.VS2010.net4_5.x64.debug.bat' is specific
rem    to the system and build requirements. Adjust as appropriate
rem
rem This file is assumed to be in the top level WhiteCore-Dev directory
rem
rem      greythane (Rowan Deppeler) - August 2019
rem

echo ========================================
echo ====  WhiteCore Coverity build    ======
echo ========================================

rem ## Default "configuration" choice ((release, (debug)
set configuration=debug

rem ## Default Visual Studio edition
set vstudio=2015

rem ## Default Framework
set framework=4_7_2

rem ## End user selections ##

rem Determine current architecture in case
set bits=x86
if exist "%PROGRAMFILES(X86)%" (set bits=x64)

echo Creating solution 
Prebuild.exe /target vs2015 /targetframework v%framework% /conditionals ISWIN;NET_%framework%

echo Setting up for build
set FileName=Compile.WhiteCore.bat
setlocal ENABLEEXTENSIONS
set VALUE_NAME=MSBuildToolsPath
rem try find vs2017
if "%PROCESSOR_ARCHITECTURE%"=="x86" set PROGRAMS=%ProgramFiles%
if defined ProgramFiles(x86) set PROGRAMS=%ProgramFiles(x86)%

for %%e in (Enterprise Professional Community) do (

    if exist "%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\MSBuild.exe" (
        set fpath="%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\"
		goto :found
    )

    if exist "%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\MSBuild.exe" (
        set fpath="%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\"
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
    echo Found msbuild at %fpath%
    echo Creating build files
    rem @echo %fpath%\msbuild opensim.sln > compile.bat

	if %bits%==x64 (
		set args=/p:Platform=x64
	)
	if %bits%==x86 (
		set args=/p:Platform=x86
	)
	if %configuration%==release set cfg=/p:Configuration=Release
	if %configuration%==debug set cfg=/p:Configuration=Debug
	
	echo Creating %FileName%
	echo %fpath%\msbuild WhiteCore.sln %args% %cfg% > %FileName% /p:DefineConstants="ISWIN;NET_%framework%

	echo Let's do it...
	..\cov-analysis\bin\cov-build.exe --dir cov-int Compile.WhiteCore.bat

	echo Zip entire cov-int directory and upload to Coverity
	echo Coverity build Finished

:done
set /p nothing= Enter to continue
exit