[English](README.md) | [简体中文](README.zh-CN.md)

![banner](assets/banner.png)

**Screen off → lights off. Screen on → lights back.**

Windows tray daemon that syncs Colorful (七彩虹) motherboard RGB fans with the display
power state. When Windows turns the monitor off, the fans go dark; the moment it comes
back, your saved lighting effect resumes flowing.

## TL;DR

Requires: Windows 10/11, a Colorful motherboard, and [iGC.Lite](https://www.colorful.cn) installed.

```powershell
git clone https://github.com/Fae42/oh-my-coloroff-rgb
cd oh-my-coloroff-rgb
powershell -ExecutionPolicy Bypass -File setup_autostart.ps1   # autostart + watchdog
.\RgbAuto.exe                                                   # or just re-login; done.
```

No build needed — grab `RgbAuto.exe` from [Releases](../../releases) if you don't want to compile.
Uninstall: `remove_autostart.ps1`, delete the folder.

## Why

RGB fans look great — until you step away and the display sleeps while the fans keep
strobing in an empty room. Colorful boards only expose lighting control through the
vendor's private stack (`iGC.Lite`), which OpenRGB does not support. This daemon reuses
the LED service stack that ships with iGC.Lite (read-only; nothing under
`C:\Program Files\iGC.Lite` is modified) and listens to Windows'
`GUID_CONSOLE_DISPLAY_STATE` power notifications, so no polling and near-zero idle cost.

## Features

- **Display-driven**: reacts to the real console display state (idle timeout, hotkey,
  `SC_MONITORPOWER` — all covered).
- **Debounced**: lights change only after the state is stable (1.5 s off / 1.0 s on),
  with a one-way echo guard — LED writes themselves produce fake power events, and
  swallowing only the unsafe direction keeps the loop from re-arming.
- **System tray**: yellow dot = lights on, gray = off; right-click for
  *Restore effect / Lights off / Exit* (exit also pauses the watchdog).
- **Plays nice with iGC.Lite**: open Lite to tune effects — the daemon yields
  automatically and resumes within seconds after Lite closes.
- **Self-healing**: a 5-minute watchdog scheduled task relaunches the daemon if it
  ever dies; single-instance mutex; log rotation at 1 MB.
- **Multi-device**: pushes the sleep effect to every lighting device the stack
  enumerates (motherboard, and any future Colorful GPU).

## Usage

- Daily driving: forget it exists. Tune effects by opening iGC.Lite, then close Lite.
- Tray **Exit** = stop for real (watchdog paused; logon task untouched — re-run the
  setup script to re-enable, `remove_autostart.ps1` to uninstall fully).
- `tools\turn-off-display.cmd`: manual screen-off helper for testing the linkage
  without waiting for the idle timeout.

## How it works

1. `RegisterPowerSettingNotification` + a hidden message-only form receives
   `GUID_CONSOLE_DISPLAY_STATE` events (Windows replays the current state at
   registration, so startup always converges).
2. Debounce state machine (500 ms tick): apply `SetLightingEffect(id, Sleep)` after the
   display has been stably off, `InitLightingEffect()` after it is stably on.
3. `iGC.Lite.Service.LED.LEDAPIService` is constructed by hand (its DI container is
   internal — reflection fills `ConfigManager<T>` non-public constructors) and reused
   exactly as the vendor's own UI does.

## Hardware findings (empirical, BATTLE-AX B760M + iGC.Lite)

Documented so the next person doesn't rediscover them the hard way:

- **The MCU needs a resident pumper.** Any controlling process exit — ours *and the
  vendor's Lite* — freezes the LEDs on the last frame. A clean `Uninit()` does not
  bring back the boot-time default animation; only a reboot does. The BIOS default
  rainbow runs before the first software init and never returns.
- `SetLightingEffect` returns `UnknowError(1)` on success. Trust the fans, not the
  HRESULT.
- `SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, -1)` ("force wake"
  without input) makes Windows turn the display back off ~1.1 s later. On→Off pairs
  in the log are Windows behavior, not daemon noise. Wake with real input to test.
- LED writes echo fake power events; the one-way echo guard (ignore On-direction
  events for 3 s after a lights-off write, never ignore Off) keeps the loop stable.
  Cost: a genuine wake within 3 s of a lights-off write waits for the next event.

## Limitations

- Colorful-only: it borrows iGC.Lite's private stack; a vendor update can break it.
- Covers display sleep, not system sleep/hibernate (fans may freeze mid-glow — the
  hardware offers no off-at-suspend hook we've found).
- Verified on one board model; other Colorful models may enumerate differently.

## Build from source

No IDE needed:

```bat
csc -nologo -platform:x64 -target:winexe -win32icon:assets\app.ico -out:RgbAuto.exe ^
  "-r:C:\Program Files\iGC.Lite\iGameAPI.Contracts.dll" "-r:C:\Program Files\iGC.Lite\iGC.Lite.Service.dll" ^
  "-r:C:\Program Files\iGC.Lite\iGameCenter.ConfigManager.dll" "-r:C:\Program Files\iGC.Lite\Castle.Core.dll" ^
  "-r:C:\Program Files\iGC.Lite\iGameCenter.Hardware.dll" "-r:C:\Program Files\iGC.Lite\iGameCenter.Contracts.dll" ^
  -r:System.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll RgbAuto.cs
```

## License

MIT — see [LICENSE](LICENSE).
