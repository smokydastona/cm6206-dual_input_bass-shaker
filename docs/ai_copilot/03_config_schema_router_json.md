# Router Config (`router.json`) — Schema Notes

This file describes the JSON config used by the router app, based on `cm6206_dual_router/RouterConfig.cs`.

## Device selection
- `musicInputRenderDevice` (string): FriendlyName of input A (required in headless mode).
- `shakerInputRenderDevice` (string): FriendlyName of input B (optional in some UI flows).
- `outputRenderDevice` (string): FriendlyName of 7.1 output endpoint (required).
- `latencyInputCaptureDevice` (string|null): Optional capture device for latency measurement.

Note:
- Current implementation ingests audio from `musicInputRenderDevice` / `shakerInputRenderDevice` via WASAPI loopback capture.
- Planned driver track keeps the same friendly-name config fields but changes ingestion to a driver interface (shared memory / IOCTL).

## Output format
- `sampleRate` (int): preferred SR (8k..384k).
- `useExclusiveMode` (bool): attempt WASAPI exclusive.
- `blacklistedSampleRates` (int[]|null): avoid specific SRs during probing/fallback.
- `outputChannels` (int): currently expected to be `8`.

Important:
- In Shared mode, Windows uses the device **mix format**; the UI may warn that your requested SR is not in effect.
- Output device should be configured as **7.1** in Windows.

## Gains
- `musicGainDb` (float)
- `shakerGainDb` (float)
- `masterGainDb` (float): global post-route gain.
- `lfeGainDb` (float): shaker strength control (commonly exposed in Simple Mode).
- `rearGainDb` (float)
- `sideGainDb` (float)

## Filters
- Shaker band-pass:
  - `shakerHighPassHz` (float > 0)
  - `shakerLowPassHz` (float > 0)
- Optional music filters:
  - `musicHighPassHz` (float|null)
  - `musicLowPassHz` (float|null)

## Mixing and routing
- `mixingMode` (string): one of
  - `FrontBoth`, `Dedicated`, `MusicOnly`, `ShakerOnly`, `PriorityMusic`, `PriorityShaker`

- `groupRouting` (bool[]|null): optional override matrix.
  - Must be length `12` (6 rows × 2 cols)
  - Row order: Front, Center, LFE, Rear, Side, Reserved
  - Col order: A (Music), B (Shaker)
  - Stored row-major: index = row*2 + col

## Per-channel controls (7.1 order)
7.1 channel order is:
`FL, FR, FC, LFE, BL, BR, SL, SR`

- `channelGainsDb` (float[]|null): length 8.
- `outputChannelMap` (int[]|null): length 8, values 0..7.
- `channelMute` (bool[]|null): length 8.
- `channelInvert` (bool[]|null): length 8.
- `channelSolo` (bool[]|null): length 8.

## Calibration / test helpers
- `enableVoicePrompts` (bool)
- `calibrationAutoStep` (bool)
- `calibrationPreset` (string): `Manual`, `IdentifySine`, `LevelPink`, `AlternateSinePink`
- `calibrationStepMs` (int)
- `calibrationLoop` (bool)

## Latency
- `latencyMs` (int): output latency (10..500)

## Validation rules (summary)
- Arrays like `channelGainsDb`, `outputChannelMap`, `channelMute/Invert/Solo` must be length 8.
- `groupRouting` must be length 12.
- `shakerHighPassHz < shakerLowPassHz`.
