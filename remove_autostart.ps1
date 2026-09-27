Unregister-ScheduledTask -TaskName 'RGB Auto Off' -Confirm:$false -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName 'RGB Auto Off Watchdog' -Confirm:$false -ErrorAction SilentlyContinue
Stop-Process -Name RgbAuto -Force -ErrorAction SilentlyContinue
Write-Host "RGB Auto Off 已停用并退出（含看门狗）。（重新启用请运行 setup_autostart.ps1）"
