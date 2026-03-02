using System.Net.Http.Headers;
using System.Text.Json;

namespace Cm6206DualRouter;

internal sealed record UpdateInfo(
    Version LatestVersion,
    string TagName,
    Uri HtmlUrl,
    Uri? AssetDownloadUrl,
    string? AssetName);

internal static class UpdateChecker
{
    // Keep this simple + explicit. If you fork the repo, update these constants.
    public const string RepoOwner = "smokydastona";
    public const string RepoName = "cm6206-dual_input_bass-shaker";

    public static Version GetCurrentVersion()
        => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0, 0);

    public static bool IsUpdateAvailable(Version current, Version latest)
        => latest > current;

    public static async Task<UpdateInfo?> TryGetLatestUpdateAsync(HttpClient http, CancellationToken cancellationToken)
        => await TryGetLatestUpdateAsync(http, Environment.ProcessPath, cancellationToken).ConfigureAwait(false);

    public static async Task<UpdateInfo?> TryGetLatestUpdateAsync(HttpClient http, string? currentExePath, CancellationToken cancellationToken)
    {
        try
        {
            // GitHub API requires a User-Agent.
            if (!http.DefaultRequestHeaders.UserAgent.Any())
                http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Cm6206DualRouter", "1"));

            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var resp = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                AppLog.Warn($"Update check failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? (tagEl.GetString() ?? "") : "";
            var html = root.TryGetProperty("html_url", out var htmlEl) ? (htmlEl.GetString() ?? "") : "";

            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(html))
                return null;

            // tags are expected to be like v1.2.3 or 1.2.3
            var versionText = tag.Trim();
            if (versionText.StartsWith('v') || versionText.StartsWith('V'))
                versionText = versionText[1..];

            if (!Version.TryParse(NormalizeTo4Part(versionText), out var latestVersion))
                return null;

            Uri? assetUrl = null;
            string? assetName = null;

            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                var assets = assetsEl.EnumerateArray().Select(a =>
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var dl = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    return (name, dl);
                }).Where(x => !string.IsNullOrWhiteSpace(x.name) && !string.IsNullOrWhiteSpace(x.dl)).ToList();

                // Important: GitHub releases often include an installer EXE (Inno Setup) plus a portable ZIP.
                // The in-app updater can only safely self-replace with a *portable* app EXE/ZIP.
                // If the app is installed under Program Files (not writable), prefer the installer.
                var installKind = GuessInstallKind(currentExePath);

                var installerExe = assets.FirstOrDefault(a => IsInstallerExeName(a.name!));
                var portableExe = assets.FirstOrDefault(a => string.Equals(a.name, "Cm6206DualRouter.exe", StringComparison.OrdinalIgnoreCase));
                var portableZip = assets.FirstOrDefault(a => IsPortableBundleZipName(a.name!));

                (string? name, string? dl) chosen;
                if (installKind == InstallKind.Installed)
                {
                    chosen = installerExe.name is not null ? installerExe
                        : portableZip.name is not null ? portableZip
                        : portableExe;
                }
                else
                {
                    chosen = portableExe.name is not null ? portableExe
                        : portableZip.name is not null ? portableZip
                        : default;
                }

                if (chosen.name is not null && chosen.dl is not null)
                {
                    assetName = chosen.name;
                    if (Uri.TryCreate(chosen.dl, UriKind.Absolute, out var parsed))
                        assetUrl = parsed;
                }
            }

            return new UpdateInfo(
                LatestVersion: latestVersion,
                TagName: tag,
                HtmlUrl: new Uri(html),
                AssetDownloadUrl: assetUrl,
                AssetName: assetName);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Update check error: {ex.Message}");
            return null;
        }
    }

    private enum InstallKind
    {
        Portable,
        Installed
    }

    private static InstallKind GuessInstallKind(string? exePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return InstallKind.Portable;

            var full = Path.GetFullPath(exePath);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrWhiteSpace(programFiles) && full.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
                return InstallKind.Installed;
            if (!string.IsNullOrWhiteSpace(programFilesX86) && full.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
                return InstallKind.Installed;

            return InstallKind.Portable;
        }
        catch
        {
            return InstallKind.Portable;
        }
    }

    private static bool IsInstallerExeName(string name)
    {
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return false;

        // Examples: Cm6206DualRouterSetup_0.2.2.exe, Cm6206DualRouterSetup_0.2.2-hotfix1.exe
        return name.StartsWith("Cm6206DualRouterSetup_", StringComparison.OrdinalIgnoreCase)
               || name.Contains("setup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPortableBundleZipName(string name)
    {
        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return false;

        // Avoid accidentally selecting unrelated zips (like the Minecraft UI bundle).
        if (name.Contains("neon_ui_bundle", StringComparison.OrdinalIgnoreCase))
            return false;

        return name.Contains("dual_router_bundle", StringComparison.OrdinalIgnoreCase)
               || name.Contains("cm6206_dual_router_bundle", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTo4Part(string versionText)
    {
        // Version parsing expects 2-4 parts. For tags like 1.2.3, treat as 1.2.3.0.
        var parts = versionText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 3)
            return versionText + ".0";
        return versionText;
    }
}
