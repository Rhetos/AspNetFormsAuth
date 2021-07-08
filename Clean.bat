@FOR /F "delims=" %%i IN ('dir bin /s/b/ad') DO DEL /F/S/Q "%%i" >nul & RD /S/Q "%%i"
@FOR /F "delims=" %%i IN ('dir obj /s/b/ad') DO DEL /F/S/Q "%%i" >nul & RD /S/Q "%%i"
@REM Question mark is here to prevent an issue with 'dir' not listing the subfolders if there is a folder with the same name in the project root folder.
@FOR /F "delims=" %%i IN ('dir TestResult? /s/b/ad') DO DEL /F/S/Q "%%i" >nul & RD /S/Q "%%i"
@REM Empty line, to prevent this script from returning an error code if there are no folders found in the previous command.
