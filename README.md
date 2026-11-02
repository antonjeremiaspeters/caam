# CaaM — Controller as a Mouse

CaaM turns a controller into a system-wide mouse and keyboard for Windows, optimized for controlling a computer comfortably from the couch.

## Highlights

- Plug-and-play detection for Xbox, PlayStation, Nintendo Switch, and generic controllers
- USB, Bluetooth, and wireless-adapter support through SDL, HID, and XInput
- Left stick for mouse movement and right stick for precise scrolling
- Instant mouse/scroll stick swapping for left- or right-handed use
- Fully configurable controller buttons with matching Xbox, PlayStation, and Switch labels
- DualShock 4 and DualSense touchpad mouse support
- Automatic handoff to games with native controller support
- On-screen keyboard fallback for text fields
- Windows startup and optional priority startup with restart protection
- System, light, dark, and resource-efficient minimal themes, plus custom CSS
- German, English, and automatic system-language selection
- Separate reset options for general settings and button mappings
- Complete configuration backup and restore
- Lightweight system-tray operation without unnecessary background UI rendering
- Local settings and no telemetry

## System Requirements

- Windows 10 version 1809 or later, or Windows 11
- 64-bit system
- A game controller recognized by Windows
- Microsoft Edge WebView2 Runtime for the modern themes; the minimal theme uses a native interface

## Usage

1. Start `CaaM.exe`.
2. Connect a controller.
3. Adjust button mappings and sensitivity if needed.
4. Select **Save** to apply the configuration. Stick swapping takes effect immediately.

CaaM continues running in the system tray after the main window is closed. In supported games, mouse and keyboard emulation pauses automatically so the game can use the controller natively.

## Building

The .NET 7 SDK for Windows is required.

```powershell
.\build.ps1
```

The portable, self-contained 64-bit executable is created at `release-v1.0/CaaM.exe`.

## Privacy

CaaM stores its settings locally under `%LOCALAPPDATA%\CaaM`. It contains no telemetry and does not automatically transmit usage data.

## Third-Party Components

CaaM uses SDL 3 and the SDL controller-mapping database. The relevant license information is included with the release as `SDL3-LICENSE.txt`.

## Author and Links

Copyright © 2026 Anton Jeremias Peters. All rights reserved.

- [GitHub profile](https://github.com/antonjeremiaspeters)
- [CaaM repository](https://github.com/antonjeremiaspeters/caam)
- [Support the project](https://www.paypal.com/paypalme/antonjeremiaspeters)

This project is distributed under the terms in [LICENSE](LICENSE).
