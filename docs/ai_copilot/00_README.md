# AI Copilot Offline Knowledge Base (Better Sound)

This folder is a **local knowledge base** for the repo’s built-in “Setup Assistant / AI Copilot”.

Goal:
- Let the copilot answer user questions **using repo-specific facts first**.
- Only consult the internet for **vendor/API details** that are not present locally.

Scope rules (must not be violated):
- The copilot may explain UI, ask setup questions, recommend presets, detect misconfiguration from **state**, and suggest existing UI actions.
- The copilot may **not** invent new DSP chains, claim to “hear” audio content, silently change routing, or give freeform audio engineering advice.
- If a request is ambiguous, ask **one** clarification question and propose **no actions**.

## What’s in here
- [01_architecture_cm6206_dual_router.md](01_architecture_cm6206_dual_router.md): Router app architecture + data flow.
- [02_ui_map_and_terms.md](02_ui_map_and_terms.md): Tabs, Simple Mode vocabulary, control intent.
- [03_config_schema_router_json.md](03_config_schema_router_json.md): `router.json` schema (from `RouterConfig`).
- [04_audio_channel_mapping.md](04_audio_channel_mapping.md): 7.1 channel order + group routing matrix.
- [05_ai_copilot_contracts.md](05_ai_copilot_contracts.md): Context fields + action allowlist + JSON response contract.
- [06_troubleshooting_playbook.md](06_troubleshooting_playbook.md): “Why isn’t this working?” checks and fixes.
- [07_minecraft_haptic_engine_overview.md](07_minecraft_haptic_engine_overview.md): Second app in repo.

## Maintenance guidance
Update these docs when:
- You add/remove copilot action types.
- You change `CopilotContext` fields or meanings.
- You change Simple presets or channel mapping.
- You change config schema or validation rules.

Authoritative sources in code:
- `cm6206_dual_router/RouterConfig.cs`
- `cm6206_dual_router/CopilotContracts.cs`
- `cm6206_dual_router/AiCopilotService.cs`
- `cm6206_dual_router/RouterMainForm.cs`
- `cm6206_dual_router/SetupAssistantPanel.cs`
