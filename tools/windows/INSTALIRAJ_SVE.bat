@echo off
setlocal
chcp 65001 >nul
rem Vanjsko uredjenje PRO - instalira AutoCAD i SketchUp dodatak jednim klikom.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0instaliraj_sve.ps1" %*
set RC=%ERRORLEVEL%
echo.
pause
exit /b %RC%
