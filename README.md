# TouchpadCanvas

**Use an Apple Magic Trackpad like a touchscreen in Clip Studio Paint and Photoshop on Windows.** Pan, pinch-zoom
and rotate the canvas with two fingers, the way you would on an iPad, while the trackpad keeps working as a
normal touchpad everywhere else.

This repository is a fork of Vito Plantamura's
[MagicTrackpad2ForWindows](https://github.com/vitoplantamura/MagicTrackpad2ForWindows) driver (see
[Credits](#credits)). Trackpad Canvas replaces that project's control panel, so one app now handles both the
gestures and the driver settings.

## What Trackpad Canvas does

**Gestures tab**
- In allowed apps (Clip Studio Paint and Photoshop by default), it creates two virtual touchscreen fingers under
  your cursor that follow your real fingers. Pan, zoom and rotate track your fingers exactly.
- In every other app, two-finger scrolling and pinch-zoom work like a normal touchpad.
- A two-finger tap still right-clicks.
- It shows the trackpad's battery level and warns you when it's low.
- These features work with any Windows Precision Touchpad, not only the Magic Trackpad.

**Trackpad driver tab**
- All the options of the upstream driver: haptic click strength, silent clicking, disabling or maxing out the
  haptics, freezing the pointer when you lift a finger, palm and near-finger rejection, upside-down use, and
  non-admin access.
- The app runs as a normal user. Clicking *Apply* asks for administrator permission once, only to save these
  settings.

Full setup, options and limitations: [`TrackpadCanvas/README.md`](TrackpadCanvas/README.md).

## Installation

1. **Install the driver** (Magic Trackpad users). Use the **Microsoft-signed** package from the
   [upstream Releases](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases). Driver builds from
   this fork aren't signed, and the driver code here is unchanged from upstream.
   - First uninstall any older version of this driver, the imbushuo driver or the official 2021 Apple driver
     (for example with [DriverStore Explorer](https://github.com/lostindark/DriverStoreExplorer) or Device
     Manager). **Also uninstall *Magic Utilities* and *Trackpad++*.**
   - Unzip the package, open the folder for your architecture (AMD64 or ARM64), right-click the INF file and
     choose *Install*. On Windows 10, only AMD64 is supported, through upstream's self-signed package.
2. **Get Trackpad Canvas.** Build it with
   `powershell -ExecutionPolicy Bypass -File TrackpadCanvas\build.ps1`. No SDK is needed; it uses the .NET
   Framework compiler that ships with Windows. Or download `TrackpadCanvas-amd64.exe` from this repo's Actions
   build.
3. **Set up Windows and your art program.** Turn off Windows' own two-finger scroll and pinch-zoom, and turn on
   touch gestures in Clip Studio Paint or Photoshop. The steps are in the
   [Trackpad Canvas README](TrackpadCanvas/README.md#setup).

## Repository layout

| Folder | What it is | Origin |
|---|---|---|
| `TrackpadCanvas/` | The tray app: gestures and driver settings | This fork |
| `AmtPtpDeviceUsbUm/` | USB user-mode driver (UMDF) | Upstream, unchanged |
| `AmtPtpHidFilter/` | Bluetooth HID filter driver (KMDF) | Upstream, unchanged |
| `build/`, `tools/` | Packaging scripts and test tools | Upstream; scripts updated to ship Trackpad Canvas |

## Credits

The driver, which makes the Magic Trackpad 2 a Windows Precision Touchpad over USB and Bluetooth, is entirely the
work of **[Vito Plantamura](https://github.com/vitoplantamura)** and the people he credits. It builds on the
[imbushuo mac-precision-touchpad](https://github.com/imbushuo/mac-precision-touchpad) driver and adds USB-C
support, battery reading, haptic feedback control and pointer-precision options. Vito personally paid for an EV
certificate so the driver could be properly signed by Microsoft. If it works for you, consider supporting the
[upstream project](https://github.com/vitoplantamura/MagicTrackpad2ForWindows).

Credits from the upstream project:

- [This excellent PR](https://github.com/imbushuo/mac-precision-touchpad/pull/533) by
  [1Revenger1](https://github.com/1Revenger1) to the imbushuo repo. It fixes the "near field fingers" problem,
  cleans up the code and removes the QueryPerformanceCounter call from the interrupt function.
- The haptic feedback control messages are based on the reverse engineering work of
  [dos1](https://github.com/dos1)
  ([here](https://github.com/mwyborski/Linux-Magic-Trackpad-2-Driver/issues/28#issuecomment-451625504)).
- [Landlogic IT](https://landlogic.it/), who handle access to Microsoft's Hardware Dashboard and the signing of
  the upstream driver packages.
- @ordens, Taylor Sharp, @Wikiwix, @nagromc, @danspel, 乔​泽昱, Purasu Oy, Patrick Adler and @macleodal, who
  helped pay for the upstream EV certificate
  ([more info](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/issues/31)).

## License

GPLv2, the same as the upstream project. See [LICENSE](LICENSE).
