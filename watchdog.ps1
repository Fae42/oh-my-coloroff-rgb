# RGB Auto Off watchdog: relaunch the daemon if no instance (daemon or deferred) is running.
# Runs every 5 minutes via the 'RGB Auto Off Watchdog' scheduled task while the user is logged on.
$exe = 'D:\Software\Dev\RgbAuto\RgbAuto.exe'
$wd  = 'D:\Software\Dev\RgbAuto'
if (-not (Get-Process RgbAuto -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $exe -WorkingDirectory $wd
    Add-Content -Path (Join-Path $wd 'rgbrun.log') -Value ("{0:HH:mm:ss.fff} watchdog: no RgbAuto instance, relaunched" -f (Get-Date))
}
