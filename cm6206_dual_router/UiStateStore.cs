using System.Text.Json;

namespace Cm6206DualRouter;

internal sealed record UiState(
    bool ShowAdvancedControls,
    bool HasSeenSimpleMode);

internal static class UiStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetUiStatePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cm6206DualRouter");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "ui_state.json");
    }

    public static UiState Load()
    {
        try
        {
            var path = GetUiStatePath();
            if (!File.Exists(path))
                return new UiState(ShowAdvancedControls: false, HasSeenSimpleMode: false);

            var json = File.ReadAllText(path);
            var s = JsonSerializer.Deserialize<UiState>(json, JsonOptions);
            return s ?? new UiState(ShowAdvancedControls: false, HasSeenSimpleMode: false);
        }
        catch
        {
            return new UiState(ShowAdvancedControls: false, HasSeenSimpleMode: false);
        }
    }

    public static void Save(UiState state)
    {
        try
        {
            var path = GetUiStatePath();
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // non-critical
        }
    }
}
