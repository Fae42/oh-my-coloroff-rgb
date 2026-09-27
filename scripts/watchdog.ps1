# RGB Auto Off watchdog: relaunch the daemon if no instance (daemon or deferred) is running.
# Runs every 5 minutes via the 'RGB Auto Off Watchdog' scheduled task while the user is logged on.
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir
$bin  = Join-Path $root 'bin'
$exe  = Join-Path $bin 'RgbAuto.exe'
if (-not (Get-Process RgbAuto -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $exe -WorkingDirectory $bin
    Add-Content -Path (Join-Path $bin 'rgbrun.log') -Value ("{0:HH:mm:ss.fff} watchdog: no RgbAuto instance, relaunched" -f (Get-Date))
}
