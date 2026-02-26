# Virtual playback endpoints ("for real") — driver plan

Authoritative spec (source of truth):
- `docs/virtual_audio_driver/ARCHITECTURE_SPEC.md`

Goal: create **two real Windows playback devices** that show up as render endpoints:
- `Virtual Game Audio`
- `Virtual Shaker Audio`

So apps can output to them normally, and **CM6206 Dual Router** can ingest the audio via a driver interface (shared memory / IOCTL), then mix to the physical CM6206 7.1 output.

Update (required design direction): once the driver exists, the router should not depend on WASAPI loopback capture to ingest audio. The intended architecture is:
- apps render to the endpoints via the normal Windows audio engine
- the driver exposes a dedicated interface (shared memory / IOCTL) so the router can pull PCM from each endpoint

This cannot be done purely in user mode; it requires a **kernel-mode audio driver**.

## Why the Microsoft samples matter
Microsoft’s `Windows-driver-samples` repo includes **SysVAD** (System Virtual Audio Device), which is the canonical starting point for a virtual audio endpoint driver.

Repo:
- https://github.com/microsoft/Windows-driver-samples

Sample of interest:
- `audio/sysvad`

Helper included in this repo:
- `scripts/fetch_sysvad.ps1` (downloads/extracts `audio/sysvad` into `virtual_audio_driver/src/sysvad/`, ignored by git)

SysVAD demonstrates the exact category of work we need:
- Exposing virtual endpoints (render/capture)
- WaveRT miniport patterns (the model Windows expects in 2026)
- INF packaging for audio endpoints
- Friendly name strings and endpoint topology

## Constraints that affect design
- **Friendly name matching**: the router selects devices by **exact friendly name**. The driver should set the endpoint friendly names to exactly match the defaults above, or users will need to copy/paste the names into the router UI/config.
- **Multi-channel**: the goal is that apps can output **surround (5.1/7.1)** to the virtual endpoints; the router should be able to preserve that (especially for the Game endpoint).
- **Hard requirement: WaveRT only**: do not base this on WaveCyclic. SysVAD’s WaveRT approach is the baseline.
- **Shipping reality**: distributing an audio driver requires Windows driver signing (test signing for dev; attestation/production signing for users). Secure Boot policies can block test-signed drivers.
- **Support burden**: a driver installer step requires admin and will be the most failure-prone part of the product.

## Recommended technical approach (SysVAD fork)
1) Fork SysVAD and create a new driver variant (new HWID + branding).
2) Configure it to expose **two render endpoints**.
3) Ensure the endpoints’ device interface and “endpoint name” strings are:
   - `Virtual Game Audio`
   - `Virtual Shaker Audio`
4) Keep formats simple and stable:
   - Shared-mode mix formats should include at least `48kHz, 32-bit float, stereo`.
   - For the **Game** endpoint, also advertise `5.1` and `7.1` formats (48kHz float is the priority).
   - Ensure channel order/masks match Windows expectations: `FL, FR, FC, LFE, BL, BR, SL, SR`.

5) Implement the router-facing data path.
   - Preferred for this product: Option B (event + pull model).
   - The driver maintains a per-endpoint ring buffer fed by the render stream.
   - The router opens a driver device interface and pulls PCM via IOCTL.

IOCTL contract (draft):
- `docs/virtual_audio_driver/02_ioctl_contract.md`

Important: do NOT build a WaveCyclic miniport. The intended model is a **WaveRT virtual miniport** (SysVAD).

## Endpoint behavior expectations
- Each virtual render endpoint acts like a normal playback device.
- The router will pull audio from each endpoint via the driver interface:
   - **Virtual Game Audio** provides the game/music stream (up to 7.1)
   - **Virtual Shaker Audio** provides the shaker stream (stereo is sufficient)
- Latency is governed by the endpoint’s buffering model, driver ring-buffer sizing, and router buffering.

Note: during early bring-up it can be tempting to use WASAPI loopback capture because it is “easy”. That approach is explicitly not the target architecture here.

## Build prerequisites (dev)
- Windows 10/11
- Visual Studio with C++ Desktop workload
- Windows Driver Kit (WDK) matching your OS/VS

## Install prerequisites (dev/test)
You have two common dev paths:

### A) Test signing on a dev machine
- Create a test certificate
- Enable test signing (`bcdedit /set testsigning on`)
- Sign the driver package
- Install via `pnputil /add-driver <inf> /install`

This is the simplest path but is often blocked by Secure Boot policies.

### B) Test on a separate machine/VM configured for test-signed drivers
Recommended if you don’t want to weaken your daily driver configuration.

## Packaging plan (repo integration)
Keep this driver build separate from the main app build initially.

Proposed repo layout:
- `virtual_audio_driver/`
  - `README.md` (build/install)
  - `src/` (SysVAD-derived driver project)
  - `package/` (INF, CAT outputs, signed artifacts)

We can later integrate with the existing Inno Setup installer by adding an optional task similar to the CM6206 hardware driver step:
- Copy driver payload to `{tmp}`
- Run `pnputil` on the virtual endpoint INF

Payload contract in this repo (installer expects this exact filename):
- `virtual_audio_driver_payload/WIN10/Driver/CMVADR.inf`

See also: docs/virtual_audio_driver/01_sysvad_patch_map.md

## Production signing notes (high-level)
For end users, the driver must be signed in a way Windows will accept by default:
- Typical options include **attestation signing** via the Windows Hardware Developer Program.
- For broader distribution and strong compatibility, WHQL/HLK-tested signing may be needed.

Exact requirements can change; the key point is: **shipping drivers is a different pipeline** than shipping a .NET app.

## Next concrete steps
1) Decide endpoint details:
   - Stereo only vs optional 7.1
   - Fixed 48kHz only vs multiple sample rates
2) Create a SysVAD fork and pin a SysVAD commit/version to fork.
   - Keep the SysVAD-derived source out of this repo unless we intentionally decide to vendor it.
   - Use docs/virtual_audio_driver/01_sysvad_patch_map.md as the starting checklist.
3) Build the fork locally with WDK, confirm devices appear in Windows Sound.
   - Sanity check what Windows is advertising:
     - `dotnet run -c Release --project cm6206_dual_router/Cm6206DualRouter.csproj -- --list-devices --show-formats`
    - Sanity check the driver IOCTL interface (once implemented in the fork):
       - `dotnet run -c Release --project cm6206_dual_router/Cm6206DualRouter.csproj -- --probe-cmvadr`
4) Validate router end-to-end:
   - Set Windows default output to `Virtual Game Audio` and verify apps can target it per-app
   - Verify the router can read both endpoints via IOCTL (driver interface)
   - Mix / filter / route to CM6206
   - Confirm B can be used for bass shaker content or disabled.

