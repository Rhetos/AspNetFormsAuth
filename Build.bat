SETLOCAL
SET Version=2.2.0
SET Prerelease=auto

IF NOT DEFINED VisualStudioVersion CALL "%VS140COMNTOOLS%VsDevCmd.bat" || ECHO ERROR: Cannot find Visual Studio 2015, missing VS140COMNTOOLS variable. && GOTO Error0
@ECHO ON

PowerShell .\ChangeVersion.ps1 %Version% %Prerelease% || GOTO Error0
WHERE /Q NuGet.exe || ECHO ERROR: Please download the NuGet.exe command line tool. && GOTO Error0
NuGet restore -NonInteractive || GOTO Error0
MSBuild /target:rebuild /p:Configuration=Debug /verbosity:minimal /fileLogger || GOTO Error0
IF NOT EXIST Install md Install
NuGet pack -o Install || GOTO Error0
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
