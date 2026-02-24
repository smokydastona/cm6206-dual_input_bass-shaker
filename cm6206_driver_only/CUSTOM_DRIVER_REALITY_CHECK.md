# Custom CM6206 driver (Win11) — reality check

You asked for a **new updated custom driver copy** that:
- never changes Windows default device
- exposes **two separate playback devices** (Music + Shaker)
- is **normally signed** (no test mode)
- avoids Voicemeeter-style routing apps

## 1) “Never become default” is not really a driver feature
Windows chooses defaults via policy/user choice. A driver can ship a control panel or helper app that *changes* defaults, but a driver can’t reliably enforce “don’t ever pick me as default” globally.

So the realistic target is:
- don’t install vendor helper apps that change defaults
- set a stable default device once

## 2) Two separate devices requires a *virtual audio endpoint driver*
To show two distinct playback devices in Windows Sound settings, you need an audio driver that exposes two render endpoints.

Common approaches:
- Kernel-mode audio endpoint driver (AVStream / PortCls) + topology
- A virtual device driver + (often) a user-mode service to route audio to real hardware

## 3) Routing “Music” and “Shaker” into the same CM6206 hardware
Even if you create two virtual endpoints, you still must get the audio to the CM6206 outputs.

There is no simple “driver-to-driver patch cable” in Windows audio. Most solutions end up being:
- a user-mode audio engine/service that captures from the virtual endpoint and plays to the target device (WASAPI)

This is functionally the same category as Voicemeeter, just custom-built.

## 4) Normal signing on Win11 is the hard part
For a kernel-mode driver that loads on Windows 11 without test mode:
- You typically need an **EV code signing certificate** and a Microsoft partner/dev account
- You submit the driver for **attestation signing** via the Microsoft Hardware Dev Center
- You must follow driver packaging rules (INF/CAT), and in many cases run HLK for broader distribution

This is not a “mod an INF and call it a day” situation.

## Recommended practical path
### A) What we can do now (fast)
- Install **driver-only** (no C-Media control panel), so defaults are not manipulated.
- Use Equalizer APO on the CM6206 endpoint for shaker filtering and channel distribution.

### B) If you truly need two endpoints (Music/Shaker)
- Use an existing **signed virtual audio device** driver (3rd party) to create the two endpoints
- Route the shaker endpoint to CM6206
- Apply shaker EQ/filtering only on that path

### C) If you still want your own custom driver
Treat it as a real software project:
- Set up WDK + Visual Studio driver build environment
- Start from Microsoft samples (SysVAD, audio endpoint examples)
- Implement two render endpoints
- Implement routing service (user-mode) to CM6206
- Get signing sorted (EV + attestation)

If you want, we can turn this into an engineering checklist for your exact goals, but it won’t be a quick tweak.
