# Virtual audio endpoints driver payload (CMVADR)

This folder is the **installer payload** for the kernel-mode virtual audio endpoints driver.

Expected contents (built/signed in the SysVAD fork, then copied here):
- `CMVADR.inf`
- `CMVADR.cat`
- `CMVADR.sys`
- any required co-installers / DLLs (if applicable)

The Inno Setup installer will include this payload **only if this folder exists**, and will offer an optional task:
- “Install Virtual Game/Shaker playback endpoints (advanced)”

As of the current installer script, that task is only shown when `CMVADR.inf` exists in this folder.

Installer behavior:
- Copies this folder to `{tmp}\cm6206_virtual_audio_driver`
- Runs `pnputil /add-driver "CMVADR.inf" /install`

Notes:
- Driver artifacts are intentionally allowed in git under `virtual_audio_driver_payload/` (see `.gitignore`).
- During development you’ll typically install on a VM with test signing enabled.
