# AGENTS.md — contributor & agent notes for oh-my-coloroff-rgb

Human-facing docs live in [README.md](README.md) / [README.zh-CN.md](README.zh-CN.md).
This file is for anyone (human or coding agent) about to modify the code. Read it first;
it records hard-won empirical facts that are not visible in the source.

## What this is

A Windows tray daemon (~500-line `src\RgbAuto.cs`, .NET Framework 4.x, no IDE) that turns
Colorful (七彩虹) motherboard RGB fans off when the display sleeps and restores the
saved effect on wake. It reuses the LED service stack that ships with the vendor's
iGC.Lite (read-only) because OpenRGB does not support Colorful boards.

## Build & test

Compile commands are in the header comment of `src\RgbAuto.cs`. The tests scaffolding is a
separate assembly (`src\RgbAuto.Tests.cs` → `bin\RgbAuto.Tests.exe`) and must stay out of the
shipped daemon.

```powershell
# diagnostics (bin\RgbAuto.Tests.exe):
.\bin\RgbAuto.Tests.exe listen               # log display power events, no LED access
.\bin\RgbAuto.Tests.exe test svc sleep 45    # push Sleep for 45 s, then release the stack
.\bin\RgbAuto.Tests.exe test svc rainbow 30  # push Rainbow, then release
.\bin\RgbAuto.Tests.exe test probe           # LedDriver dump
.\bin\RgbAuto.Tests.exe test nprobe 20       # raw iGameMBoard.dll P/Invoke test (crashes on
                                             # some driver states — treat results skeptically)
```

Verification workflow: `taskkill /IM bin\RgbAuto.exe /F` before rebuilding (the exe is
locked while running) → start `bin\RgbAuto.exe` → check `bin\rgbrun.log` → exercise with
`tools\turn-off-display.cmd` (screen off) and real mouse input (wake). Never verify a
wake with `SC_MONITORPOWER -1` — see findings below.

## Architecture invariants (do not break)

- **Single instance** via named mutex `Local\RgbAutoDaemon.SingleInstance` (daemon AND
  deferred instance). Duplicates exit 0.
- **iGC.Lite handoff chain**: when Lite starts, the daemon spawns itself with
  `--deferred` and exits; the deferred instance polls until Lite exits, then becomes the
  daemon. If spawn fails 3× before LED init, fall back to waiting in-process. Every exit
  path must leave either a live daemon or a deferred waiter — never zero.
- **The logon scheduled task must NOT have a restart-on-failure count**: yield exits
  use non-zero codes by design and a restart policy would fight iGC.Lite for hardware.
- **The watchdog must only cover unexpected death**: tray Exit disables the
  `RGB Auto Off Watchdog` task (via `schtasks /Disable`, no elevation needed for own
  tasks); `scripts\setup_autostart.ps1` re-enables it.
- **Debounce + one-way echo guard** are load-bearing. LED writes produce fake
  `GUID_CONSOLE_DISPLAY_STATE` events; the guard swallows On-direction events for 3 s
  after a lights-off write and NEVER ignores Off-direction ones.
- Scripts derive the install dir from `$MyInvocation.MyCommand.Path` — keep them
  location-independent; no machine-specific paths in the repo.

## Hardware findings (empirical — trust these over intuition)

Verified on a BATTLE-AX B760M-WHITE WIFI D5 + iGC.Lite, Sept 2026:

- **The MCU needs a resident pumper.** ANY controlling process exit — ours AND the
  vendor's Lite — freezes the LEDs on the last frame. A clean `Uninit()` returning
  success does NOT bring back the boot-time default animation; only a reboot does. The
  BIOS default rainbow runs before the first software init and never returns. Do not
  attempt "exit and let the hardware animate" — it cannot.
- **`SetLightingEffect` returns `UnknowError(1)` on success.** The vendor's own stack
  treats this as OK. Trust the fans, not the HRESULT; log it and move on.
- **`SC_MONITORPOWER -1` ("force wake" without input) is not a wake**: Windows turns
  the display back off ~1.1 s later, producing On→Off pairs in the log that look like
  daemon noise but are Windows behavior. Always test wakes with real input.
- **LED writes echo fake power events** (likely the stack re-enumerates the display
  output). This created a ~8 Hz write/echo oscillation before the debounce + echo guard
  existed. Any new write path must respect the 3 s refractory window.
- **iGC.Lite holds the hardware exclusively** while running; another process's native
  Init blocks (don't wait forever — the daemon's init watchdog gives up after 40 s).
- Effects are pushed per device id from `GetDeviceInfos()`; the daemon must loop over
  ALL ids, not just the first.

## Vendor-stack fragility (expected breakage modes)

The daemon constructs `iGC.Lite.Service.LED.LEDAPIService` by hand because its DI
container is internal: reflection fills `ConfigManager<T>`'s non-public constructor,
plus `ConfigManagerFactory` and `HardwareMonitor`. An iGC.Lite update can rename any of
these — if the daemon stops finding devices after an update, diff the new
`iGC.Lite.Service.dll` / `iGameCenter.ConfigManager.dll` against these names first.

## License

MIT — see [LICENSE](LICENSE).
