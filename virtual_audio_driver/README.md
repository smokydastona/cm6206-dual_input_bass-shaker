# Virtual Audio Driver (planned)

This folder is reserved for the future **kernel-mode virtual audio endpoint driver** that will create two playback devices:
- `Virtual Game Audio`
- `Virtual Shaker Audio`

Each endpoint must behave like a normal Windows render device:
- selectable in Sound Settings
- selectable per-app in the Windows volume mixer

The CM6206 router will consume audio from these endpoints via a driver interface (shared memory / IOCTL), then mix and output to the physical CM6206 7.1 device.

## Status
Not implemented in this repo yet.

## Bootstrap (get the correct starting point)
This project must be based on the WDK SysVAD sample (`audio/sysvad`). This repo intentionally does **not** vendor Microsoft’s sample source.

To fetch a local working copy (ignored by git):
```powershell
./scripts/fetch_sysvad.ps1
```

That will download and extract SysVAD to:
- `virtual_audio_driver/src/sysvad/`

From there, you’ll open the SysVAD solution in Visual Studio with WDK installed and follow the patch-map checklist to:
- duplicate the render endpoint (two playback devices)
- rename/brand everything
- add the IOCTL/event+pull interface for the router

## Design / build plan
See:
- docs/virtual_audio_driver/ARCHITECTURE_SPEC.md
- docs/virtual_audio_driver/00_plan.md
- docs/virtual_audio_driver/01_sysvad_patch_map.md
