using NAudio.CoreAudioApi;

namespace Cm6206DualRouter;

public static class DeviceHelper
{
    public const string DefaultSystemRenderDevice = "Default Game Output";
    public const string NoneDevice = "(None)";

    public static void PrintRenderDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        Console.WriteLine("Active playback devices:");
        foreach (var d in devices)
        {
            Console.WriteLine($"- {d.FriendlyName}");
        }

        Console.WriteLine();
        Console.WriteLine("Tip: copy/paste the exact names into router.json");
    }

    public static void PrintCaptureDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        Console.WriteLine("Active capture devices:");
        foreach (var d in devices)
        {
            Console.WriteLine($"- {d.FriendlyName}");
        }

        Console.WriteLine();
        Console.WriteLine("Tip: latency measurement needs a mic/line-in here.");
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
