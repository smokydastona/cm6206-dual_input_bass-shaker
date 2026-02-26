using NAudio.CoreAudioApi;
using NAudio.Wave;

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

        throw new InvalidOperationException($"Render device not found: {friendlyName}. Use --list-devices.");
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

        throw new InvalidOperationException($"Capture device not found: {friendlyName}. Use --list-devices.");
    }
}
