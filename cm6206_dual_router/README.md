# CM6206 Dual Virtual Router (2 virtual outputs -> 1 CM6206 7.1)

This is the "simple dual virtual device program" you described:
- You pick **two Windows playback devices** as inputs (your virtual endpoints):
  - `Music` device (full range)
  - `Shaker` device (bass-only)
- The app captures both via **WASAPI loopback** and outputs to **one physical device**:
  - `CM6206 Speakers (7.1)`

It also supports bass-only processing + 7.1 shaker distribution:
- FL/FR get L/R
- RL/RR copy L/R
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

## Run
List device names on your system:
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --list-devices
```

Edit `router.json` next (device names must match your system).

Then:
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --config router.json
```

### UI mode (tabs + per-channel adjustment)
```powershell
cd "cm6206_dual_router"
dotnet run -c Release -- --ui --config router.json
```

UI tabs:
- **Devices**: pick music/shaker/output endpoints + Start/Stop
- **DSP**: gains, shaker HP/LP, latency, exclusive-mode toggle
- **Channels**: per-channel gain, **remap**, mute, invert (plus a quick Side↔Rear swap)
- **Calibration**: play test tone/noise per channel to verify wiring & mapping
  - Optional **Voice prompts** checkbox to speak the channel name

Or run the built exe:
- `bin\\Release\\net8.0-windows\\Cm6206DualRouter.exe --config router.json`

## Notes / limitations
- Creating new playback devices *from scratch* requires a signed driver. This app instead uses whatever virtual devices you already have (Voicemeeter/VB-CABLE/etc.) and routes them.
- If you set Windows default output to the `Music` virtual device, normal apps will go there automatically.

## Config knobs (router.json)
- `musicGainDb`, `shakerGainDb`: independent level controls.
- `shakerHighPassHz`, `shakerLowPassHz`: bass shaker band-pass.
- `musicHighPassHz`, `musicLowPassHz`: optional music filtering (omit or set `null` for none).
- `rearGainDb`, `sideGainDb`, `lfeGainDb`: per-group trims for shaker distribution.
- `useCenterChannel`: optional mono feed to center.
- `latencyMs`: output latency (lower = snappier, higher = safer).
- `channelGainsDb`: per-channel trims in dB for FL,FR,FC,LFE,BL,BR,SL,SR.
- `outputChannelMap`: per-channel routing map (indices 0..7) to fix Side/Rear swap etc.
- `channelMute`, `channelInvert`: per-channel boolean flags.
- `useExclusiveMode`: tries WASAPI exclusive mode (can fail if the device/format isn’t supported).

## Troubleshooting
- If you hear feedback/echo: don’t route the CM6206 output back into one of the input virtual devices.
- If the app can’t find a device: copy the exact name from Windows Sound settings.
