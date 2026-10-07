@echo off
REM double click to build. runs build.ps1 with ExecutionPolicy Bypass and keeps the window open
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
echo.
echo ============================================================
echo  Build finished. Look in the  dist  folder for Oblivion.exe
echo ============================================================
echo.
pause
