# Minecraft Haptic Engine — Offline Overview

This repo also contains `minecraft_haptic_engine/`, a console app that converts telemetry JSON into synthesized “haptic audio buses”.

## Purpose
- Read telemetry from WebSocket and/or UDP.
- Parse JSON packets into a normalized internal telemetry state.
- Drive real-time synthesis effects per “bus”.
- Output audio via WASAPI to a selected render endpoint.

## Telemetry packet shape (high-level)
The parser expects JSON fields like:
- `type` (required)
- `t` (timestamp ms)
- optional `id`, `kind`
- telemetry fields: `speed`, `accel`, `elytra` (depending on event)

Conceptually:
- Continuous effects update each tick.
- One-shots are matched by `{ type, id, kind }`.

## Audio architecture (high-level)
- Per-bus output: WASAPI output stream
- Bus sample generation: fixed-size chunks for predictable latency
- Mixer combines synthesis effects and routes by presets or explicit weights

## Routing semantics
- `route.preset` selects a known routing preset.
- `route.weights` provides explicit per-channel weights.

## Important constraints
- This engine’s “audio” is synthesized control signals intended for transducers.
- Avoid aggressive per-sample limiting; prefer smoothing at effect/mixer level.

(For more details, see `minecraft_haptic_engine/README.md` and the code under `minecraft_haptic_engine/src/`.)
