# CM6206 dual-input bass shaker router

This repo provides a simple Windows app that:
- Captures **two Windows playback devices** (your “Music” + “Shaker” virtual outputs) via WASAPI loopback.
- Routes/mixes them into **one CM6206 7.1 output**.
- Includes a **tabbed UI** with **per-channel trims** (FL/FR/FC/LFE/BL/BR/SL/SR).

## Project
- App + UI + config: `cm6206_dual_router/`
- Optional local reference (not in git): `cm6206_extracted/` (vendor bundle)

## Requirements (local build)
- Install **.NET 8 SDK (x64)**: https://dotnet.microsoft.com/download/dotnet/8.0

## Prebuilt EXE (GitHub Actions)
- This repo publishes a self-contained `win-x64` single-file build.
- On GitHub: **Actions** → latest **build-windows** → download artifact `cm6206_dual_router_win-x64`.
- If you create a tag like `v1.0.0`, the workflow attaches the build to a GitHub Release.

