# Copilot Instructions — Better Sound (CM6206 Dual Router)

This repo is a **.NET 8 Windows audio app** that uses **NAudio + WASAPI**.

## Repo layout (source of truth)
- `cm6206_dual_router/`: WinForms router that captures **two render endpoints** (WASAPI loopback) and outputs **one 7.1 render endpoint**.
- `cm6206_extracted/`: vendor driver bundle reference — treat as a **static artifact** (avoid editing unless you’re intentionally updating vendor files).
- `cm6206_driver_payload/`: minimal WIN10 driver payload committed for the installer/CI (INF/CAT + x86/x64 binaries).

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
- After each **completed logical change set** (e.g., “fix build error”, “add feature”, “refactor X”), you MUST run the **Pre-Commit Full Scan** below.
- Only after the scan passes, run:
  - `git status`
  - `git add -A`
  - `git commit -m "<short message>"`
  - `git push`
- If `git push` can’t run (no remote / no auth), clearly say so and stop after committing.

## Pre-Commit Full Scan (MANDATORY before every commit/push)
You are an autonomous code-review + repair agent. Before **every** `git commit` or `git push`, run this exact workflow across the whole repo, fix everything you find, then re-scan until clean.

### 1) Recursive scan (entire workspace)
- Scan *all* source/config/scripts/CI/docs/installer/assets.
- Identify and fix:
  - Compile/build errors
  - Runtime crash risks and obvious deadlocks/hangs
  - Broken paths/resource loading
  - CI/script issues and incorrect relative paths
  - Docs/setup instructions that no longer match reality

### 2) Required validations
Run the validations that are possible in the current environment. If a required tool is missing (e.g., .NET SDK), do NOT proceed to commit/push; report the blocker.

- **Static analysis / editor diagnostics**
  - Use the workspace error scan (`get_errors`) for the whole repo.

- **Build (Release)**
  - `dotnet build "cm6206_dual_router/Cm6206DualRouter.csproj" -c Release`
  - `dotnet build "tools/Cm6206AssetGenerator/Cm6206AssetGenerator.csproj" -c Release`

- **Installer sanity (when Inno Setup is available)**
  - Compile `installer/Cm6206DualRouter.iss` with `ISCC.exe` using the same relative paths CI uses.
  - Confirm the output lands under `artifacts/installer/`.

- **CI sanity**
  - Verify workflows reference paths that exist in-repo (especially `cm6206_driver_payload/`, `tools/Cm6206AssetGenerator`, and `artifacts/...`).

### 3) Iteration rule
- After each fix: re-run the scan + validations; assume fixes can introduce new issues.
- Stop only when there are **zero build errors** and **no warnings of functional significance**.

### 4) Output requirements (every time)
Maintain a running log in your response:
- Issue found
- File(s) affected
- Exact fix applied
- Reasoning

When complete, state:
- **ALL SYSTEMS PASS**
- A concise change summary

### CI terminology: “push” vs “tag and push”
This repo has two GitHub Actions workflows:
- **Push builds** (`.github/workflows/build-windows.yml`): triggered by `git push` to `main` and PRs. Produces build artifacts but does **not** create a GitHub Release.
- **Tag releases** (`.github/workflows/release-windows.yml`): triggered only when a version tag is pushed (e.g. `v1.2.3`). Builds + packages a versioned ZIP and creates a GitHub **Release**.

When the user says:
- **“push”**: do the normal `git push` (no tagging). This is for day-to-day commits and CI build artifacts.
- **“tag and push”**: create a version tag and push it so a Release is created:
  - `git tag v<major>.<minor>.<patch>`
  - `git push origin v<major>.<minor>.<patch>`

