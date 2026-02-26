# CM6206 Dual Router — Architecture

This documents the router app in `cm6206_dual_router/`.

## One-sentence summary
Current implementation: captures up to **two Windows render endpoints** via WASAPI loopback and routes/mixes them into **one 7.1 render endpoint**.

Planned implementation (virtual driver track): apps render to two SysVAD/WaveRT virtual endpoints, and the router ingests audio via a driver interface (shared memory / IOCTL), then mixes to CM6206.

## Core data flow
High-level pipeline (simplified):
1) Input A: `WasapiLoopbackCapture` for “Game/Music Source” (render loopback) (current)
2) Input B: `WasapiLoopbackCapture` for “Secondary Source” (render loopback) (current)
3) Each input feeds a `BufferedWaveProvider`
4) Router combines them via `RouterSampleProvider`
5) Output goes to `WasapiOut` targeting one 7.1 render endpoint

Planned ingestion (driver track):
- Input A/B are render endpoints created by a kernel-mode WaveRT driver (`Virtual Game Audio` / `Virtual Shaker Audio`).
- The router pulls PCM from the driver via IOCTL (event + pull model), instead of loopback capture.

Key constraints:
- Output is **7.1 float**.
- Shared mode uses the Windows device **mix format**; the app can warn/override chosen sample rate.
- Shared mode requires the output device to be configured as **7.1** in Windows.

## Routing mental model
There are two routing layers:

1) “Mixing mode” (coarse):
- A few named strategies for combining inputs.

2) “Group routing matrix override” (precise):
- Optional `groupRouting` (6 rows × 2 cols) that overrides mixing mode.
- Rows: Front, Center, LFE, Rear, Side, Reserved.
- Cols: A (Game/Music Source), B (Secondary Source).

Simple Mode presets primarily manipulate the **group routing matrix override**.

## Profiles and storage
- Router config: user-selected JSON path (often `cm6206_dual_router/router.json`).
- Profiles: separate JSON files stored under `%AppData%\Cm6206DualRouter\profiles\*.json`.
- AI settings: `%AppData%\Cm6206DualRouter\ai_settings.json` (API key is protected at rest per-user).

## Important: device naming
Device selection is by **exact FriendlyName string**.
- UX pattern: “List devices → copy/paste name into JSON / select in UI”.

Note: the friendly-name workflow remains the same in the driver track; what changes is *how* the router ingests the audio (IOCTL pull instead of WASAPI loopback).

## Safety and UX conventions
- The Setup Assistant can propose actions, but **nothing changes without explicit user confirmation**.
- Proactive hints are state-based and should remain non-intrusive.
