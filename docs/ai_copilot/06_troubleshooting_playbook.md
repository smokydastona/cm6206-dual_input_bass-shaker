# Troubleshooting Playbook (State-Based)

This is the “Why isn’t this working?” guide aligned with the router’s *existing* monitoring logic.

Principles:
- Use **state** and device availability checks.
- Do not claim to “hear” audio.
- Prefer single-cause / single-fix suggestions.

## 1) Output not selected
Symptoms:
- `OutputDevice` is null/empty.
- `HealthText` indicates selecting an output.

Likely cause:
- Output device not chosen yet.

Fix:
- Ask user to select the output device in Simple/Devices tab.
- Optional proposed actions:
  - (none) because it requires the user to pick a real device name.

## 2) Output device disconnected
Symptoms:
- `OutputDevice` is non-null but `OutputOk == false`.

Likely causes:
- USB adapter unplugged.
- Windows disabled the device.
- FriendlyName changed.

Fix:
- Suggest re-plugging/reenabling device.
- Propose `refresh_devices`.
- Then ask user to re-select output device.

## 3) Router running, but no Game Source audio detected
Symptoms:
- `RouterRunning == true`
- `GamePeak` stays below ~0.0015 for ~3 seconds (the app uses a threshold/time window)
- Health line shows a warning.

Likely causes:
- Wrong “Game Source” selected (game playing on another endpoint).
- Game is paused or muted.
- Capturing the wrong device (e.g., capturing the physical output instead of a virtual bus).

Fix:
- Ask: “Where is the game audio playing (which Windows output device)?”
- Suggest switching Game Source to the default system render device.
- Optional proposed actions:
  - `set_game_source` to the default system render name used by the app.

## 4) Shaker enabled but LFE output looks silent
Symptoms:
- `RouterRunning == true`
- `ShakerEnabled == true`
- `GamePeak` is clearly active (e.g., > 0.01)
- `OutputLfePeak` stays low for ~3 seconds

Likely causes:
- Shaker strength is too low.
- Output channel mapping routes LFE somewhere else.
- Physical wiring: amp/transducer not connected to the expected output.

Fix:
- Suggest increasing shaker strength modestly (+3 to +6 dB).
- Suggest verifying channel mapping and calibration tone for LFE.
- Optional proposed actions:
  - `set_shaker_strength_db` (+6 dB, clamped)

## 5) LFE close to clipping
Symptoms:
- `RouterRunning == true`
- `OutputLfePeak > ~0.95`

Likely causes:
- Shaker strength too high.
- Input is hot.

Fix:
- Reduce shaker strength.
- Optional proposed actions:
  - `set_shaker_strength_db` (-3 dB, clamped)

## 6) Shaker routing disabled
Symptoms:
- `RouterRunning == true`
- `ShakerEnabled == false`
- `GamePeak` clearly active (> 0.01)

Likely cause:
- Preset/matrix disables LFE routing.

Fix:
- Suggest switching to a shaker-enabled preset or enabling shaker routing.
- Optional proposed actions:
  - `apply_simple_preset` = "Game + Bass Shaker"
  - Or `set_shaker_mode` = "always" / "gamesOnly" (if the matrix is active)

## 7) Shaker enabled but strength very low
Symptoms:
- `RouterRunning == true`
- `ShakerEnabled == true`
- `ShakerStrengthDb <= -50`

Fix:
- Suggest increasing shaker strength.
- Optional proposed actions:
  - `set_shaker_strength_db` to something moderate (e.g., -18..-6)

## Clarification prompts to use
- “Rear speakers” → ask if user means Back (BL/BR) or Side (SL/SR) or both.
- “Make shaker stronger” → ask if they want ‘a little’ (+3 dB) or ‘noticeably’ (+6 dB).
- “Music Clean” preset but Secondary Source is None → clarify where music is coming from.
