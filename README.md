# Magic Trackpad 2 Precision Touchpad driver for Windows 11, signed by Microsoft, no more hacks required 🎉

This is a fork of the excellent [imbushuo](https://github.com/imbushuo/mac-precision-touchpad) driver for the Magic Trackpad 2. **It supports Bluetooth**. Compared to imbushuo or to the official 2021 Apple driver, this project adds:

- support for USB-C Magic Trackpad 2
- battery level reading
- haptic feedback control
- various options for controlling pointer precision
- **Trackpad Canvas** (this fork): one tray app for the driver settings plus touchscreen-style pan, zoom and
  rotate in art programs. It replaces the upstream control panel; see below.

The previous version of this project used a hack to install itself in the DriverStore and couldn't support Bluetooth. At the beginning of this year, I decided to purchase an EV certificate to properly sign the driver: I paid 485 euros for it, including taxes that I have no way of recovering as an individual (btw, only organizations can request an EV certificate). I was tired of seeing people resorting to the wildest hacks to get the MT2 to work via Bluetooth 😀 (you can get a glimpse of this in the issues of this repo). **Windows drivers signing requirements and costs are unfair to open-source developers**.

## Installation on Windows 11

0) Uninstall any previous versions of this driver, imbushuo or `official 2021 Apple driver`. Personally I use [DriverStore Explorer](https://github.com/lostindark/DriverStoreExplorer) for that, alternatively you can use Windows Device Manager. Also, **it's especially important to uninstall `Magic Utilities` and `Trackpad++`** before continuing with the installation!

1) Download the zip file of this project from the [Releases](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases) of this repo and unzip it.

2) Select your architecture: AMD64 or ARM64. Right-click on the INF file and click "Install".

### Note about Windows 10

Windows 10 AMD64 is supported through a self-signed driver package.

Windows 10 ARM64 is not supported.

## Trackpad Canvas (settings and art program gestures)

In this fork, [`TrackpadCanvas/`](TrackpadCanvas/README.md) is the single companion app for the driver. It
replaces the upstream WinForms and WinUI 3 control panels. It runs in the system tray as a normal user and has
two tabs:

- **Gestures**: when an art program such as Clip Studio Paint or Photoshop has focus, two-finger gestures
  become real touchscreen pan, pinch-zoom and rotate. Everywhere else it scrolls and zooms like a normal
  touchpad. This works with any Precision Touchpad driver.
- **Trackpad driver**: the former control panel's options (click feedback and strength, silent clicking,
  pointer stop on lift, palm rejection, near-finger rejection, invert movement, non-admin access). *Apply*
  shows one UAC prompt and writes the settings through an elevated copy of the app. The tray app itself
  never runs as admin.

The battery level is shown on the Gestures tab. It is read from the trackpad's HID battery report, or through
this driver when that report isn't available. Over USB, that needs *Let non-admin apps talk to the driver*
to be on. The driver itself is unchanged, so the Microsoft-signed driver package keeps working as-is.

Build: `powershell -ExecutionPolicy Bypass -File TrackpadCanvas\build.ps1` (no SDK needed; uses the .NET
Framework compiler that ships with Windows). The CI workflow also publishes `TrackpadCanvas-amd64.exe`.

## Credits

- [This excellent PR](https://github.com/imbushuo/mac-precision-touchpad/pull/533) of [1Revenger1](https://github.com/1Revenger1) to the imbushuo repo, which fixes the "near field fingers" problem, cleans up the code, and removes the QueryPerformanceCounter call in the interrupt function.
- The haptic feedback control messages sent by the driver to the MT2 in this project are based on the excellent reverse engineering work of [dos1](https://github.com/dos1) ([here](https://github.com/mwyborski/Linux-Magic-Trackpad-2-Driver/issues/28#issuecomment-451625504)).
- My long-time friends at [Landlogic IT](https://landlogic.it/), who took care of the grueling process of gaining access to Microsoft's Hardware Dashboard and who take care of signing the driver packages for me.
- @ordens, Taylor Sharp, @Wikiwix, @nagromc, @danspel, 乔​泽昱, Purasu Oy, Patrick Adler, @macleodal for their contribution to the purchase of the EV certificate ([more info here](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/issues/31)).
