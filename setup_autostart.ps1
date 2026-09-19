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
Get-ScheduledTask -TaskName 'RGB Auto Off' | Select-Object TaskName, State
