# CM6206 dual-input router (Music + Shaker → one 7.1 output)

This repo contains a Windows app for the common “I want two independent PC outputs, but only one physical 7.1 USB adapter” problem:
- Captures **two Windows playback endpoints** (typically two virtual devices) via **WASAPI loopback**:
	- Music (full-range)
	- Shaker (bass-only)
- Mixes/routes them into **one physical 7.1 output** (e.g. CM6206).

It’s designed to avoid driver/registry hacks and to not fight Windows default-device behavior. You choose your endpoints and the app routes audio.

## What’s included
- WinForms UI with tabs for **Devices**, **DSP**, **Channels**, **Calibration**.
- Per-channel: **trim**, **mute**, **solo**, **invert**, and **remap**.
- Visual **drag-to-remap** 7.1 layout.
- Calibration signals: sine, pink/white noise, and sweep (with optional voice prompts + auto-step).
- Round-trip latency measurement (requires a capture device + a physical loopback).
- Profiles (separate JSON files) + optional per-app auto-switch by running EXE name.
- Output format helper: **shows Windows mix format**, **warns about Shared-mode sample rate**, and can **probe/blacklist Exclusive-mode rates**.

## Where the app lives
- App + UI + config: `cm6206_dual_router/`
- Minecraft telemetry -> haptic audio buses (SimHub-style engine): `minecraft_haptic_engine/`
- Vendor bundle reference (optional / local): `cm6206_extracted/`

For full usage details, see `cm6206_dual_router/README.md`.

## Prebuilt EXE (GitHub Actions)
- This repo publishes a self-contained `win-x64` single-file build.
- On GitHub: **Actions** → latest **build-windows** → download artifact `cm6206_dual_router_win-x64`.

## Local build (optional)
- Install **.NET 8 SDK (x64)**: https://dotnet.microsoft.com/download/dotnet/8.0

