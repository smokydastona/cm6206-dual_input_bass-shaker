using System.Text.Json;

namespace MinecraftHapticEngine.Telemetry;

public static class TelemetryParser
{
    public static bool TryParse(string json, out TelemetryPacket packet)
    {
        packet = new TelemetryPacket(Type: "unknown", TimestampMs: 0, Id: null, Kind: null, Speed: null, Accel: null, Elytra: null, RawJson: json);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(type))
            {
                return false;
            }

            var t = root.TryGetProperty("t", out var tEl) && tEl.TryGetInt64(out var tVal) ? tVal : 0;

            string? id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            string? kind = root.TryGetProperty("kind", out var kindEl) ? kindEl.GetString() : null;

            float? speed = null;
            if (root.TryGetProperty("speed", out var speedEl) && speedEl.TryGetSingle(out var s)) speed = s;

            float? accel = null;
            if (root.TryGetProperty("accel", out var accelEl) && accelEl.TryGetSingle(out var a)) accel = a;

            bool? elytra = null;
            if (root.TryGetProperty("elytra", out var elytraEl) && elytraEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                elytra = elytraEl.GetBoolean();
            }

            packet = new TelemetryPacket(type!, t, id, kind, speed, accel, elytra, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
