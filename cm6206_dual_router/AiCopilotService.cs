using System.Text;
using System.Text.Json;

namespace Cm6206DualRouter;

internal sealed class AiCopilotService
{
    private readonly OpenAiClient _client;

    public AiCopilotService(OpenAiClient client)
    {
        _client = client;
    }

    private sealed record ModelResponse(
        string? assistantText,
        string? clarificationQuestion,
        string[]? clarificationOptions,
        CopilotAction[]? proposedActions);

    public async Task<CopilotResponse> HandleCommandAsync(
        string apiKey,
        string model,
        CopilotContext ctx,
        string userCommand,
        CancellationToken cancellationToken)
    {
        var system = BuildSystemPrompt();
        var user = BuildUserPrompt(ctx, userCommand);
        var json = await _client.CreateJsonAsync(apiKey, model, system, user, cancellationToken).ConfigureAwait(false);

        ModelResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ModelResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            parsed = null;
        }

        var text = (parsed?.assistantText ?? "I could not parse the AI response.").Trim();
        text = text.Length > 700 ? text[..700] : text;

        var clarQ = parsed?.clarificationQuestion;
        var clarOpts = parsed?.clarificationOptions ?? Array.Empty<string>();
        CopilotClarification? clarification = null;
        if (!string.IsNullOrWhiteSpace(clarQ) && clarOpts.Length > 0)
            clarification = new CopilotClarification(clarQ.Trim(), clarOpts.Where(o => !string.IsNullOrWhiteSpace(o)).Take(5).ToArray());

        var actions = ValidateActions(parsed?.proposedActions);

        return new CopilotResponse(
            AssistantText: text,
            Clarification: clarification,
            ProposedActions: actions);
    }

    private static CopilotAction[] ValidateActions(CopilotAction[]? actions)
    {
        if (actions is null || actions.Length == 0)
            return Array.Empty<CopilotAction>();

        // Hard allowlist to prevent the model from inventing actions.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "set_game_source",
            "set_secondary_source",
            "set_output_device",
            "apply_simple_preset",
            "set_shaker_strength_db",
            "set_master_gain_db",
            "set_shaker_mode",
            "show_advanced_controls",
            "refresh_devices",
            "start_routing",
            "stop_routing",
            "set_channel_mute"
        };

        var result = new List<CopilotAction>();
        foreach (var a in actions)
        {
            if (a is null) continue;
            if (string.IsNullOrWhiteSpace(a.Type)) continue;
            if (!allowed.Contains(a.Type)) continue;
            result.Add(a);
        }
        return result.ToArray();
    }

    private static string BuildSystemPrompt()
    {
        return
            "You are a guided setup assistant for a Windows multichannel audio router (CM6206 Dual Router).\n" +
            "Strict scope:\n" +
            "- You may explain what the user is seeing, ask clarifying setup questions, recommend presets, detect misconfiguration, and suggest existing UI actions.\n" +
            "- You may NOT invent features, invent DSP chains, change low-level routing silently, or give freeform audio-engineering advice.\n" +
            "- Never use emojis. Be calm and technical.\n\n" +
            "If the user prompt includes a 'LOCAL OFFLINE KNOWLEDGE BASE' appendix, treat it as authoritative for app-specific facts.\n\n" +
            "Channel mapping (for set_channel_mute intValue 0-7):\n" +
            "0=FL, 1=FR, 2=FC, 3=LFE, 4=BL, 5=BR, 6=SL, 7=SR.\n" +
            "Terminology note: 'rear speakers' is ambiguous (Back BL/BR vs Side SL/SR).\n\n" +
            "Output format: JSON object only. Do not include markdown.\n" +
            "Keys:\n" +
            "- assistantText: string\n" +
            "- clarificationQuestion: string or null\n" +
            "- clarificationOptions: array of strings (0-5)\n" +
            "- proposedActions: array of actions (may be empty)\n\n" +
            "Action schema:\n" +
            "{ \"type\": string, \"stringValue\": string|null, \"floatValue\": number|null, \"intValue\": number|null, \"boolValue\": boolean|null }\n\n" +
            "Allowed action types:\n" +
            "- set_game_source (stringValue = render device name)\n" +
            "- set_secondary_source (stringValue = render device name or '(None)')\n" +
            "- set_output_device (stringValue = render device name)\n" +
            "- apply_simple_preset (stringValue = one of: 'Game + Bass Shaker', 'Music Clean', 'Game Only', 'Shaker Only', 'Flat / Debug')\n" +
            "- set_shaker_strength_db (floatValue, range -24..+12)\n" +
            "- set_master_gain_db (floatValue, range -60..+20)\n" +
            "- set_shaker_mode (stringValue = 'always' or 'gamesOnly')\n" +
            "- show_advanced_controls (boolValue)\n" +
            "- refresh_devices\n" +
            "- start_routing\n" +
            "- stop_routing\n" +
            "- set_channel_mute (intValue = channel index 0-7, boolValue = mute)\n\n" +
            "Rules:\n" +
            "- If the request is ambiguous, ask ONE clarificationQuestion with 2-3 clarificationOptions and proposeActions must be empty.\n" +
            "- If the user says 'rear speakers'/'rear channels', ask whether they mean Back (BL/BR) or Side (SL/SR). Use clarificationOptions: ['Back (BL/BR)', 'Side (SL/SR)', 'Both']. proposeActions must be empty.\n" +
            "- Otherwise proposeActions should be the smallest set of changes needed.\n";
    }

    private static string BuildUserPrompt(CopilotContext ctx, string userCommand)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Current app state:");
        sb.AppendLine($"- activeTab: {ctx.ActiveTab}");
        sb.AppendLine($"- routerRunning: {ctx.RouterRunning}");
        sb.AppendLine($"- gameSource: {ctx.GameSource}");
        sb.AppendLine($"- secondarySource: {ctx.SecondarySource}");
        sb.AppendLine($"- outputDevice: {ctx.OutputDevice}");
        sb.AppendLine($"- outputOk: {ctx.OutputOk}");
        sb.AppendLine($"- speakersEnabled: {ctx.SpeakersEnabled}");
        sb.AppendLine($"- shakerEnabled: {ctx.ShakerEnabled}");
        sb.AppendLine($"- masterGainDb: {ctx.MasterGainDb:0.0}");
        sb.AppendLine($"- shakerStrengthDb: {ctx.ShakerStrengthDb:0.0}");
        sb.AppendLine($"- gamePeak: {ctx.GamePeak:0.0000}");
        sb.AppendLine($"- secondaryPeak: {ctx.SecondaryPeak:0.0000}");
        sb.AppendLine($"- outputLfePeak: {ctx.OutputLfePeak:0.0000}");
        sb.AppendLine($"- health: {ctx.HealthText}");
        sb.AppendLine();

        var kb = CopilotKnowledgeBase.BuildAppendix(userCommand, ctx);
        if (!string.IsNullOrWhiteSpace(kb))
        {
            sb.AppendLine(kb);
            sb.AppendLine();
        }

        sb.AppendLine("User request:");
        sb.AppendLine(userCommand);
        sb.AppendLine();
        sb.AppendLine("Respond with JSON only.");
        return sb.ToString();
    }
}
