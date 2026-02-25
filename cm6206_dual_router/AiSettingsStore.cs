using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cm6206DualRouter;

internal sealed record AiSettings(
    bool Enabled,
    bool ProactiveHintsEnabled,
    string? EncryptedApiKey,
    string Model,
    bool HasSeenFirstRunPrompt)
{
    public static AiSettings Default => new(
        Enabled: false,
        ProactiveHintsEnabled: false,
        EncryptedApiKey: null,
        Model: "gpt-4o-mini",
        HasSeenFirstRunPrompt: false);
}

internal static class AiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static byte[] Entropy => Encoding.UTF8.GetBytes("Cm6206DualRouter.AiCopilot.v1");

    private static string GetPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cm6206DualRouter");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "ai_settings.json");
    }

    public static AiSettings Load()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
                return AiSettings.Default;

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AiSettings>(json, JsonOptions);
            if (loaded is null)
                return AiSettings.Default;

            if (string.IsNullOrWhiteSpace(loaded.Model))
                loaded = loaded with { Model = AiSettings.Default.Model };

            return loaded;
        }
        catch
        {
            return AiSettings.Default;
        }
    }

    public static void Save(AiSettings settings)
    {
        var path = GetPath();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static string? ProtectApiKey(string? apiKeyPlaintext)
    {
        if (string.IsNullOrWhiteSpace(apiKeyPlaintext))
            return null;

        var plaintextBytes = Encoding.UTF8.GetBytes(apiKeyPlaintext.Trim());
        var protectedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string? UnprotectApiKey(string? apiKeyProtected)
    {
        if (string.IsNullOrWhiteSpace(apiKeyProtected))
            return null;

        try
        {
            var protectedBytes = Convert.FromBase64String(apiKeyProtected);
            var plaintextBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch
        {
            return null;
        }
    }
}
