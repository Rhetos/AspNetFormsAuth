SETLOCAL
SET Version=3.1.0
SET Prerelease=auto

CALL Tools\Build\FindVisualStudio.bat || GOTO Error0

REM Packing the files with an older version of nuget.exe for backward compatibility (spaces in file names, https://github.com/Rhetos/Rhetos/issues/80).
IF NOT EXIST Install MD Install
IF NOT EXIST Install\NuGet.exe POWERSHELL (New-Object System.Net.WebClient).DownloadFile('https://dist.nuget.org/win-x86-commandline/v4.5.1/nuget.exe', 'Install\NuGet.exe') || GOTO Error0

REM Updating the build version.
PowerShell -ExecutionPolicy ByPass .\ChangeVersion.ps1 %Version% %Prerelease% || GOTO Error0

NuGet.exe restore -NonInteractive || GOTO Error0
MSBuild /target:rebuild /p:Configuration=Debug /verbosity:minimal /fileLogger || GOTO Error0
Install\NuGet.exe pack -OutputDirectory Install || GOTO Error0

REM Updating the build version back to "dev" (internal development build), to avoid spamming git history with timestamped prerelease versions.
PowerShell -ExecutionPolicy ByPass .\ChangeVersion.ps1 %Version% dev || GOTO Error0

@REM ================================================

@ECHO.
@ECHO %~nx0 SUCCESSFULLY COMPLETED.
@EXIT /B 0

:Error0
@ECHO.
@ECHO %~nx0 FAILED.
@IF /I [%1] NEQ [/NOPAUSE] @PAUSE
@EXIT /B 1
