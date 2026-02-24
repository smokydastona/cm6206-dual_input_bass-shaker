# Minecraft Haptic Engine (Telemetry -> Real-time Haptics)

This module turns Minecraft telemetry (WebSocket JSON or UDP JSON) into real-time haptic audio buses — a SimHub-style pipeline, but tailored to Minecraft and your multichannel hardware routing.

## What it does
- Listens to telemetry packets (`type: telemetry`) and event packets (`type: event`) over **WebSocket** (default) and/or **UDP**.
- Maps packets to haptic effects via a **JSON mapping file**.
- Synthesizes waveforms in real time using 4 primitives:
  - `sine`, `noise`, `impulse`, `sweep`
- Outputs **one audio stream per bus** to Windows render endpoints (VB-Cable / Voicemeeter / ASIO via a virtual endpoint, etc.).
- Per-effect: gain, envelope, optional HP/LP filters, per-channel routing weights.
- Per-bus: gain, HP/LP filters, configurable delay (latency compensation).

## Quick start
1) List audio device names:
```powershell
cd "minecraft_haptic_engine"
dotnet run -c Release -- --list-devices
```

2) Edit the config:
- `config/engine.json`
  - Set `buses.<busName>.renderDeviceName` to match your device names

3) Run:
```powershell
cd "minecraft_haptic_engine"
dotnet run -c Release -- --config config/engine.json
```

## Calibration click
To generate a repeating click on a bus for external latency measurement:
```powershell
cd "minecraft_haptic_engine"
dotnet run -c Release -- --config config/engine.json --calibrate rumble
```

## Telemetry source
By default it connects to:
- `ws://127.0.0.1:7117/`

Override at runtime:
```powershell
cd "minecraft_haptic_engine"
dotnet run -c Release -- --config config/engine.json --ws ws://127.0.0.1:7117/
```

## Notes
- This project targets `net8.0-windows` and uses WASAPI via NAudio.
- For 7.1 routing: set a bus `channels` to `8` and provide `route.weights` with 8 values per effect (FL, FR, FC, LFE, BL, BR, SL, SR).
