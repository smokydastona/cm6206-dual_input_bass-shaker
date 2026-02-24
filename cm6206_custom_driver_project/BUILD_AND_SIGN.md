# Build + normal signing (Windows 11)

This is the minimum you need to produce something you can install on Win11 **without test mode**.

## Tooling
- Visual Studio (Community is fine)
- Windows Driver Kit (WDK) matching your Windows SDK

## Source baseline (do NOT copy from the web into this repo)
Microsoft publishes official driver samples (e.g., SysVAD) that demonstrate audio endpoint drivers.
The usual workflow is:
1) Install WDK
2) Get the official samples from Microsoft
3) Modify them locally

I’m not pasting sample code here because it’s large and is best pulled from the official source.

## Signing pipeline (normal, Win11)
You typically need:
- An **EV code signing certificate**
- A Microsoft Hardware Dev Center account
- Build your driver package (`.inf`, `.sys`, generate `.cat`)
- Submit for **attestation signing**
- Install the returned signed package

## High-level checklist
1) Build the virtual audio driver exposing two render endpoints.
2) Package: INF references the driver binaries.
3) Create catalog: `inf2cat` (done during driver build tooling)
4) Submit for attestation signing.
5) Install via `pnputil /add-driver <inf> /install`.

## Router service
Even with two endpoints, you need a router:
- Capture from `Shaker Input` endpoint
- Render to the CM6206 endpoint (WASAPI)
- Apply shaker EQ/filters

This service is a normal signed user-mode app (much easier).

## If you refuse any user-mode routing
Then “two devices” cannot reach the same physical CM6206 hardware in a controllable way.
That requirement is logically in conflict with how Windows’ audio stack works.
