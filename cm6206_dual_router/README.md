# CM6206 Dual Router (2 Windows outputs → one 7.1 device)

Pick two Windows playback endpoints (usually two virtual devices), and route them into one physical 7.1 output (CM6206-class USB adapters included).

Note on the long-term architecture:
- Current releases ingest audio via WASAPI loopback capture of the chosen render endpoints.
- The planned “for real” approach is a SysVAD/WaveRT virtual audio driver that creates two endpoints and exposes audio to the router via IOCTL/shared memory. See: `docs/virtual_audio_driver/00_plan.md`.

Inputs:
- **Music** device (full range)
- **Shaker** device (bass-only)

Output:
- One **7.1 render endpoint** (your USB adapter)

Shaker distribution defaults:
- FL/FR get L/R
- BL/BR copy L/R
- SL/SR copy L/R
- LFE = (L+R) mono

## What you must install (one-time)
1) **.NET 8 SDK (x64)**
- Download: https://dotnet.microsoft.com/download/dotnet/8.0

2) Two virtual playback devices (already on your machine if you’re using Voicemeeter)
- Examples:
  - `Voicemeeter Input (VB-Audio Voicemeeter VAIO)`
  - `Voicemeeter AUX Input (VB-Audio Voicemeeter AUX VAIO)`

## Build
From this folder:
```powershell
cd "cm6206_dual_router"
dotnet restore
dotnet build -c Release
```

## Quick start (UI)
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --ui --config router.json
```

1) In **Devices**, pick:
- Music input device
- Shaker input device
- Output device

2) Click **Start**.

Tip: If you want normal apps to go to the Music input automatically, set your Windows default output to the Music virtual device (not the CM6206).

## Run
List device names on your system:
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --list-devices
```

Show each device's Windows mix format (helps verify your virtual endpoints are advertising 5.1/7.1 when desired):
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --list-devices --show-formats
```

Edit `router.json` next (device names must match your system).

Then:
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --config router.json
```

Smoke-test template:
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --ui --config router.smoke.json
```
`router.smoke.json` is a template: replace the device names with ones from `--list-devices`.

### UI mode (tabs + per-channel adjustment)
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --ui --config router.json
```

UI tabs:
- **Devices**: pick music/shaker/output endpoints + Start/Stop
-  - Includes simple **Profiles** (Save As / Load / Delete)
  - Includes **round-trip latency measurement** (requires a selected mic/line-in and a physical loopback cable)
- **DSP**: gains, shaker HP/LP, latency, sample rate + exclusive-mode toggle
  - Includes an **Output format helper**:
    - Shows the **Windows mix format** (what Shared mode actually runs at)
    - Warns when **Shared mode ignores your chosen sample rate**
    - Can **probe Exclusive-mode rates** and let you **blacklist** ones that crackle/glitch
- **Channels**: per-channel gain, **remap**, mute, **solo**, invert (plus a quick Side↔Rear swap)
  - Visual 7.1 map supports **drag-to-remap** by swapping channel assignments
- **Calibration**: play test tone/noise per channel to verify wiring & mapping
  - Supports **sine**, **pink/white noise**, and a **log sweep** generator
  - Optional **Voice prompts** checkbox to speak the channel name
  - Optional **Auto-step channels** mode to cycle FL→…→SR automatically
  - **Preset** dropdown can lock signal to Sine/Pink or alternate Sine↔Pink per step

Or run the built exe:
- `bin\\Release\\net8.0-windows\\Cm6206DualRouter.exe --config router.json`

## Installer builds
If you want a normal Windows installer (and an optional CM6206 driver install step), see the repo root README for the Inno Setup installer.

## Notes / limitations
- Creating new playback devices *from scratch* requires a signed driver.
- Current releases of this app use whatever virtual devices you already have (Voicemeeter/VB-CABLE/etc.) and routes them.
- If you want to pursue the “for real” driver approach (two dedicated virtual playback endpoints), see: `docs/virtual_audio_driver/00_plan.md`.
- If you set Windows default output to the `Music` virtual device, normal apps will go there automatically.

## Important Windows audio setup (CM6206 gotcha)
This app outputs 7.1 audio. If Windows is set to **Stereo** for your USB adapter, starting the router in Shared mode can fail.

In Windows Sound settings for the output device:
- **Configure speakers**: set to **7.1**
- **Advanced**: choose a reasonable default format (48 kHz is usually safest)

## Config knobs (router.json)
- `musicGainDb`, `shakerGainDb`: independent level controls.
- `shakerHighPassHz`, `shakerLowPassHz`: bass shaker band-pass.
- `musicHighPassHz`, `musicLowPassHz`: optional music filtering (omit or set `null` for none).
- `shakerNudgesEnabled`: enables gentle time nudges to keep the shaker input buffer near a target fill level.
- `shakerNudgeTargetBufferMs`: target buffered audio (ms) for shaker ingest (used only when nudges are enabled).
- `shakerNudgeDeadbandMs`: +/- deadband around target (ms) where no correction happens.
- `rearGainDb`, `sideGainDb`, `lfeGainDb`: per-group trims for shaker distribution.
- `useCenterChannel`: optional mono feed to center.
- `sampleRate`: preferred output sample rate.
  - **Exclusive mode**: the app tries to open *exactly* this format.
  - **Shared mode**: Windows uses the device **mix format** sample rate; the app will show you the effective rate in the UI.
- `blacklistedSampleRates`: optional list of sample rates to avoid when probing/falling back in Exclusive mode (useful for adapters that “support” 192kHz but glitch).
- `latencyMs`: output latency (lower = snappier, higher = safer).
- `channelGainsDb`: per-channel trims in dB for FL,FR,FC,LFE,BL,BR,SL,SR.
- `outputChannelMap`: per-channel routing map (indices 0..7) to fix Side/Rear swap etc.
- `channelMute`, `channelSolo`, `channelInvert`: per-channel boolean flags.
- `useExclusiveMode`: tries WASAPI exclusive mode (can fail if the device/format isn’t supported).

## Profiles
- Profiles are stored as **separate JSON files** at `%AppData%\Cm6206DualRouter\profiles\*.json`.
- A profile is a named snapshot of settings (mapping, gains, DSP, calibration preferences, etc.).
- Each profile can optionally include `processNames` (EXE names like `game.exe`) so the app can auto-switch when it detects a matching process running.
- In the UI: use **Import...** to copy a profile JSON into the profiles folder, or **Open folder** to manage them manually.

## Troubleshooting
- If you hear feedback/echo: don’t route the CM6206 output back into one of the input virtual devices.
- If the app can’t find a device: copy the exact name from Windows Sound settings.
- Latency measurement: connect the CM6206 output to your chosen capture device (line-in preferred), set input levels so the click is visible but not clipping.
- If Start fails in Shared mode: confirm the output device is configured as **7.1** (not Stereo) in Windows.
