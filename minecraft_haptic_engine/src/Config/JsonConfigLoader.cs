using System.Text.Json;

namespace MinecraftHapticEngine.Config;

public static class JsonConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static HapticEngineConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config not found: {path}");
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<HapticEngineConfig>(json, Options);
        if (config is null)
        {
            throw new InvalidDataException("Failed to parse config JSON");
        }

        return Normalize(config);
    }

    public static void Save(string path, HapticEngineConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
    }

    private static HapticEngineConfig Normalize(HapticEngineConfig config)
    {
        var buses = config.Buses ?? new Dictionary<string, BusConfig>(StringComparer.OrdinalIgnoreCase);
        var mappings = config.Mappings ?? new List<EffectMapping>();
        var telemetry = config.Telemetry ?? new TelemetryInputConfig();

        if (buses.Comparer != StringComparer.OrdinalIgnoreCase)
        {
            buses = new Dictionary<string, BusConfig>(buses, StringComparer.OrdinalIgnoreCase);
        }

        return config with { Telemetry = telemetry, Buses = buses, Mappings = mappings };
    }
}
