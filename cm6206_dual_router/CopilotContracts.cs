namespace Cm6206DualRouter;

internal sealed record CopilotContext(
    string ActiveTab,
    bool RouterRunning,
    string? GameSource,
    string? SecondarySource,
    string? OutputDevice,
    bool OutputOk,
    bool SpeakersEnabled,
    bool ShakerEnabled,
    float MasterGainDb,
    float ShakerStrengthDb,
    float GamePeak,
    float SecondaryPeak,
    float OutputLfePeak,
    float OutputPeakMax,
    string HealthText);

internal sealed record CopilotClarification(string Question, string[] Options);

internal sealed record CopilotAction(
    string Type,
    string? StringValue = null,
    float? FloatValue = null,
    int? IntValue = null,
    bool? BoolValue = null);

internal sealed record CopilotResponse(
    string AssistantText,
    CopilotClarification? Clarification,
    CopilotAction[] ProposedActions);
