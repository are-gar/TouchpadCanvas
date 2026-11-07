# Trackpad Canvas

> Part of this fork of [MagicTrackpad2ForWindows](https://github.com/vitoplantamura/MagicTrackpad2ForWindows),
> where it replaces the driver's control panel (see *Trackpad driver tab* below). The gesture features work
> with any Precision Touchpad driver.

Turns two-finger gestures on a Windows Precision Touchpad (e.g. Apple Magic Trackpad with the
mac-precision-touchpad driver) into **touchscreen** gestures, so art programs like Clip Studio Paint
and Photoshop get real pan, pinch-zoom and rotate instead of Windows' scroll-wheel emulation.

## How it works

1. Reads raw finger positions from the trackpad via Windows Raw Input (no driver changes).
2. When two fingers move on the pad while an allowed app is focused, it creates two virtual
   touchscreen contacts centred on the mouse cursor (via `InjectTouchInput`) and moves them with
   your fingers. Finger spacing and angle are preserved, so rotation and zoom are exact.
3. A two-finger *tap* (no movement) injects nothing, so right-click still works.

## Setup

1. Build: `powershell -ExecutionPolicy Bypass -File build.ps1` → `bin\TrackpadCanvas.exe`
2. **Windows Settings → Bluetooth & devices → Touchpad → Scroll & zoom**: turn off
   *Drag two fingers to scroll* and *Pinch to zoom*. Otherwise Windows also scrolls/zooms the
   canvas at the same time. Trackpad Canvas then provides two-finger scrolling and pinch-zoom in
   other apps itself (see *Everywhere else* below).
3. **Clip Studio Paint**: in *File → Preferences*, set **Touch gesture settings** to
   **Windows gestures** (confirmed working in CSP 5.0.4).
4. **Photoshop**: *Preferences → Tools → Enable Gestures*.
5. Run `bin\TrackpadCanvas.exe`. Minimizing hides it to the tray.

## Options

| Setting | Meaning |
|---|---|
| Enabled | Master on/off (also in the tray menu) |
| Touch gestures (pan, zoom, rotate) only in | Comma-separated process names (no `.exe`) that get touchscreen gestures. Untick to use touch gestures in every app |
| Canvas movement (pixels per mm) | How far the virtual fingers move per mm on the pad |
| Reverse pan direction | Flip panning only; zoom/rotate unaffected |
| Everywhere else: scroll and pinch-zoom like a normal touchpad | In all other apps, two-finger movement sends mouse-wheel scrolling and a pinch sends Ctrl + wheel, so scrolling still works with Windows' own touchpad scrolling turned off |
| Scroll speed | Wheel units per mm of finger movement (120 = one wheel notch) |
| Reverse scroll direction | Default is "natural" scrolling (content follows your fingers), like Windows' default |

## Trackpad driver tab

The settings of this repo's Magic Trackpad 2 driver (formerly `AmtPtpControlPanel`). They are stored in the
registry under each driver's `Parameters` key in HKLM, the same place the old control panel used, so existing
settings carry over.

| Setting | Meaning |
|---|---|
| macOS-style click | Haptic click with a Light / Medium / Firm strength. *Silent clicking* keeps the click but drops the vibration sound |
| Disable haptic feedback | Turns off the actuator and the force touch button completely |
| Maximum haptic feedback | Strongest possible click (very loud) |
| When you lift your finger | Optionally freeze the pointer when pressure or touch size drops to a threshold, so it doesn't jump as you lift |
| Ignore near fingers | Ignore fingers hovering just above the surface |
| Palm rejection | Ignore large contacts |
| Ignore the finger that presses the button | For dragging: click with your thumb and move with your index finger |
| Invert up/down and left/right | For using the trackpad upside down |
| Let non-admin apps talk to the driver | Opens the driver's USB pipe to normal apps. Trackpad Canvas needs this to read the battery level over USB |

*Apply* asks for administrator permission once (UAC). Trackpad Canvas then runs
`TrackpadCanvas.exe --apply-driver-settings <name=value,...>` elevated. That copy writes the registry, tells the
connected trackpad to reload its settings, logs the result and exits. The tray app keeps running as a normal
user. *Revert* reloads the values from the registry. The tab is disabled when the driver isn't installed.

Settings are saved to `bin\TrackpadCanvas.ini`; a log is written to `bin\TrackpadCanvas.log`.

`TrackpadCanvas.exe --probe` writes detected touchpads to the log and exits.
`TrackpadCanvas.exe --minimized` starts in the tray (useful in a Startup shortcut).

## Known limitations

- The virtual fingers are clamped to the screen edges; for long pans, lift and repeat.
- After a gesture the mouse cursor may jump to where the virtual touches were.
- If the art program runs as administrator, this tool must too (Windows blocks injecting input
  into higher-privilege windows).
