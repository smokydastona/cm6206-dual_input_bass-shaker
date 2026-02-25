# High‑Res Pixel‑Art Hybrid Asset Pack — S‑NDB‑UND (Graphite + Cyan)

This document turns the “literal” asset spec into a *complete, fill‑in‑the‑blanks, design‑ready* blueprint: exact colors (hex), pixel patterns, construction steps, 9‑slice margins, sprite‑sheet layouts, and light‑theme recolors.

Source of truth for palette: `cm6206_dual_router/NeonTheme.cs`.

---

## 0) Global Rules

- **Pixel grid:** 1 px.
- **Crisp rendering:** no anti‑aliasing. If exporting SVG, use `shape-rendering="crispEdges"` and snap all coordinates to integers.
- **Hybrid look:** pixel‑art edges + dither shading, but with *clean geometry* (circles/arcs) that still reads “engineered”.
- **Alpha convention:** when spec says “X% opacity cyan”, use the *preblended* hex listed below for PNG pixel placement (so the art is deterministic across export tools).
- **Disabled state:** desaturate to grayscale + apply 40% opacity over the background. (For PNG: multiply alpha by 0.4; for SVG: group opacity 0.4 + desaturate filter.)

---

## 1) Palette (Dark Graphite Theme)

### 1.1 Base palette (from NeonTheme)

- **Graphite0 / Background:** `#0A0C10` (BgPrimary)
- **Graphite1 / Panel:** `#11141A` (BgPanel)
- **Graphite2 / Raised:** `#1A1E26` (BgRaised)
- **TextSecondary / UI gray:** `#A9B4C6`
- **White:** `#FFFFFF`
- **CyanAccent:** `#00F6FF`
- **MeterMid (violet):** `#7A5CFF`
- **MeterHigh (amber):** `#FFB000`
- **ClipRed:** `#FF3B3B`

### 1.2 Derived UI metals (exact hex)

Computed blends used for pixel bevels and borders:

- **BorderOuter:** `#3E444E`  (25% mix Graphite2 → TextSecondary)
- **BorderInner:** `#111419`  (35% mix Graphite2 → Black)
- **HighlightTop:** `#43464D` (18% mix Graphite2 → White)
- **ShadowBottom:** `#0C0E11` (55% mix Graphite2 → Black)
- **TrackBase:** `#15181F`    (45% mix Graphite1 → Graphite2)

Pressed/hover graphite variants:

- **Graphite2−10% (pressed base):** `#171B22`
- **Graphite2+10%:** `#31343C`
- **Graphite2+20%:** `#484B51`

Cyan brightening for hover arcs:

- **CyanBright20:** `#33F8FF`
- **CyanBright35:** `#59F9FF`

### 1.3 Preblended cyan “opacity” pixels (deterministic)

Use these **as actual pixel colors** for glow dithers:

Against **Graphite0** (`#0A0C10`):
- **Cyan 30% on Bg:** `#075258`
- **Cyan 20% on Bg:** `#083B40`

Against **Graphite1** (`#11141A`):
- **Cyan 30% on Panel:** `#0C585F`
- **Cyan 20% on Panel:** `#0E4148`

---

## 2) Palette (Light Theme Variant)

Light theme is a recolor pass (no geometry changes). Cyan stays identical.

- **Stone0 / Background:** `#ECECED` ("StonePrimary")
- **Stone1 / Panel:** `#DEDEDF` ("StonePanel")
- **Stone2 / Raised:** `#D6D6D8` ("StoneRaised")
- **StoneTextSecondary:** `#3B3F45`
- **White:** `#FFFFFF`
- **CyanAccent:** `#00F6FF`

Rule mapping:
- Graphite0 → Stone0
- Graphite1 → Stone1
- Graphite2 → Stone2
- BorderOuter/Inner/Highlight/Shadow: recompute with same mixing % (recommended), or hand‑tune to keep contrast.

---

## 3) Pixel Patterns

### 3.1 50% checkerboard (classic dither)

Alternating pixels:

```
A B A B
B A B A
A B A B
B A B A
```

### 3.2 2×2 Bayer matrix (ordered dither)

Use the canonical 2×2 Bayer threshold map:

```
0 2
3 1
```

Application rule for a gradient field:
- Normalize gradient value `g` to 0..1.
- For each pixel, compute threshold `t` from the matrix (0..3) as `(t+0.5)/4`.
- If `g >= threshold` place the “lighter” color, else place the “base” color.

### 3.3 25% noise dither

Place the lighter pixel in **1 out of every 4** pixels. Use a fixed tileable 4×4 mask (repeatable) to keep exports deterministic.

Recommended 4×4 mask (ones = place lighter):

```
1 0 0 0
0 0 1 0
0 1 0 0
0 0 0 1
```

---

## 4) CONTROL ASSETS

### 4.1 Primary Knob (64×64)

**Canvas**
- Size: 64×64
- Center: (32,32)

**Geometry**
- Base circle diameter: 56 px
- Radius: 28 px

**Layers (bottom → top)**

1) **Base circle fill**
- Fill: `Graphite2` `#1A1E26`

2) **Border ring (2 px)**
- Outer ring: `BorderOuter` `#3E444E`
- Inner ring: `BorderInner` `#111419`

3) **Dithered shading (top‑left → bottom‑right)**
- Use 2×2 Bayer.
- Two‑tone set:
  - Base pixels: `Graphite2` `#1A1E26`
  - Light pixels: `HighlightTop` `#43464D`
- Gradient: strongest light at ~315° (top‑left), fades toward ~135° (bottom‑right).
- Optional deeper shadow band near bottom‑right:
  - Dark pixels: `ShadowBottom` `#0C0E11` (use 25% noise mask, not 50%, so it stays subtle)

4) **Accent arc (value indicator)**
- Thickness: 4 px
- Radius: 28 px
- Start: 135°
- End: 45°
- Color (default): `CyanAccent` `#00F6FF`
- Glow: 1 px outer dither using `Cyan 30% on Bg` `#075258` (sparse 25% noise mask)

5) **Knob indicator**
- Rectangle: 6×2 px
- Color: `White` `#FFFFFF`
- Position: rotates around circle along radius ~22 px.

**States**
- Default: as above
- Hover:
  - Arc color: `CyanBright20` `#33F8FF`
  - Add **2 px cyan glow ring** (outside base circle), dithered:
    - Inner glow pixels (50% checkerboard): `Cyan 30% on Bg` `#075258`
    - Outer glow pixels (20% density via 25% noise mask): `Cyan 20% on Bg` `#083B40`
- Active (pressed): shift entire knob art down by **1 px**
  - Base fill becomes `Graphite2−10%` `#171B22`
- Disabled:
  - Desaturate + 40% opacity


### 4.2 Secondary Knob (48×48)

Same construction, scaled:
- Canvas: 48×48; center: (24,24)
- Base diameter: 44 px (radius 22)
- Border: 1 px ring (Outer=`BorderOuter`, Inner=`BorderInner`)
- Arc thickness: 3 px
- Indicator: 4×2 px (`#FFFFFF`)


### 4.3 Slider (Track 260×4 + Thumb 14×14)

**Track**
- Size: 260×4
- Base: `TrackBase` `#15181F`
- Active segment: `CyanAccent` `#00F6FF`
- Bevel:
  - Top edge 1 px highlight: `HighlightTop` `#43464D`
  - Bottom edge 1 px shadow: `ShadowBottom` `#0C0E11`

**Thumb**
- Size: 14×14
- Shape: diamond (rotated square); pixel edges stepped.
- Border: 1 px `#FFFFFF`
- Fill: `Graphite1` `#11141A`
- Hover glow: 1 px cyan dither ring using `Cyan 30% on Panel` `#0C585F` (50% checkerboard)


### 4.4 Toggle Switch (40×20)

**Track**
- Size: 40×20
- Radius: 10 px (pixel‑stepped corner approximation)
- Off color: `TrackBase` `#15181F`
- On color: `Cyan 20% on Panel` `#0E4148` + 1 px inner highlight `#075258` sparsely
- Bevel:
  - Top 1 px: `HighlightTop` `#43464D`
  - Bottom 1 px: `ShadowBottom` `#0C0E11`

**Thumb**
- Size: 18×18
- Circle
- Border: 1 px `#FFFFFF`
- Fill: `Graphite2` `#1A1E26`

**Animation frames (left→right)**
- 4 frames, thumb x‑offset increments: **+2, +2, +1, +1 px**
- Export as `toggle_40x20_slide_4f.png` sprite strip: 160×20.


### 4.5 Buttons

**Primary button** (min 120×36)
- Background: `Graphite2` `#1A1E26`
- Border: 1 px `BorderOuter` `#3E444E`
- Bevel:
  - Top: `HighlightTop` `#43464D`
  - Bottom: `ShadowBottom` `#0C0E11`
- Hover: add 1 px cyan edge dither (`#0C585F`) on top/left edges.

**Secondary button**
- Background: `Graphite1` `#11141A`
- Border: 1 px `BorderOuter` `#3E444E`

**Icon button** (32×32)
- Background: `Graphite1` `#11141A`
- Border: 1 px `BorderOuter` `#3E444E`
- Hover: cyan glow ring (1 px) using `#0C585F` (50% checkerboard)
- Icon content box: 18×18 centered

---

## 5) METERS

### 5.1 Large Vertical Meter (24×220)

- Background: `Graphite1` `#11141A`
- Border: 1 px `BorderOuter` `#3E444E`
- Tick marks: every 6 dB
  - Color: `TextSecondary` `#A9B4C6` at 60% density (use 25% noise mask to avoid busy look)
- Fill gradient (pixel‑stepped, bottom→top):
  - Bottom: `MeterLow` `#00F6FF`
  - Middle: `MeterMid` `#7A5CFF`
  - Top: `MeterHigh` `#FFB000`
  - Clip overlay: `ClipRed` `#FF3B3B`

**Peak hold bar**
- Size: 24×2
- Color: `CyanBright20` `#33F8FF`


### 5.2 Mini Meter (12×120)

Same structure, scaled down.


### 5.3 GR Meter (80×12)

- Background: `Graphite1` `#11141A`
- Border: 1 px `BorderOuter` `#3E444E`
- Gain reduction bar: pixel‑stepped red gradient
  - Start (low GR): mix of `Graphite1` → `ClipRed`
  - End (high GR): `ClipRed` `#FF3B3B`
- Tick marks: every 4 px
  - Color: `TextSecondary` `#A9B4C6` (sparse 25% noise)

---

## 6) CARDS & PANELS

### 6.1 Node Card (140×60)

- Canvas: 140×60
- Radius: 8 px (pixel‑stepped corners)
- Fill: dithered graphite (50% checkerboard)
  - A: `Graphite1` `#11141A`
  - B: `Graphite2` `#1A1E26`
- Border: 2 px
  - Outer: `BorderOuter` `#3E444E`
  - Inner: `BorderInner` `#111419`
- Hover glow: 1 px cyan dither around outer border
  - Use `Cyan 30% on Panel` `#0C585F` with 50% checkerboard


### 6.2 DSP Section Card (323×448)

Same style as Node Card:
- Radius: 8 px
- Fill: Graphite1/Graphite2 checkerboard
- Border: 2 px (Outer/Inner)
- Hover glow: 1 px cyan dither

---

## 7) BACKGROUND (1920×1080)

Layer 1 — Base graphite (25% noise)
- Dark pixels: `Graphite0` `#0A0C10`
- Light pixels: `Graphite1` `#11141A`
- Apply 25% noise mask (tileable).

Layer 2 — Vignette
- Radial darkening from edges to center at **10% strength**.
- For deterministic pixel art, apply in 8–12 stepped rings (not smooth).

Layer 3 — Cyan accent strips
- 1 px horizontal lines at **y = 200** and **y = 600**
- Color: `Cyan 20% on Bg` `#083B40`

---

## 8) ICON SET (24×24)

Common style:
- 1 px outline: `TextSecondary` `#A9B4C6`
- Fill: `Graphite2` `#1A1E26`
- Highlight pixels: `CyanAccent` `#00F6FF`
- Shading: 50% checkerboard with `Graphite2` + `HighlightTop`

Icons to include:
- Settings (gear)
- Device (USB chip)
- Input A, Input B
- Filters
- Limiter
- Output
- Warning, Error, Success
- Presets: Gaming, Movies, Music, Custom

---

## 9) 9‑SLICE GUIDES

Because different tools interpret 9‑slice differently, export **both**:

1) The **plain PNG** (no guides) and
2) A **JSON slice descriptor** with pixel margins.

### 9.1 JSON format

Create `*.9slice.json`:

```json
{
  "left": 12,
  "top": 12,
  "right": 12,
  "bottom": 12
}
```

### 9.2 Recommended slice margins

- Node Card (140×60): left=12, top=12, right=12, bottom=12
- DSP Card (323×448): left=16, top=16, right=16, bottom=16
- Primary Button (min 120×36): left=12, top=12, right=12, bottom=12

Rationale: preserves 8 px corners + 2 px border + 2 px breathing room.

---

## 10) ANIMATION SPRITESHEETS

All animations export as **horizontal strips** (frame 0..N‑1 left→right).

- Knob rotation (Primary knob): 16 frames × (64×64) ⇒ **1024×64**
  - `knob_primary_64_rotate_16f.png`
- Toggle slide: 4 frames × (40×20) ⇒ **160×20**
  - `toggle_40x20_slide_4f.png`
- Button press: 2 frames × (120×36) ⇒ **240×36**
  - `button_primary_120x36_press_2f.png`
- Meter decay: 8 frames × (24×220) ⇒ **192×220**
  - `meter_v_24x220_decay_8f.png`

Preset morph (6 frames): treat as **UI value animation reference** (knob/slider values), not a bitmap requirement unless you want marketing GIFs.

---

## 11) Export Targets & Naming

Recommended export tree:

- `assets/exports/png/dark/…`
- `assets/exports/png/light/…`
- `assets/exports/svg/dark/…`
- `assets/exports/svg/light/…`
- `assets/exports/9slice/…` (PNG + JSON)

Naming template:

`<asset>_<w>x<h>_<state>.png`

Examples:
- `knob_primary_64_default.png`
- `knob_primary_64_hover.png`
- `node_card_140x60_default.png`
- `node_card_140x60_default.9slice.json`

---

## 12) Figma Component Library Structure

One component set per asset type:

- `Knob/Primary (64)`
- `Knob/Secondary (48)`
- `Slider`
- `Toggle`
- `Button/Primary`, `Button/Secondary`, `Button/Icon`
- `Meter/Large`, `Meter/Mini`, `Meter/GR`
- `Card/Node`, `Card/DSP`
- `Icons/*`

Variants:
- Theme: `Dark`, `Light`
- State: `Default`, `Hover`, `Active`, `Disabled`

---

## 13) Notes for Implementation in This Repo

The current app UI is drawn by custom WinForms controls (no bitmap assets yet). This blueprint is designed so you can:

- **Generate bitmaps** for use in WinForms (draw on `OnPaint`) OR
- Keep vector templates (SVG) for marketing/docs while rendering runtime UI procedurally.

If you want, I can add a small deterministic “asset generator” tool in this repo that outputs the PNG/SVG/spritesheets exactly per this blueprint.
