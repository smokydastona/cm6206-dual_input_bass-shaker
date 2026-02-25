# Audio Channel Mapping and Indexing

This is the canonical mapping for the router’s output channels.

## 7.1 channel order
Output is 8 channels in this order:

Index → Channel
- 0 → Front Left (FL)
- 1 → Front Right (FR)
- 2 → Center (FC)
- 3 → LFE
- 4 → Back Left (BL)
- 5 → Back Right (BR)
- 6 → Side Left (SL)
- 7 → Side Right (SR)

This ordering matches `WAVEFORMATEXTENSIBLE` 7.1 and is used by:
- Per-channel arrays: `channelGainsDb`, `channelMute`, `channelInvert`, `channelSolo`
- Output remap: `outputChannelMap`
- Copilot action `set_channel_mute` (intValue 0..7)

## Terminology pitfalls
- “Rear speakers” is ambiguous.
  - Some users mean **Back** (BL/BR).
  - Some users mean **Side** (SL/SR).
  - Copilot must ask a clarification question before muting either set.

## Group routing matrix override (Simple presets)
`groupRouting` is a 6×2 boolean matrix (flattened to length 12).

- Rows (in order): Front, Center, LFE, Rear, Side, Reserved
- Cols: A (primary/Game/Music source), B (Secondary source)

If `groupRouting` is present, it overrides `mixingMode` for those groups.
