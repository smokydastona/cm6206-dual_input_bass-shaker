using System.Text.Json.Serialization;

namespace MinecraftHapticEngine.Config;

public sealed record HapticEngineConfig(
    TelemetryInputConfig Telemetry,
    Dictionary<string, BusConfig> Buses,
    List<EffectMapping> Mappings)
{
    public static HapticEngineConfig CreateDefault() => new(
        Telemetry: new TelemetryInputConfig(),
        Buses: new Dictionary<string, BusConfig>(StringComparer.OrdinalIgnoreCase),
        Mappings: new List<EffectMapping>());
}

public sealed record TelemetryInputConfig(
    string WebSocketUrl = "ws://127.0.0.1:7117/",
    bool EnableWebSocket = true,
    bool EnableUdp = false,
    int UdpPort = 7117,
    int ReconnectDelayMs = 1000,
    int ReceiveQueueLimit = 2048);

public sealed record BusConfig(
    string RenderDeviceName,
    int Channels = 2,
    int SampleRate = 48000,
    int DesiredLatencyMs = 50,
    bool ExclusiveMode = false,
    int BufferSizeFrames = 512,
    float GainDb = 0,
    float? LowPassHz = null,
    float? HighPassHz = null,
    float DelayMs = 0);

public sealed record EffectMapping(
    string Name,
    string Bus,
    MatchRule Match,
    string Mode = "oneshot",
    float GainDb = 0,
    RouteConfig? Route = null,
    GeneratorConfig? Generator = null,
    EnvelopeConfig? Envelope = null,
    FilterConfig? Filter = null,
    ContinuousConfig? Continuous = null);

public sealed record MatchRule(
    string? Type = null,
    string? Id = null,
    string? Kind = null);

public sealed record RouteConfig(
    string? Preset = null,
    float[]? Weights = null);

public sealed record GeneratorConfig(
    string Type,
    float FrequencyHz = 60,
    float FrequencyHzTo = 120,
    float DurationMs = 120,
    float NoiseColor = 0);

public sealed record EnvelopeConfig(
    float AttackMs = 0,
    float HoldMs = 0,
    float ReleaseMs = 80);

public sealed record FilterConfig(
    float? LowPassHz = null,
    float? HighPassHz = null);

public sealed record ContinuousConfig(
    TelemetryValueMap? AmplitudeFrom = null,
    TelemetryValueMap? FrequencyFrom = null,
    float UpdateSmoothingMs = 50);

public sealed record TelemetryValueMap(
    string Field,
    float Min = 0,
    float Max = 1,
    string Curve = "linear",
    float Scale = 1,
    float Offset = 0);
