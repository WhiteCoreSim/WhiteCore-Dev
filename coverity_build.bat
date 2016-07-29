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
rem      greythane (Rowan Deppeler) - May 2016
rem

echo ========================================
echo ====  WhiteCore Coverity build    ======
echo ========================================

rem ## Default "configuration" choice ((release, (debug)
set configuration=debug

rem ## Default Visual Studio edition
set vstudio=2013

rem ## Default Framework
set framework=4_5

rem ## End user selections ##

rem Determine current architecture in case
set bits=x86
if exist "%PROGRAMFILES(X86)%" (set bits=x64)

echo Creating solution 
Prebuild.exe /target vs2013 /targetframework v%framework% /conditionals ISWIN;NET_%framework%

echo Setting up for build
set fpath=C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\msbuild
if %bits%==x64 set args=/p:Platform=x64
if %bits%==x86 set args=/p:Platform=x86
if %configuration%==release set cfg=/p:Configuration=Release
if %configuration%==debug set cfg=/p:Configuration=Debug
set filename=Compile.WhiteCore.bat
echo %fpath% WhiteCore.sln %args% %cfg% > %filename% /p:DefineConstants="ISWIN;NET_%framework%

echo Let's do it...
..\cov-analysis\bin\cov-build.exe --dir cov-int Compile.WhiteCore.bat

echo Zip entire cov-int directory and upload to Coverity
echo Coverity build Finished

set /p nothing= Enter to continue
exit