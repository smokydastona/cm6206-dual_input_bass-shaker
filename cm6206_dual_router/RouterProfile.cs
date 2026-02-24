using System.Text.Json.Serialization;

namespace Cm6206DualRouter;

public sealed class RouterProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    // Optional: if any of these processes are running, this profile can be auto-selected.
    // Entries should be EXE names like "game.exe" or "vlc.exe".
    [JsonPropertyName("processNames")]
    public string[]? ProcessNames { get; set; } = null;

    [JsonPropertyName("config")]
    public RouterConfig Config { get; set; } = new();

    [JsonIgnore]
    public string? SourcePath { get; set; } = null;
}
