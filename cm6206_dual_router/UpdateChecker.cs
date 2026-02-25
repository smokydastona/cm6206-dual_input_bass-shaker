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

                // Prefer a direct .exe; fall back to .zip
                var exe = assets.FirstOrDefault(a => a.name!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                var zip = assets.FirstOrDefault(a => a.name!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                var chosen = exe.name is not null ? exe : zip;
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

    private static string NormalizeTo4Part(string versionText)
    {
        // Version parsing expects 2-4 parts. For tags like 1.2.3, treat as 1.2.3.0.
        var parts = versionText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 3)
            return versionText + ".0";
        return versionText;
    }
}
