using NAudio.CoreAudioApi;

namespace MinecraftHapticEngine.Utils;

public static class DeviceLister
{
    public static void PrintRenderDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        Console.WriteLine("Render devices:");
        foreach (var d in devices)
        {
            Console.WriteLine($"- {d.FriendlyName}");
        }
    }

    public static MMDevice FindRenderDeviceByName(string deviceName)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        var exact = devices.FirstOrDefault(d => string.Equals(d.FriendlyName, deviceName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var contains = devices.FirstOrDefault(d => d.FriendlyName.Contains(deviceName, StringComparison.OrdinalIgnoreCase));
        if (contains is not null)
        {
            return contains;
        }

        throw new InvalidOperationException($"Render device not found: '{deviceName}'. Use --list-devices.");
    }
}
