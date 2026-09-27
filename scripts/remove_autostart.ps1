Unregister-ScheduledTask -TaskName 'RGB Auto Off' -Confirm:$false -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName 'RGB Auto Off Watchdog' -Confirm:$false -ErrorAction SilentlyContinue
Stop-Process -Name RgbAuto -Force -ErrorAction SilentlyContinue
Write-Host "RGB Auto Off stopped and removed (daemon, watchdog, autostart). Re-enable with scripts\setup_autostart.ps1"
