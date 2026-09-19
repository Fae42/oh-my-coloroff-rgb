$task = 'RGB Auto Off'
Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
Stop-Process -Name RgbAuto -Force -ErrorAction SilentlyContinue
Write-Host "RGB Auto Off 已停用并退出。（重新启用请运行 setup_autostart.ps1 并手动启动 RgbAuto.exe）"
