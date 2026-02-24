using System.Text.Json.Serialization;

namespace Cm6206DualRouter;

public sealed class RouterProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("config")]
    public RouterConfig Config { get; set; } = new();
}
