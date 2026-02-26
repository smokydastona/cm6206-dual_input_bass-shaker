# Authoritative spec — Virtual Audio Driver track (SysVAD/WaveRT + IOCTL pull)

This document is the **source of truth** for the “for real” virtual-endpoints pipeline.

## Locked-in architecture
High-level stack:
- User app: CM6206 Dual Router (this repo)
- IPC: IOCTL pull (event + pull) + per-endpoint ring buffer in kernel
- Kernel: virtual audio endpoint driver based on **WDK SysVAD** (`audio/sysvad`)
- Virtual Playback #1: `Virtual Game Audio`
- Virtual Playback #2: `Virtual Shaker Audio`
- SysAudio / MMDevAPI
- Windows Sound Control Panel: two normal playback devices, per-app selectable

## Non-negotiables
- Base the driver on `audio/sysvad`.
- Use **WaveRT** virtual miniports (SysVAD pattern). Do **not** use WaveCyclic.
- Expose **two separate render endpoints** (two independent MMDevAPI playback devices).
- Router ingestion is via a **driver interface** (IOCTL/shared memory), not WASAPI loopback.

Important technical note:
- SysVAD is a **PortCls + WaveRT miniport** sample. “WaveRT miniport” is the critical modern model; the explicit anti-goal is **WaveCyclic**, not “PortCls in general”.

## Endpoint requirements
- Both endpoints must behave like normal speakers in Windows:
  - selectable in Sound Settings
  - selectable per-app
- Friendly names must be exactly:
  - `Virtual Game Audio`
  - `Virtual Shaker Audio`

## Phases
### Phase 1 — Driver baseline from SysVAD
Goal: a working, renamed SysVAD clone that installs and plays.
- Start from the correct sample: `audio/sysvad`
- Build as-is first
- Confirm it installs and exposes its default virtual render device
- Rename & re-ID:
  - provider/manufacturer strings
  - driver name + service name (example: `cmvadr`)
  - stable, unique HWIDs (example root-enumerated IDs)
- Keep the WaveRT plumbing intact (position reporting, notifications, buffering model)

### Phase 2 — Two render endpoints
Goal: Windows shows **two** independent playback devices; both render audio.
- Duplicate/instantiate the render endpoint (“speaker” style endpoint) twice
- Register two separate endpoint filters/descriptors (unique KS names)
- INF registers two endpoints with distinct friendly names

### Phase 3 — Data path to user mode (router owns samples)
Goal: router can read PCM from both endpoints in real time.
- Kernel side:
  - ring buffer per endpoint (Game + Shaker)
  - WaveRT render stream writes into the ring buffer
  - signal availability via an event or by completing a pending IOCTL
- User side (router):
  - open per-endpoint device interfaces:
    - `\\.\CMVADR_Game`
    - `\\.\CMVADR_Shaker`
  - issue IOCTL read requests on each device handle
  - push PCM into the router’s processing graph

### Phase 4 — INF, signing, HLK reality
- Dev: test signing + clean VM
- Pre-release sanity: basic HLK audio tests (render stability, power transitions, sleep/resume)
- Release: attestation signing / HLK pipeline as needed

### Phase 5 — Router integration
- Device selection remains the two friendly names
- Ingestion changes from WASAPI loopback → IOCTL pull
- DSP/routing stays: channel mapping, shaker filtering, CM6206 output

## Linked docs
- Implementation plan: `docs/virtual_audio_driver/00_plan.md`
- SysVAD fork checklist: `docs/virtual_audio_driver/01_sysvad_patch_map.md`
- IOCTL contract (draft): `docs/virtual_audio_driver/02_ioctl_contract.md`
