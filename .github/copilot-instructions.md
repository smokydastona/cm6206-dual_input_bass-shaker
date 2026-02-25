# Copilot Instructions — Better Sound (CM6206 Dual Router)

This repo is a **.NET 8 Windows audio app** that uses **NAudio + WASAPI**.

## Repo layout (source of truth)
- `cm6206_dual_router/`: WinForms router that captures **two render endpoints** (WASAPI loopback) and outputs **one 7.1 render endpoint**.
- `cm6206_extracted/`: vendor driver bundle reference — treat as a **static artifact** (avoid editing unless you’re intentionally updating vendor files).

## Build & run (common CLI flags)
- Uses `System.CommandLine`.
- Router:
  - `dotnet run -c Release -- --list-devices`
  - `dotnet run -c Release -- --ui --config router.json`
  - `dotnet run -c Release -- --config router.json`

## CM6206 Dual Router: core data flow + conventions
- Entry: `cm6206_dual_router/Program.cs` → `WasapiDualRouter` (headless) or `RouterMainForm` (UI).
- Pipeline: `WasapiLoopbackCapture` (music + shaker) → `BufferedWaveProvider` → `RouterSampleProvider` → `WasapiOut`.
- Output is **7.1 float** with channel order: `FL, FR, FC, LFE, BL, BR, SL, SR` (see `RouterSampleProvider`).
- Config is JSON (`router.json`) parsed with `System.Text.Json` (case-insensitive, trailing commas allowed). Arrays like `channelGainsDb`, `outputChannelMap`, `channelMute/Invert/Solo` must be **length 8** (validated in `RouterConfig.Validate()`).
- Shared vs Exclusive:
  - Shared mode uses the device **Windows mix format**; `OutputFormatNegotiator` may override `sampleRate` and warns.
  - Shared mode requires the output device to be configured as **7.1** in Windows, otherwise startup fails.
- Profiles: stored as separate JSON files under `%AppData%\Cm6206DualRouter\profiles\*.json` (see `ProfileStore`).

## Project conventions / guardrails
- Target framework is `net8.0-windows`; nullable is enabled (`<Nullable>enable</Nullable>`). Prefer `record` configs and immutable updates (e.g., `config with { Telemetry = ... }`).
- Device selection is by **exact friendly name**; keep UX changes consistent with the existing “list devices → copy/paste name into JSON” flow.
- Avoid introducing audio clicks/glitches: keep per-sample math simple; prefer stable smoothing at effect/mixer level over aggressive limiting.

## Version control (keep CI in sync)
- After each **completed logical change set** (e.g., “fix build error”, “add feature”, “refactor X”), run `git status`, then:
  - `git add -A`
  - `git commit -m "<short message>"`
  - `git push`
- If `git push` can’t run (no remote / no auth), clearly say so and stop after committing.

### CI terminology: “push” vs “tag and push”
This repo has two GitHub Actions workflows:
- **Push builds** (`.github/workflows/build-windows.yml`): triggered by `git push` to `main` and PRs. Produces build artifacts but does **not** create a GitHub Release.
- **Tag releases** (`.github/workflows/release-windows.yml`): triggered only when a version tag is pushed (e.g. `v1.2.3`). Builds + packages a versioned ZIP and creates a GitHub **Release**.

When the user says:
- **“push”**: do the normal `git push` (no tagging). This is for day-to-day commits and CI build artifacts.
- **“tag and push”**: create a version tag and push it so a Release is created:
  - `git tag v<major>.<minor>.<patch>`
  - `git push origin v<major>.<minor>.<patch>`

