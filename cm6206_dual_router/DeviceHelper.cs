using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Text;

namespace Cm6206DualRouter;

public static class DeviceHelper
{
    public const string DefaultSystemRenderDevice = "Default Game Output";
    public const string NoneDevice = "(None)";

    public static string? TryFindLikelyCm6206OutputRenderDeviceFriendlyName()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            static bool HasAny(string s, params string[] needles)
                => needles.Any(n => s.Contains(n, StringComparison.OrdinalIgnoreCase));

            // Highest confidence: looks like the generic CMUAC name + is configured to 7.1 (8ch mix).
            string? best8Ch = null;
            string? bestAny = null;

            foreach (var d in devices)
            {
                var name = d.FriendlyName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // Typical CM6206-class adapters show up as generic USB audio class speakers.
                var looksLikeCm6206 = HasAny(
                    name,
                    "CM6206",
                    "C-Media",
                    "CMedia",
                    "USB Audio Class 1.0 and 2.0 Device Driver",
                    "USB 7.1",
                    "USB Audio");

                if (!looksLikeCm6206)
                    continue;

                bestAny ??= name;

                try
                {
                    var mix = d.AudioClient?.MixFormat;
                    if (mix is not null && mix.Channels == 8)
                    {
                        best8Ch = name;
                        break;
                    }
                }
                catch
                {
                    // ignore per-device probing failures
                }
            }

            return best8Ch ?? bestAny;
        }
        catch
        {
            return null;
        }
    }

    public static void PrintRenderDevices(bool showFormats)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        Console.WriteLine("Active playback devices:");
        foreach (var d in devices)
        {
            var suffix = showFormats ? FormatMixSuffix(d) : string.Empty;
            Console.WriteLine($"- {d.FriendlyName}{suffix}");
        }

        Console.WriteLine();
        Console.WriteLine("Tip: copy/paste the exact names into router.json");
    }

    public static void PrintCaptureDevices(bool showFormats)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        Console.WriteLine("Active capture devices:");
        foreach (var d in devices)
        {
            var suffix = showFormats ? FormatMixSuffix(d) : string.Empty;
            Console.WriteLine($"- {d.FriendlyName}{suffix}");
        }

        Console.WriteLine();
        Console.WriteLine("Tip: latency measurement needs a mic/line-in here.");
    }

    private static string FormatMixSuffix(MMDevice device)
    {
        try
        {
            var mix = device.AudioClient?.MixFormat;
            if (mix is null)
                return "";

            var channels = mix.Channels;
            var sampleRate = mix.SampleRate;

            // Keep this intentionally concise so --list-devices stays readable.
            // Highlight common surround layouts so it's easy to spot the virtual endpoints.
            var highlight = channels is 6 or 8 ? "*" : " ";
            return $"  {highlight}[mix: {sampleRate} Hz, {channels}ch]";
        }
        catch (Exception ex)
        {
            return $"  [mix: unavailable: {ex.Message}]";
        }
    }

    public static MMDevice GetRenderDeviceByFriendlyName(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
            throw new ArgumentException("friendlyName is required", nameof(friendlyName));

        var search = friendlyName.Trim();

        if (string.Equals(search, DefaultSystemRenderDevice, StringComparison.OrdinalIgnoreCase))
        {
            using var enumeratorDefault = new MMDeviceEnumerator();
            // Multimedia is the common "default output" for games/desktop audio.
            return enumeratorDefault.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        if (string.Equals(search, NoneDevice, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Device '(None)' cannot be opened as an audio device.");

        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        var match = devices.FirstOrDefault(d => string.Equals(d.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        // fallback: contains
        match = devices.FirstOrDefault(d => d.FriendlyName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        if (match is not null)
            return match;

        var available = devices
            .Select(d => d.FriendlyName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var suggestions = GetBestMatches(search, available, max: 5);
        var availableText = FormatDeviceList(available, max: 12);
        var suggestionsText = suggestions.Length > 0 ? $" Closest matches: {string.Join("; ", suggestions)}." : string.Empty;

        throw new InvalidOperationException(
            $"Render device not found: {friendlyName}. " +
            $"Available playback devices: {availableText}.{suggestionsText} " +
            "Use --list-devices.");
    }

    public static MMDevice GetCaptureDeviceByFriendlyName(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
            throw new ArgumentException("friendlyName is required", nameof(friendlyName));

        var search = friendlyName.Trim();

        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        var match = devices.FirstOrDefault(d => string.Equals(d.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        // fallback: contains
        match = devices.FirstOrDefault(d => d.FriendlyName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        if (match is not null)
            return match;

        var available = devices
            .Select(d => d.FriendlyName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var suggestions = GetBestMatches(search, available, max: 5);
        var availableText = FormatDeviceList(available, max: 12);
        var suggestionsText = suggestions.Length > 0 ? $" Closest matches: {string.Join("; ", suggestions)}." : string.Empty;

        throw new InvalidOperationException(
            $"Capture device not found: {friendlyName}. " +
            $"Available capture devices: {availableText}.{suggestionsText} " +
            "Use --list-devices.");
    }

    private static string FormatDeviceList(string[] names, int max)
    {
        if (names.Length == 0)
            return "(none)";

        var shown = names.Take(Math.Max(1, max)).ToArray();
        if (shown.Length == names.Length)
            return string.Join("; ", shown);

        return string.Join("; ", shown) + $"; ... (+{names.Length - shown.Length} more)";
    }

    private static string[] GetBestMatches(string search, string[] candidates, int max)
    {
        if (string.IsNullOrWhiteSpace(search) || candidates.Length == 0 || max <= 0)
            return Array.Empty<string>();

        var s = NormalizeForMatch(search);
        if (s.Length == 0)
            return Array.Empty<string>();

        // Prefer contains (either direction) and then token overlap.
        var ranked = candidates
            .Select(c => new { Name = c, Score = ScoreCandidate(NormalizeForMatch(c), s) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(x => x.Name)
            .ToArray();

        return ranked;
    }

    private static int ScoreCandidate(string candidateNorm, string searchNorm)
    {
        if (candidateNorm.Length == 0 || searchNorm.Length == 0)
            return 0;

        if (candidateNorm.Equals(searchNorm, StringComparison.Ordinal))
            return 100;

        if (candidateNorm.Contains(searchNorm, StringComparison.Ordinal) || searchNorm.Contains(candidateNorm, StringComparison.Ordinal))
            return 70;

        // Token overlap on whitespace-split original-ish segments.
        // We re-split normalized strings on '_' which we use as a separator.
        var cTokens = candidateNorm.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sTokens = searchNorm.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (cTokens.Length == 0 || sTokens.Length == 0)
            return 0;

        var overlap = 0;
        foreach (var st in sTokens)
        {
            if (st.Length < 2)
                continue;
            if (cTokens.Any(ct => ct.Contains(st, StringComparison.Ordinal) || st.Contains(ct, StringComparison.Ordinal)))
                overlap++;
        }

        return overlap == 0 ? 0 : 30 + overlap;
    }

    private static string NormalizeForMatch(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        var sb = new StringBuilder(s.Length);
        var lastWasSep = false;

        foreach (var ch in s.Trim())
        {
            var isWord = char.IsLetterOrDigit(ch);
            if (isWord)
            {
                sb.Append(char.ToUpperInvariant(ch));
                lastWasSep = false;
                continue;
            }

            if (!lastWasSep)
            {
                sb.Append('_');
                lastWasSep = true;
            }
        }

        // Trim separator at end.
        while (sb.Length > 0 && sb[^1] == '_')
            sb.Length--;

        return sb.ToString();
    }
}
