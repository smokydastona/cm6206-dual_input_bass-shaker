using System.Text.Json;

namespace Cm6206DualRouter;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static void EnsureBundledDefaultProfilesInstalled()
    {
        // Profiles are stored under %AppData%\Cm6206DualRouter\profiles.
        // For first-run UX, we ship some defaults with the app and copy them in once.
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var bundledDir = Path.Combine(baseDir, "default_profiles");
            if (!Directory.Exists(bundledDir))
                return;

            var bundledFiles = Directory.EnumerateFiles(bundledDir, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in bundledFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<RouterProfile>(json, JsonOptions);
                    if (profile is null || string.IsNullOrWhiteSpace(profile.Name))
                        continue;

                    var targetPath = GetProfilePathForName(profile.Name);
                    if (File.Exists(targetPath))
                        continue;

                    SaveProfile(profile);
                }
                catch
                {
                    // ignore invalid bundled profiles
                }
            }
        }
        catch
        {
            // ignore; profiles are an optional UX feature
        }
    }

    public static string GetProfilesDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cm6206DualRouter",
            "profiles");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetLegacyProfilesPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cm6206DualRouter");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "profiles.json");
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        if (safe.Length == 0) safe = "Profile";
        return safe;
    }

    private static string GetProfilePathForName(string name)
    {
        var dir = GetProfilesDirectory();
        var file = MakeSafeFileName(name) + ".json";
        return Path.Combine(dir, file);
    }

    private static void TryMigrateLegacyProfilesFile()
    {
        var legacyPath = GetLegacyProfilesPath();
        if (!File.Exists(legacyPath)) return;

        try
        {
            var json = File.ReadAllText(legacyPath);
            var profiles = JsonSerializer.Deserialize<List<RouterProfile>>(json, JsonOptions);
            if (profiles is null || profiles.Count == 0) return;

            foreach (var p in profiles)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                    continue;
                SaveProfile(p);
            }

            // Keep a backup so nothing is lost.
            var backup = legacyPath + ".bak";
            if (!File.Exists(backup))
                File.Move(legacyPath, backup);
        }
        catch
        {
            // Ignore migration failures; old profiles still exist.
        }
    }

    public static List<RouterProfile> LoadAll()
    {
        EnsureBundledDefaultProfilesInstalled();
        TryMigrateLegacyProfilesFile();

        var dir = GetProfilesDirectory();
        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<RouterProfile>();
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<RouterProfile>(json, JsonOptions);
                if (profile is null) continue;
                profile.SourcePath = file;
                if (string.IsNullOrWhiteSpace(profile.Name))
                    profile.Name = Path.GetFileNameWithoutExtension(file);
                results.Add(profile);
            }
            catch
            {
                // skip invalid profile files
            }
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    public static void SaveProfile(RouterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new ArgumentException("Profile name must not be empty", nameof(profile));

        var path = GetProfilePathForName(profile.Name);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static void Upsert(string name, RouterConfig config, IEnumerable<string>? processNames)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name must not be empty", nameof(name));

        var pn = processNames?
            .Select(s => (s ?? string.Empty).Trim())
            .Where(s => s.Length > 0)
            .Select(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s : (s + ".exe"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var profile = new RouterProfile
        {
            Name = name.Trim(),
            Config = config,
            ProcessNames = pn is { Length: > 0 } ? pn : null
        };

        SaveProfile(profile);
    }

    public static bool Delete(string name)
    {
        var dir = GetProfilesDirectory();
        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(file), name, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
                return true;
            }

            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<RouterProfile>(json, JsonOptions);
                if (profile is not null && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                    return true;
                }
            }
            catch
            {
                // ignore
            }
        }

        return false;
    }
}
