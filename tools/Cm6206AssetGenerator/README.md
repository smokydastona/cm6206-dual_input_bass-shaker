# Cm6206AssetGenerator

Deterministic asset pack generator for the S‑NDB‑UND pixel‑art hybrid theme.

## Outputs

Generates to (by default):

- `assets/generated/png/(dark|light)/*.png`
- `assets/generated/svg/(dark|light)/*.svg`
- `assets/generated/9slice/(dark|light)/*.9slice.json`

Includes sprite sheets:

- `knob_primary_64_rotate_16f.png`
- `toggle_40x20_slide_4f.png`
- `button_primary_120x36_press_2f.png`
- `meter_v_24x220_decay_8f.png`

## Run

From repo root:

- `./scripts/generate_assets.ps1 -OutDir assets/generated -Theme all`

Or directly:

- `dotnet run -c Release --project tools/Cm6206AssetGenerator -- --out assets/generated --theme all`

## Notes

- PNGs are the canonical pixel-perfect exports.
- SVGs are designed to stay small and use crisp edges/patterns (the background SVG uses a repeating noise pattern).
- Palette constants are aligned with `docs/assets/PIXEL_ASSET_PACK_BLUEPRINT.md`.
