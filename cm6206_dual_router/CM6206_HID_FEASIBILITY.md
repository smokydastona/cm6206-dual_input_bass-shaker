# CM6206 HID / EEPROM feasibility (Windows)

This repo currently routes audio using WASAPI/NAudio; it does **not** talk to the CM6206 chip directly.

## What `cm6206ctl` shows (high confidence)

The project https://github.com/vestom/cm6206ctl is a small CLI that controls a CM6206-based USB sound card by opening a **USB HID interface** (via `hidapi`). It targets:

- Vendor/Product: `0d8c:0102` (C-Media CM6206 / CM6206_LX)
- A small set of chip “registers” (it reads/writes 6 registers in the public code)
- It uses HID *output* reports to request reads/writes, and HID *input* reports to return data

So: **register-level control via HID is real and documented-by-implementation**.

## What it does *not* prove

- It does not, by itself, prove “EEPROM read/write” access.
  - Some settings may be persistent depending on firmware/driver behavior, but that would need confirmation on-device.
- It does not guarantee the HID interface is accessible on Windows with every driver setup.
  - If the device exposes a HID interface as a composite function, Windows should enumerate it.
  - If the vendor driver claims/filters the interface in a way that hides it from generic HID access, we may not be able to open it.

## Windows feasibility: realistic path

### 1) Detect whether a HID interface exists
- Look for HID devices with VID/PID `0d8c:0102`.
- If present, attempt opening it and doing a **read-only** register dump.

### 2) Implement read-only register dump first
- Implement our own HID read/write (do **not** copy `cm6206ctl` source into this repo).
- Use a .NET HID library (e.g., HidSharp) or Windows HID APIs.
- Keep it opt-in and safe: “Read registers” should be available without requiring admin.

### 3) Add guarded write operations
- Writes should be behind an explicit “I understand” toggle.
- Provide a “Restore defaults” button only if we can prove safe behavior on real hardware.

## Licensing note (important)

`cm6206ctl` is GPL-2.0+.

- We should **not** copy/paste its source into this project unless you want this project to become GPL-compatible.
- We *can*:
  - Treat it as a reference for behavior and write our own clean implementation, or
  - Offer integration as an **external tool** (user provides a path to a `cm6206ctl.exe` they built), and we parse its output.

## Next concrete step to unblock HID work

Add a small “CM6206 HID” diagnostics panel that:

1) Lists matching HID devices (VID/PID)
2) Shows “HID accessible: yes/no”
3) If accessible, reads registers and displays them

This will quickly tell us whether “actual CM6206 HID” is possible on your Windows 11 + driver stack.
