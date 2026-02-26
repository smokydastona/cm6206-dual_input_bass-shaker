# Router ↔ Virtual Audio Driver IOCTL contract (locked)

This is the **user-mode contract** the CM6206 router will use to ingest PCM from the two virtual render endpoints implemented in a SysVAD/WaveRT fork.

Scope:
- Two endpoints:
  - `Virtual Game Audio` (ID 0)
  - `Virtual Shaker Audio` (ID 1)
- **Event + pull** model (acceptable latency; easier bring-up/debug)
- Minimal stable IOCTL surface

Non-goals:
- Defining the entire WaveRT internals
- Using WASAPI loopback as the primary ingestion path

## Device model
Expose **two per-endpoint device interfaces** (separate from the audio endpoint filters) that the router opens:
- `\\.\CMVADR_Game`
- `\\.\CMVADR_Shaker`

The router will:
1) `CreateFile` on each endpoint device
2) query version + negotiated format for each
3) (optionally) register an event for “data available” on each
4) read PCM frames via IOCTL from each device handle

## IOCTLs (exact)
### Codes
```c
#define IOCTL_CMVADR_OPEN_STREAM CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED,  FILE_READ_DATA)
#define IOCTL_CMVADR_READ        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_OUT_DIRECT, FILE_READ_DATA)
#define IOCTL_CMVADR_GET_FORMAT  CTL_CODE(FILE_DEVICE_UNKNOWN, 0x803, METHOD_BUFFERED,  FILE_READ_DATA)
```

### Structs
```c
typedef struct _CMVADR_AUDIO_FORMAT {
  UINT32 SampleRate;
  UINT32 BitsPerSample;
  UINT32 Channels;
} CMVADR_AUDIO_FORMAT;

typedef struct _CMVADR_READ_REQUEST {
  UINT32 RequestedFrames;
} CMVADR_READ_REQUEST;

typedef struct _CMVADR_READ_RESPONSE {
  UINT32 FramesReturned;
  BYTE   Data[1]; // variable length
} CMVADR_READ_RESPONSE;
```

### Semantics
1) `OPEN_STREAM`
- Called once after opening the device handle.

2) `GET_FORMAT`
- Returns the endpoint’s negotiated PCM format (router uses this to size frames and set its input wave format).

3) `READ`
- Input: `CMVADR_READ_REQUEST`
- Output: `CMVADR_READ_RESPONSE` (header + PCM payload)

## PCM format expectations
For early bring-up keep formats intentionally small:
- Game endpoint:
  - 48kHz, 32-bit float, 2/6/8 channels
- Shaker endpoint:
  - 48kHz, 32-bit float, 2 channels

Channel order for 7.1 should match Windows standard:
- `FL, FR, FC, LFE, BL, BR, SL, SR`

## Router-side definitions
The router-side constants/structs live in:
- `cm6206_dual_router/VirtualAudioDriverIoctl.cs`

Driver-side code should match these definitions exactly once implemented.

## Model note (SysVAD/WaveRT)
SysVAD is a PortCls + WaveRT miniport sample. The “modern” requirement here is **WaveRT** (and explicitly not WaveCyclic).
