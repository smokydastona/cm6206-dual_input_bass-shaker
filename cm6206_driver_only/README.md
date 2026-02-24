# CM6206 driver-only install (Win11)

This avoids installing the C-Media control panel / “Xear Audio Center” app and installs **only** the driver INF.

Why this helps
- The CM6206 hardware is a USB Audio device; Windows can handle it fine.
- The vendor **app/control panel** is usually what tries to “help” by setting defaults or exposing “set default device” UI.
- A true custom Windows audio driver that exposes extra endpoints and is **normally signed** is a major engineering + signing project (EV cert + Microsoft attestation/HLK), not a quick mod.

## Install now (driver only)

Important: This repo does **not** include the vendor driver payload files (INF/CAT/SYS/DLL). You must provide them locally.

What you need in `cm6206_driver_only\\package\\`:
- `CMUAC.inf`
- `CMUAC.cat`
- `X64\\CMUAC.sys` (and any required DLLs alongside it)

You can source these from:
- The vendor installer you already have, or
- The extracted bundle you keep locally (`cm6206_extracted/`), or
- An already-installed driver package on your machine.

### Option 1: Manual install (Device Manager)
1. Unplug the CM6206.
2. If you previously installed the vendor suite, uninstall it (Apps & Features) and reboot.
3. Plug CM6206 back in.
4. Open Device Manager.
5. Find the CM6206 device (often under **Sound, video and game controllers** or **Audio inputs and outputs**).
6. Right-click -> **Update driver** -> **Browse my computer** -> **Let me pick** -> **Have Disk...**
7. Browse to this folder and select `CMUAC.inf`:
	- `cm6206_driver_only\\package\\CMUAC.inf`

### Option 2: Install via PowerShell (pnputil)
Right-click `install_driver_only.ps1` and **Run with PowerShell** (as Admin).

## What it does
- Runs `pnputil /add-driver` on the provided INF and requests install.
- Does **not** run any of the vendor Setup UI.

## Files used
This uses the self-contained driver package folder:
- `cm6206_driver_only\\package\\CMUAC.inf`
- `cm6206_driver_only\\package\\CMUAC.cat`
- `cm6206_driver_only\\package\\X64\\CMUAC.sys` (+ required DLLs)

This is based on the Win10 driver set, which is commonly what Win11 uses for these devices.

## Still seeing default-device switching?
That can be Windows policy when *new* audio devices appear.
The most reliable setup is:
- Set Windows Default Output to a stable device you actually listen to.
- Use a mixer/router (Voicemeeter or equivalent) if you need duplication or per-app routing.

If you want two separate audio devices (“Music” + “Shaker”) without Voicemeeter:
- The practical route is a **signed virtual audio device driver** (3rd-party) + an audio routing service.
- Building and signing your own from scratch is possible, but it’s a multi-week project.
