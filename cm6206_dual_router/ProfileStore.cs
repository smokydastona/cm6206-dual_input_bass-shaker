using System.Text.Json;

namespace Cm6206DualRouter;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string GetProfilesPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cm6206DualRouter");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "profiles.json");
    }

    public static List<RouterProfile> LoadAll()
    {
        var path = GetProfilesPath();
        if (!File.Exists(path)) return [];

        try
        {
            var json = File.ReadAllText(path);
            var profiles = JsonSerializer.Deserialize<List<RouterProfile>>(json, JsonOptions);
            return profiles ?? [];
        }
        catch
        {
            // If the file is corrupted, don't crash the app; just start fresh.
            return [];
        }
    }

    public static void SaveAll(List<RouterProfile> profiles)
    {
        var path = GetProfilesPath();
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static void Upsert(string name, RouterConfig config)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name must not be empty", nameof(name));

        var profiles = LoadAll();
        var existing = profiles.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            profiles.Add(new RouterProfile { Name = name, Config = config });
        }
        else
        {
            existing.Config = config;
        }

        profiles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        SaveAll(profiles);
    }

    public static bool Delete(string name)
    {
        var profiles = LoadAll();
        var removed = profiles.RemoveAll(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed <= 0) return false;

        SaveAll(profiles);
        return true;
    }
}
