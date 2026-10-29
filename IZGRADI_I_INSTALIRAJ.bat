@echo off
setlocal
chcp 65001 >nul
rem Vanjsko uredjenje PRO - pokrece PowerShell skriptu instalacije (bez promjene sistemske ExecutionPolicy).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\windows\instaliraj.ps1" -Izgradi %*
set RC=%ERRORLEVEL%
echo.
pause
exit /b %RC%
