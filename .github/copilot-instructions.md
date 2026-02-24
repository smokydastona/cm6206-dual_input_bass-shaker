# Copilot Instructions — Better Sound (CM6206 Dual Router + Minecraft Haptic Engine)

This repo is primarily **two .NET 8 Windows audio apps** that use **NAudio + WASAPI**.

## Repo layout (source of truth)
- `cm6206_dual_router/`: WinForms router that captures **two render endpoints** (WASAPI loopback) and outputs **one 7.1 render endpoint**.
- `minecraft_haptic_engine/`: Console engine that turns **telemetry JSON** (WebSocket/UDP) into **real-time synthesized haptic audio buses**.
- `cm6206_extracted/`: vendor driver bundle reference — treat as a **static artifact** (avoid editing unless you’re intentionally updating vendor files).

## Build & run (common CLI flags)
- Both apps use `System.CommandLine`.
- Router:
  - `dotnet run -c Release -- --list-devices`
  - `dotnet run -c Release -- --ui --config router.json`
  - `dotnet run -c Release -- --config router.json`
- Haptic engine:
  - `dotnet run -c Release -- --list-devices`
  - `dotnet run -c Release -- --config config/engine.json --ws ws://127.0.0.1:7117/`
  - `dotnet run -c Release -- --config config/engine.json --calibrate rumble`

## CM6206 Dual Router: core data flow + conventions
- Entry: `cm6206_dual_router/Program.cs` → `WasapiDualRouter` (headless) or `RouterMainForm` (UI).
- Pipeline: `WasapiLoopbackCapture` (music + shaker) → `BufferedWaveProvider` → `RouterSampleProvider` → `WasapiOut`.
- Output is **7.1 float** with channel order: `FL, FR, FC, LFE, BL, BR, SL, SR` (see `RouterSampleProvider`).
- Config is JSON (`router.json`) parsed with `System.Text.Json` (case-insensitive, trailing commas allowed). Arrays like `channelGainsDb`, `outputChannelMap`, `channelMute/Invert/Solo` must be **length 8** (validated in `RouterConfig.Validate()`).
- Shared vs Exclusive:
  - Shared mode uses the device **Windows mix format**; `OutputFormatNegotiator` may override `sampleRate` and warns.
  - Shared mode requires the output device to be configured as **7.1** in Windows, otherwise startup fails.
- Profiles: stored as separate JSON files under `%AppData%\Cm6206DualRouter\profiles\*.json` (see `ProfileStore`).

## Minecraft Haptic Engine: core data flow + packet schema
- Entry: `minecraft_haptic_engine/src/Program.cs` → `Engine/HapticEngine`.
- Telemetry input: `Telemetry/WebSocketTelemetryClient` and/or `Telemetry/UdpTelemetryListener` push raw JSON strings.
- Packet schema parsed by `TelemetryParser` (expects JSON fields):
  - `type` (required), `t` (timestamp ms), optional `id`, `kind`, and telemetry fields `speed`, `accel`, `elytra`.
- Engine loop: queue packets → update current telemetry →
  - continuous effects updated each tick (`BusEngine.UpdateContinuous`)
  - oneshots matched by `{ type, id, kind }` and triggered (`BusEngine.TriggerOneShot`).
- Audio: per bus `WasapiBusOutput` → `BusSampleProvider` (fixed-size chunks for predictable latency) → `Synthesis/EffectMixer`.
- Routing: `route.preset` (see `Synthesis/RoutePresets.cs`) or explicit `route.weights` (one weight per channel).

## Project conventions / guardrails
- Target framework is `net8.0-windows`; nullable is enabled (`<Nullable>enable</Nullable>`). Prefer `record` configs and immutable updates (e.g., `config with { Telemetry = ... }`).
- Device selection is by **exact friendly name**; keep UX changes consistent with the existing “list devices → copy/paste name into JSON” flow.
- Avoid introducing audio clicks/glitches: keep per-sample math simple; prefer stable smoothing at effect/mixer level over aggressive limiting.

## Version control (keep CI in sync)
- After each **completed logical change set** (e.g., “fix build error”, “add feature”, “refactor X”), run `git status`, then:
  - `git add -A`
  - `git commit -m "<short message>"`
  - `git push`
- If `git push` can’t run (no remote / no auth), clearly say so and stop after committing.

