# Design: Two playback devices (Music + Shaker)

## Terminology
- **Playback device** in Windows Sound UI = an **audio render endpoint**.
- The CM6206 hardware provides one physical render endpoint (plus other pins).
- You want two separate selectable endpoints so apps can target them independently.

## Design options

### Option 1 (practical): Virtual endpoints + router (recommended)
- Create a signed **virtual audio device driver** that exposes two render endpoints:
  - `Shaker Input` (endpoint 1)
  - `Music Input` (endpoint 2)
- A small router service receives audio from those endpoints and plays it to:
  - Your normal listening device (from `Music Input`), and
  - The CM6206 device (from `Shaker Input`)

Pros:
- Does not touch the CM6206 physical driver (you keep its stability).
- You can implement shaker-only processing (LPF, compressor, limiter) in user-mode safely.
- Endpoint count and naming is fully under your control.

Cons:
- It’s still “software routing” under the hood (even if it’s your own).

### Option 2: Modify/replace the CM6206 driver to expose extra endpoints
- Requires deep driver work and re-signing.
- Very high risk: stability, latency, Windows Update compatibility.

Conclusion: Option 1 is the only realistic route to “two devices” without reimplementing USB audio.

## What “does everything original does” means in practice
- The original driver is a USB Audio Class driver with vendor effects/control panel.
- Reproducing *all* vendor features is not realistic.

But for your actual goal (two independent inputs):
- You mainly need **two endpoints** + **reliable routing** + **bass-only processing**.

## Shaker processing requirements (user-mode)
- High-pass ~20 Hz (protect transducers)
- Low-pass ~60–90 Hz
- Optional: compressor/limiter
- Optional: per-channel gains + channel duplication (to drive FL/FR/RL/RR/SL/SR/LFE)

This is straightforward in a router app; much harder in a kernel driver.
