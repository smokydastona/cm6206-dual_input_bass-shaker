# AI Copilot Contracts (Offline Reference)

This describes what the copilot *knows* and what it is *allowed to do*.

## Inputs the copilot receives (context snapshot)
The app provides a `CopilotContext` snapshot with these fields:

- `ActiveTab` (string): which UI tab/screen is active.
- `RouterRunning` (bool)
- `GameSource` (string|null): selected primary render endpoint.
- `SecondarySource` (string|null): selected secondary render endpoint.
- `OutputDevice` (string|null): selected output render endpoint.
- `OutputOk` (bool): best-effort “output device still exists” check.
- `SpeakersEnabled` (bool): derived from routing matrix/config.
- `ShakerEnabled` (bool): derived from routing matrix/config.
- `MasterGainDb` (float)
- `ShakerStrengthDb` (float): corresponds to `lfeGainDb` in config.
- `GamePeak` (float): input A peak (state-only indicator).
- `SecondaryPeak` (float): input B peak (state-only indicator).
- `OutputLfePeak` (float): output LFE peak (state-only indicator).
- `OutputPeakMax` (float): max output peak.
- `HealthText` (string): UI status summary.

Important limitation:
- Peaks are used only as a **state signal** (e.g., “audio detected”), not for content analysis.

## Output contract (what the model must return)
The model must return a **single JSON object**:

- `assistantText`: string
- `clarificationQuestion`: string|null
- `clarificationOptions`: string[] (0..5 items)
- `proposedActions`: action[] (may be empty)

If `clarificationQuestion` is non-null:
- `proposedActions` must be empty.

## Action schema
Each action is:

{ 
  "type": string,
  "stringValue": string|null,
  "floatValue": number|null,
  "intValue": number|null,
  "boolValue": boolean|null
}

## Allowed action types (allowlist)
The app will only execute these action types:

- `set_game_source` (stringValue = render device name)
- `set_secondary_source` (stringValue = render device name or "(None)")
- `set_output_device` (stringValue = render device name)
- `apply_simple_preset` (stringValue = one of: "Game + Bass Shaker", "Music Clean", "Game Only", "Shaker Only", "Flat / Debug")
- `set_shaker_strength_db` (floatValue, range -24..+12)
- `set_master_gain_db` (floatValue, range -60..+20)
- `set_shaker_mode` (stringValue = "always" or "gamesOnly")
- `show_advanced_controls` (boolValue)
- `refresh_devices`
- `start_routing`
- `stop_routing`
- `set_channel_mute` (intValue 0-7, boolValue = mute)

## Safety invariants
- The copilot must never claim it executed anything unless the UI confirmed and applied actions.
- The copilot must never invent action types or parameters.
- The copilot must propose the **smallest** sufficient set of actions.
- If the request is ambiguous (especially “rear speakers”), it must ask one clarification question.

## Channel index mapping for `set_channel_mute`
- 0=FL, 1=FR, 2=FC, 3=LFE, 4=BL, 5=BR, 6=SL, 7=SR

If the user says “rear speakers/channels”, clarificationOptions should be:
- `Back (BL/BR)`
- `Side (SL/SR)`
- `Both`
