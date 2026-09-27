$exe = 'D:\Software\Dev\RgbAuto\RgbAuto.exe'
$wd  = 'D:\Software\Dev\RgbAuto'
$user = [System.Environment]::UserName
$action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $wd
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
# No RestartCount: yield exits (5/6) are non-zero and would be misread as failure,
# relaunching the daemon while iGC.Lite owns the hardware.
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive
Register-ScheduledTask -TaskName 'RGB Auto Off' -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'Turn off RGB fan lights when display sleeps, restore on wake' -Force | Out-Null

# Watchdog: every 5 minutes, relaunch the daemon if no instance is running.
# A Once trigger with 5-min repetition (AtLogOn triggers can't carry Repetition here).
# Interactive principal keeps it from firing while the user is logged off (the
# daemon needs an interactive session).
$wdAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "D:\Software\Dev\RgbAuto\watchdog.ps1"'
$wdTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration ([TimeSpan]::FromDays(3650))
Register-ScheduledTask -TaskName 'RGB Auto Off Watchdog' -Action $wdAction -Trigger $wdTrigger -Settings $settings -Principal $principal -Description 'Relaunch RGB Auto Off daemon every 5 min if not running' -Force | Out-Null
# Tray exit disables the watchdog; re-running setup must bring it back.
Enable-ScheduledTask -TaskName 'RGB Auto Off Watchdog' -ErrorAction SilentlyContinue

Get-ScheduledTask -TaskName 'RGB Auto Off*' | Select-Object TaskName, State
