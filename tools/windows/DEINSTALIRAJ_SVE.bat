@echo off
setlocal
chcp 65001 >nul
rem Vanjsko uredjenje PRO - uklanja AutoCAD i SketchUp dodatak.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0instaliraj_sve.ps1" -Deinstaliraj %*
set RC=%ERRORLEVEL%
echo.
pause
exit /b %RC%
