@echo off
setlocal
chcp 65001 >nul
rem Vanjsko uredjenje PRO - pokrece PowerShell skriptu instalacije (bez promjene sistemske ExecutionPolicy).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0instaliraj.ps1" -Deinstaliraj %*
set RC=%ERRORLEVEL%
echo.
pause
exit /b %RC%
