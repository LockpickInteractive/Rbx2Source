@echo off
color 7c
echo This program will automatically remove the Rbx2Source Cache folder.
:choice
set /P c=Would you like to clear the Rbx2Source Cache folder now [Y/N]?
if /I "%c%" EQU "Y" goto :execute
if /I "%c%" EQU "N" goto :end
goto :choice


:execute
pause
rd %LOCALAPPDATA%\Rbx2Source /s



:end
