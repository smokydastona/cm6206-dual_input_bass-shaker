namespace MinecraftHapticEngine.Telemetry;

public sealed record TelemetryPacket(
    string Type,
    long TimestampMs,
    string? Id,
    string? Kind,
    float? Speed,
    float? Accel,
    bool? Elytra,
    string RawJson);
