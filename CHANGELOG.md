# Changelog

## Unreleased

### Added
- Per-channel output remap + mute/solo/invert.
- Calibration improvements: voice prompts, auto-step, and presets (including Sine/Pink alternation).
- Calibration signal generator adds a sweep mode.
- Round-trip latency measurement (requires selecting a capture device and physically looping output to input).
- Visual 7.1 drag-to-remap mapper (swap via drag/drop).
- Profiles in the UI (Save As / Load / Delete) stored as separate files in `%AppData%\\Cm6206DualRouter\\profiles\\*.json`.
- Optional per-app auto-switching by process EXE name (profiles can declare `processNames`).
- Output format helper: shared-mode mix-rate warnings, exclusive-format probing, and optional blacklisting of unstable sample rates.
- Preferred sample-rate selector in the UI (used for Exclusive mode).
- Updated READMEs and sample `router.json` to document sample-rate negotiation, probing/blacklisting, and the Windows 7.1 setup requirement.

## 2026-02-24

### Added
- `cm6206_dual_router/`: dual-input router (current: WASAPI loopback capture; planned: SysVAD/WaveRT driver + IOCTL ingestion) with shaker filtering + 7.1 distribution.
- Router UI mode (tabbed WinForms) including per-channel dB trims.
- GitHub Actions workflow to publish a self-contained `win-x64` single-file `.exe` artifact.

### Changed
- Patched vendor CPL profile defaults (`FactoryDefault.xml`) to disable `DefaultDeviceControl` (reduce default-device manipulation).
