# Changelog

## Unreleased

## 2026-02-24

### Added
- `cm6206_dual_router/`: dual-input WASAPI loopback router (2 virtual outputs → 1 CM6206 7.1) with shaker filtering + 7.1 distribution.
- Router UI mode (tabbed WinForms) including per-channel dB trims.
- `cm6206_driver_only/`: driver-only install package (manual install without vendor setup UI).
- GitHub Actions workflow to publish a self-contained `win-x64` single-file `.exe` artifact.

### Changed
- Patched vendor CPL profile defaults (`FactoryDefault.xml`) to disable `DefaultDeviceControl` (reduce default-device manipulation).
