using System.Text;

namespace Cm6206DualRouter;

internal static class CopilotKnowledgeBase
{
    // Keep prompts small: include only the most relevant excerpts.
    private const int MaxAppendixChars = 6000;

    private static readonly string[] DefaultSearchRoots =
    [
        // Shipped EXE / published output (preferred)
        Path.Combine(AppContext.BaseDirectory, "docs", "ai_copilot"),

        // Dev-time convenience (when running from repo root)
        Path.Combine(Environment.CurrentDirectory, "docs", "ai_copilot"),
    ];

    private static readonly (string FileName, string Tag)[] KnownDocs =
    [
        ("01_architecture_cm6206_dual_router.md", "architecture"),
        ("02_ui_map_and_terms.md", "ui"),
        ("03_config_schema_router_json.md", "config"),
        ("04_audio_channel_mapping.md", "channels"),
        ("05_ai_copilot_contracts.md", "contracts"),
        ("06_troubleshooting_playbook.md", "troubleshooting"),
    ];

    public static string BuildAppendix(string userCommand, CopilotContext ctx)
    {
        try
        {
            var command = (userCommand ?? string.Empty).Trim();
            var commandLower = command.ToLowerInvariant();
            var healthLower = (ctx.HealthText ?? string.Empty).ToLowerInvariant();

            var wantTroubleshooting =
                commandLower.Contains("not working") ||
                commandLower.Contains("no audio") ||
                commandLower.Contains("silent") ||
                commandLower.Contains("disconnected") ||
                commandLower.Contains("clipping") ||
                commandLower.Contains("help") ||
                healthLower.Contains("error") ||
                healthLower.Contains("warning");

            var wantConfig =
                commandLower.Contains("router.json") ||
                commandLower.Contains("config") ||
                commandLower.Contains("json") ||
                commandLower.Contains("mixingmode") ||
                commandLower.Contains("grouprouting") ||
                commandLower.Contains("latenc") ||
                commandLower.Contains("samplerate") ||
                commandLower.Contains("exclusive");

            var wantChannels =
                commandLower.Contains("channel") ||
                commandLower.Contains("rear") ||
                commandLower.Contains("side") ||
                commandLower.Contains("back") ||
                commandLower.Contains("mute") ||
                commandLower.Contains("solo") ||
                commandLower.Contains("invert") ||
                commandLower.Contains("map") ||
                commandLower.Contains("remap") ||
                commandLower.Contains("lfe");

            var wantUi =
                commandLower.Contains("where") ||
                commandLower.Contains("what do i click") ||
                commandLower.Contains("tab") ||
                commandLower.Contains("simple") ||
                commandLower.Contains("preset") ||
                commandLower.Contains("status") ||
                commandLower.Contains("health");

            var wantArchitecture =
                commandLower.Contains("how does") ||
                commandLower.Contains("how does it work") ||
                commandLower.Contains("architecture") ||
                commandLower.Contains("pipeline") ||
                commandLower.Contains("wasapi") ||
                commandLower.Contains("naudio");

            var wantContracts =
                commandLower.Contains("ai") ||
                commandLower.Contains("copilot") ||
                commandLower.Contains("what can you do") ||
                commandLower.Contains("actions") ||
                commandLower.Contains("allowed");

            var selected = new List<string>();

            // Selection order matters: put the highest-utility excerpts first.
            if (wantTroubleshooting) selected.Add("06_troubleshooting_playbook.md");
            if (wantUi) selected.Add("02_ui_map_and_terms.md");
            if (wantConfig) selected.Add("03_config_schema_router_json.md");
            if (wantChannels) selected.Add("04_audio_channel_mapping.md");
            if (wantArchitecture) selected.Add("01_architecture_cm6206_dual_router.md");
            if (wantContracts) selected.Add("05_ai_copilot_contracts.md");

            // Ensure at least something for first-run general questions.
            if (selected.Count == 0)
            {
                selected.Add("02_ui_map_and_terms.md");
                selected.Add("06_troubleshooting_playbook.md");
            }

            selected = selected.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var root = FindKnowledgeBaseRoot();
            if (root is null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("LOCAL OFFLINE KNOWLEDGE BASE (repo-specific, authoritative):");
            sb.AppendLine("- Use this before guessing or asking for internet lookups.");
            sb.AppendLine();

            foreach (var file in selected)
            {
                var path = Path.Combine(root, file);
                if (!File.Exists(path))
                    continue;

                var raw = File.ReadAllText(path);
                var cleaned = CleanForPrompt(raw);

                sb.AppendLine($"[KB:{file}]");
                sb.AppendLine(cleaned);
                sb.AppendLine();

                if (sb.Length >= MaxAppendixChars)
                    break;
            }

            if (sb.Length > MaxAppendixChars)
                sb.Length = MaxAppendixChars;

            return sb.ToString().TrimEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? FindKnowledgeBaseRoot()
    {
        foreach (var candidate in DefaultSearchRoots)
        {
            try
            {
                if (Directory.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static string CleanForPrompt(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder(markdown.Length);
        var inCodeFence = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence)
                continue;

            // Drop headings to save space.
            if (trimmed.StartsWith('#'))
                continue;

            // Drop empty lines runs.
            if (trimmed.Length == 0)
            {
                if (sb.Length > 0 && sb[^1] != '\n')
                    sb.AppendLine();
                continue;
            }

            sb.AppendLine(trimmed);
        }

        return sb.ToString().Trim();
    }
}
