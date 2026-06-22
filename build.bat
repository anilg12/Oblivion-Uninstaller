@echo off
REM Double-click this file to build Vanish.
REM It runs build.ps1 with the execution policy bypassed and keeps the window open.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
echo.
echo ============================================================
echo  Build finished. Look in the  dist  folder for Oblivion.exe
echo ============================================================
echo.
pause
