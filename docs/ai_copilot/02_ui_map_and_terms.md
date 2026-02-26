# CM6206 Dual Router — UI Map and Terms

This is a vocabulary map for answering “what am I looking at?” and “what do I click?” questions.

## Tabs / screens (Router UI)
- **Simple**: “2-minute success path” landing screen.
  - Choose devices (Game Source, Secondary Source, Output)
  - Pick a Simple preset
  - Start/Stop routing
  - Shows a single Health/Status line and signal-flow widget
- **Devices**: full device selection, profiles, latency test device selection.
- **DSP**: gains, filters, sample rate / exclusive-mode, latency.
- **Routing**: detailed routing controls (including group routing / matrix UI).
- **Channels**: per-channel gains/mute/solo/invert and output channel mapping.
- **Meters**: real-time level meters for inputs/outputs.
- **Calibration**: tones/noise/sweep and helpers for verifying channel wiring.
- **Diagnostics**: format and device info / state.

(Exact tab names can vary slightly; treat `CopilotContext.ActiveTab` as authoritative for “what screen is the user on”.)

## Device terms
- **Game Source**: the primary render endpoint.
  - Current implementation: captured by WASAPI loopback.
  - Planned driver track: audio is pulled from the virtual driver endpoint via IOCTL.
- **Secondary Source**: optional second render endpoint (can be “(None)” / disabled).
  - Current implementation: captured by WASAPI loopback.
  - Planned driver track: audio is pulled from the virtual driver endpoint via IOCTL.
- **Output device**: the 7.1 render endpoint receiving the final mixed stream.

Important:
- Device names must match exactly.
- If output device is configured as Stereo in Windows, Shared-mode startup can fail.

## Simple presets (conceptual)
Simple Mode presets are shortcuts that set/clear the group routing matrix override.

Common intents:
- **Game + Bass Shaker**: speakers get Game, shaker gets Game bass.
- **Music Clean**: speakers get Secondary, shaker off.
- **Game Only**: speakers get Game, shaker off.
- **Shaker Only**: speakers off, shaker gets Game bass.
- **Flat / Debug**: clears matrix override; advanced controls/mixing mode apply.

## Health/Status text
The app collapses state into a single status line with severity:
- Error states: output not selected / disconnected / last start error
- Ready state: not running
- Warning state: running but no Game Source audio detected
- OK state: running and audio detected

The copilot should interpret this status line as a summary, but still reason from the full `CopilotContext` fields.
