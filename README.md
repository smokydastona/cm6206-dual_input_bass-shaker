# better sound (CM6206 + bass shakers)

This workspace contains a few different approaches to using a **CM6206 USB 7.1** device for bass shakers (and avoiding the vendor installer/control panel when possible).

## Pick what you want to use

### Option A (recommended): the router app (2 virtual outputs → 1 CM6206 7.1)
Folder: `cm6206_dual_router/`
- Captures **two Windows playback devices** (usually Voicemeeter virtual outputs) via WASAPI loopback.
- Mixes/routes them into **one CM6206 7.1 output**, with **bass shaker filtering + 7.1 distribution**.
- Does **not** need a custom kernel driver (so it’s installable on Windows 11 without test mode).

Start here: `cm6206_dual_router/README.md`

### Option B: driver-only install (no vendor setup UI)
Folder: `cm6206_driver_only/`
- A “Have Disk…” style package to install the existing **signed** CM6206 driver without running the vendor setup UI.

Note: the actual vendor driver payload files are intentionally **not committed** to git (see `.gitignore`).

Start here: `cm6206_driver_only/README.md`

### Option C: notes/config for Voicemeeter + Equalizer APO
File: `cm6206--modded`
- A step-by-step routing/EQ guide for distributing bass to 7.1 channels.

### Option D: extracted vendor bundle (for reference)
Folder: `cm6206_extracted/`
- Original extracted installer files used for inspection/tweaks.

Note: this folder is intentionally **not committed** to git (see `.gitignore`).

## Folder map
- `cm6206_dual_router/` — source code for the routing app
- `cm6206_driver_only/` — manual driver install package (signed driver)
- `cm6206_custom_driver_project/` — planning notes for a true custom driver path
- `cm6206_extracted/` — extracted vendor installer contents

## Requirements
- For `cm6206_dual_router/`: install **.NET 8 SDK (x64)** from https://dotnet.microsoft.com/download/dotnet/8.0

