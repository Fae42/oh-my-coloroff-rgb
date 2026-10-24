$ErrorActionPreference = 'Stop'
trap {
    Write-Host ("RGB Auto Off uninstall failed. " + $_.Exception.Message)
    exit 1
}

foreach ($taskName in @('RGB Auto Off', 'RGB Auto Off Watchdog')) {
    try {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop
    }
    catch {
        if ($_.FullyQualifiedErrorId -ne 'CmdletizationQuery_NotFound_TaskName,Unregister-ScheduledTask') { throw }
    }
}
try {
    Stop-Process -Name RgbAuto -Force -ErrorAction Stop
}
catch {
    if ($_.FullyQualifiedErrorId -ne 'NoProcessFoundForGivenName,Microsoft.PowerShell.Commands.StopProcessCommand') { throw }
}
Write-Host "RGB Auto Off stopped and removed (daemon, watchdog, autostart). Re-enable with scripts\setup_autostart.ps1"
