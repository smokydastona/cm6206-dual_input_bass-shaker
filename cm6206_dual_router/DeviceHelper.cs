using NAudio.CoreAudioApi;

namespace Cm6206DualRouter;

public static class DeviceHelper
{
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

    public static MMDevice GetRenderDeviceByFriendlyName(string friendlyName)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        var match = devices.FirstOrDefault(d => string.Equals(d.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        // fallback: contains
        match = devices.FirstOrDefault(d => d.FriendlyName.Contains(friendlyName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        throw new InvalidOperationException($"Render device not found: {friendlyName}. Use --list-devices.");
    }
}
