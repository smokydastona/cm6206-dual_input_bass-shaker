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
- Vendor bundle reference (optional / local): `cm6206_extracted/`

For full usage details, see `cm6206_dual_router/README.md`.

## Minecraft haptics
This repo is **routing/mixing only**.

If you want Minecraft-specific haptics (telemetry → tactile audio), use the Forge mod repo:
- https://github.com/smokydastona/minecraft_telemetery

Common setup:
- Mod outputs haptics to a virtual device (e.g. VB-CABLE)
- This router loopback-captures that device as the “Shaker input” and routes to your CM6206 7.1 output

## Prebuilt EXE (GitHub Actions)
- This repo publishes a self-contained `win-x64` single-file build.
- On GitHub: **Actions** → latest **build-windows** → download artifact `cm6206_dual_router_win-x64`.

## Installer (recommended)
If you want a normal Windows installer (Start menu shortcut, optional desktop icon) and an optional step to install the CM6206 driver, use the Inno Setup installer.

### Download (from GitHub Releases)
- Tag releases include both:
	- a portable ZIP (`cm6206_dual_router_win-x64_<version>.zip`)
	- an installer EXE (`Cm6206DualRouterSetup_<version>.exe`)

### What the installer does
- Installs the app into `Program Files`.
- Optional task: **Install CM6206 USB 7.1 driver** (runs `pnputil /add-driver ... /install`).
	- Requires admin (UAC prompt).
	- The driver step is **best-effort**: `pnputil` may return non-zero for "already installed" and similar cases; setup continues.
	- Driver install can fail if Windows refuses the driver (signature policy / Secure Boot / incompatible OS).

Driver files source:
- CI builds use the minimal, installer-focused payload in `cm6206_driver_payload/`.
- The full vendor bundle under `cm6206_extracted/` remains ignored.

### Build locally
Prereqs:
- .NET 8 SDK (x64)
- Inno Setup 6

Command:
- `pwsh scripts/build_installer.ps1 -Version 1.2.3`

Outputs:
- Published app: `artifacts/cm6206_dual_router_win-x64/`
- Installer: `artifacts/installer/Cm6206DualRouterSetup_1.2.3.exe`

### Code signing / “Publisher”
Windows shows a real **Publisher** in UAC/SmartScreen only when the EXE is **code-signed**.

If you use a **self-signed** certificate:
- It will show your publisher name **only on machines that trust your certificate**.
- It will NOT automatically remove SmartScreen warnings for other users.

This repo supports optional signing in GitHub Actions if you add these repository secrets:
- `CODESIGN_PFX_BASE64`: base64-encoded `.pfx`
- `CODESIGN_PFX_PASSWORD`: password for the `.pfx`
- (optional) `CODESIGN_TIMESTAMP_URL`: default `http://timestamp.digicert.com`

Self-signed quickstart (dev/testing):
- Create + export a self-signed code-signing cert:
	- `pwsh scripts/new-self-signed-codesign-cert.ps1 -SubjectName SmokyDaStona -PfxPassword 'your_password' -TrustForCurrentUser`
- Convert the `.pfx` to base64 for GitHub Secrets:
	- `pwsh scripts/pfx-to-base64.ps1 -PfxPath .\codesign_dev\codesign_SmokyDaStona_dev.pfx`
	- Paste the output into `CODESIGN_PFX_BASE64`

### Releases ("tag and push")
This repo uses **two** GitHub Actions workflows:
- **Push builds**: build/upload artifacts for `main` and PRs.
- **Tag releases**: when you push a tag like `v1.2.3`, it builds and creates a GitHub **Release** with a versioned ZIP asset and a Setup EXE. The app's in-app update checker uses GitHub "latest release".

Create a release:
- `git tag v1.2.3`
- `git push origin v1.2.3`

To sign locally (requires Windows SDK SignTool):
- `pwsh scripts/sign.ps1 -File .\Cm6206DualRouter.exe -PfxPath .\yourcert.pfx -PfxPassword '...'

## Local build (optional)
- Install **.NET 8 SDK (x64)**: https://dotnet.microsoft.com/download/dotnet/8.0

