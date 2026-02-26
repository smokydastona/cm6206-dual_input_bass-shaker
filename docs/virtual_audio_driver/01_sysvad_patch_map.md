# SysVAD fork: patch map (two render endpoints + router IOCTL)

This doc is a checklist for implementing the virtual endpoints driver in a dedicated SysVAD fork **without vendoring Microsoft sample source into this repo**.

Targets:
- WaveRT (SysVAD)
- Two *render* endpoints (two separate MMDevAPI playback devices)
  - `Virtual Game Audio` (multichannel up to 7.1)
  - `Virtual Shaker Audio` (stereo is sufficient)
- Router-facing data path: **event + pull IOCTL model**

Non-goals:
- WaveCyclic
- user-mode “virtual devices”
- WASAPI loopback as the primary ingestion path

## Minimal file touch set (practical checklist)
Across WDK versions, exact filenames can vary slightly, but the minimal *areas* you will touch are stable:
- Driver entry/device registration (`sysvad.cpp` / `driver.cpp` / `device.cpp` depending on version)
- WaveRT miniport implementation (`minwavert.*` and the WaveRT filter descriptor file, often `wavertfilter.cpp`)
- Topology (`mintopo.*`)
- INF (`sysvad.inf`)
- Common IOCTL definitions/dispatch (`common\\*.h/.cpp`)

The authoritative IOCTL contract for this repo is:
- `docs/virtual_audio_driver/02_ioctl_contract.md`
- router-side constants: `cm6206_dual_router/VirtualAudioDriverIoctl.cs`

## 1) Create a SysVAD fork
1) Fetch the official SysVAD sample locally (this repo includes a helper script that keeps the sample out of git):
  - `./scripts/fetch_sysvad.ps1`
  - Output folder (ignored by git): `virtual_audio_driver/src/sysvad/`
2) Create a new repo for your driver fork (recommended) or a long-lived branch.
3) Copy the `audio/sysvad` sample into your fork as the baseline.

Why: SysVAD already has the WaveRT plumbing and the INF structure that HLK expects.

## 2) Branding + identity (must be unique)
Do these before adding endpoints so you don’t chase string/GUID mismatches later.

Checklist:
- New driver/service name
- New manufacturer strings
- New device/interface GUIDs (where appropriate)
- New hardware IDs (even for virtual devices)

Tip: in the SysVAD fork, search for these patterns and update consistently:
- `SYSVAD`
- `Sysvad`
- `SimpleAudioSample` / `SAS`
- `KSNAME_` (endpoint filter names)
- INF `[Strings]` section keys that map to KS filter names

## 3) Add the second render endpoint
You need two distinct render endpoints. Windows will not “split” one endpoint later.

Implementation shape (what to look for in SysVAD):
- A list/array of render endpoint descriptors (often “miniport pairs” or “endpoints”).
- Per-endpoint topology + wave filter descriptors.

How to locate the right place in your SysVAD fork:
- Search for a render endpoints array and duplicate one entry:
  - search text: `g_RenderEndpoints`
  - search text: `ENDPOINT_MINIPAIR`
  - search text: `SpeakerMiniports`

Create two render endpoints:
- Endpoint A:
  - Friendly name: `Virtual Game Audio`
  - KS/topology/wave names: unique
- Endpoint B:
  - Friendly name: `Virtual Shaker Audio`
  - KS/topology/wave names: unique

## 4) Format support (multichannel for Game endpoint)
Goals:
- `Virtual Game Audio` advertises (at minimum):
  - 48 kHz, 32-bit float, 2ch
  - 48 kHz, 32-bit float, 6ch
  - 48 kHz, 32-bit float, 8ch
- `Virtual Shaker Audio` advertises (at minimum):
  - 48 kHz, 32-bit float, 2ch

How to locate format tables:
- Search for the WaveRT pin data ranges / `KSDATARANGE_AUDIO` tables.
- Search text: `KSDATARANGE_AUDIO`
- Search text: `WAVEFORMATEXTENSIBLE`
- Search text: `MaximumChannels`

Note: keep the format set intentionally small at first to reduce negotiation edge cases.

## 5) INF: two endpoints, two friendly names
Your INF must register each endpoint as a separate audio endpoint.

Checklist:
- Class is `MEDIA` and uses the standard audio class GUID.
- Two endpoint instances are declared (two render endpoints).
- Each endpoint has a unique friendly name string.
- KS filter names in code match INF strings.

How to find the right INF sections:
- Search text: `AudioEndpoint`
- Search text: `KSNAME_`
- Search text: `FriendlyName`
- Search text: `[Strings]`

## 6) Router-facing data path: event + pull IOCTL model
We want the router to pull PCM from the driver, per endpoint.

High-level requirements:
- The driver maintains a ring buffer per render endpoint.
- Each render stream write from the audio engine is appended into that buffer.
- The router opens a driver device interface and reads frames via IOCTL.
- An event can be used to signal “data available”.

Design notes:
- Keep IOCTL contracts stable and versioned.
- Keep the IOCTL surface minimal initially:
  - query negotiated format
  - read frames (blocking or non-blocking)
  - optional: get dropped-frame counters

Contract reference (router side + doc):
- `cm6206_dual_router/VirtualAudioDriverIoctl.cs`
- `docs/virtual_audio_driver/02_ioctl_contract.md`

Where to implement:
- Expose per-endpoint device interfaces (one for Game, one for Shaker) separate from the audio endpoint filters.
- Add an IOCTL handler for each that reads from its endpoint ring buffer.

## 7) Integration back into this repo (payload only)
This repo should contain only the driver payload artifacts needed for installation:
- INF
- CAT
- SYS

Target layout (future):
- `virtual_audio_driver_payload/WIN10/Driver/...`

Payload naming (this repo’s installer/scripts expect):
- `CMVADR.inf`
- `CMVADR.cat`
- `CMVADR.sys`

The Inno Setup installer already has a guarded hook for this payload folder.

Note: the installer only offers the virtual driver install task when `CMVADR.inf` exists in `virtual_audio_driver_payload/WIN10/Driver/`.

## 8) Verification checklist
On a dev VM with test signing enabled:
- Install the driver.
- Confirm Sound Settings shows:
  - `Virtual Game Audio`
  - `Virtual Shaker Audio`
- Confirm per-app routing works in the Windows volume mixer.
- Confirm advertised formats using the router CLI:
  - `dotnet run -c Release --project cm6206_dual_router/Cm6206DualRouter.csproj -- --list-devices --show-formats`
- Confirm the driver IOCTL interface responds (once implemented in the fork):
  - `dotnet run -c Release --project cm6206_dual_router/Cm6206DualRouter.csproj -- --probe-cmvadr`
- Confirm the router can ingest PCM via IOCTL and output to CM6206.
