# CM6206 “two playback devices” custom driver — project kit (Win11)

You asked for: a **custom driver** that does everything the original does, but exposes **two separate playback devices** so you can install and pick them independently.

## What I can/can’t do from this workspace
### I can provide
- A concrete engineering plan that is actually how Windows audio drivers are built.
- A build/sign/attestation checklist for Windows 11.
- Scripts to capture your exact hardware IDs and current driver state.
- A packaging approach so you can end up with an installable `.inf` + `.cat` + signed `.sys`.

### I cannot provide (from here)
- A finished **normally-signed** driver binary you can just install.
  - On Windows 11, kernel drivers require a signing pipeline (EV cert + Microsoft attestation signing).
  - Writing a full USB audio class replacement driver from scratch is a large project.

## Critical reality: “two devices” implies a virtual endpoint driver + routing
To make Windows show **two playback devices** (e.g., `CM6206 Music` and `CM6206 Shaker`), you need an audio driver stack that exposes **two render endpoints**.

Then you still must **route** both endpoints into the same physical CM6206 hardware outputs. In practice this requires either:
- A user-mode routing service (WASAPI loopback/capture + render), or
- A driver stack that internally mixes/routes (complex; still not trivial)

This is why tools like Voicemeeter exist.

## Best practical outcome (what this kit targets)
- Create two **virtual playback endpoints**:
  - Endpoint A: “Music” (full range)
  - Endpoint B: “Shaker” (bass-only or at least separate so you can EQ it)
- Route Endpoint B to the CM6206 device.
- Keep the CM6206 physical driver as-is (stable + signed already).

That gets you exactly what you want (two selectable devices), without trying to replace USB audio hardware driver code.

## Next steps
1) Run the hardware info script:
   - `scripts/get_cm6206_device_info.ps1`
2) Follow the build notes:
   - `BUILD_AND_SIGN.md`
3) Decide routing model:
   - `DESIGN.md`

If you still want a *true replacement* for the CM6206 USB hardware driver, say so — but that’s a much larger scope.
