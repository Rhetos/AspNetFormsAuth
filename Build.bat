SETLOCAL
SET Version=2.2.0
SET Prerelease=auto

IF NOT DEFINED VisualStudioVersion CALL "%VS140COMNTOOLS%VsDevCmd.bat" || ECHO ERROR: Cannot find Visual Studio 2015, missing VS140COMNTOOLS variable. && GOTO Error0
@ECHO ON

REM Packing the files with an older version of nuget.exe for backward compatibility (spaces in file names, https://github.com/Rhetos/Rhetos/issues/80).
IF NOT EXIST Install MD Install
IF NOT EXIST Install\NuGet.exe POWERSHELL (New-Object System.Net.WebClient).DownloadFile('https://dist.nuget.org/win-x86-commandline/v4.5.1/nuget.exe', 'Install\NuGet.exe') || GOTO Error0

PowerShell .\ChangeVersion.ps1 %Version% %Prerelease% || GOTO Error0
Install\NuGet.exe restore -NonInteractive || GOTO Error0
MSBuild /target:rebuild /p:Configuration=Debug /verbosity:minimal /fileLogger || GOTO Error0
Install\NuGet.exe pack -OutputDirectory Install || GOTO Error0
REM Updating the version of all projects back to "dev" (internal development build), to avoid spamming git history with timestamped prerelease versions.
PowerShell .\ChangeVersion.ps1 %Version% dev || GOTO Error0

@REM ================================================

@ECHO.
@ECHO %~nx0 SUCCESSFULLY COMPLETED.
@EXIT /B 0

:Error0
@ECHO.
@ECHO %~nx0 FAILED.
@IF /I [%1] NEQ [/NOPAUSE] @PAUSE
@EXIT /B 1
