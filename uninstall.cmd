@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\remove_autostart.ps1"
if not "%ERRORLEVEL%"=="0" goto fail
exit /b 0

:fail
echo.
echo RGB Auto Off uninstall failed. See the error above.
pause
exit /b 1
