@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup_autostart.ps1"
if not "%ERRORLEVEL%"=="0" goto fail
start "" /D "%~dp0bin" "%~dp0bin\RgbAuto.exe"
if not "%ERRORLEVEL%"=="0" goto fail
exit /b 0

:fail
echo.
echo RGB Auto Off installation failed. See the error above.
pause
exit /b 1
