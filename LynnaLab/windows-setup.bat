@echo off
setlocal


rem   Executing this file will set up everything required to use LynnaLab, in
rem   particular downloading oracles-disasm and the dependencies needed to
rem   build it. MSYS2 must be installed first.


set "msys_path=C:\msys64"

if not exist "%msys_path%\msys2_shell.cmd" (
    echo MSYS2 does not appear to be installed. Install it with default settings, then rerun this script.
    echo https://www.msys2.org/
    pause
    exit /b 1
)

call :RunMsysCommand "./build-setup.sh"

pause

goto :eof


rem Run a bash command in an MSYS UCRT64 environment.
:RunMsysCommand
call %msys_path%\msys2_shell.cmd -defterm -no-start -ucrt64 -here -shell bash -c "%~1"
goto :eof

endlocal
