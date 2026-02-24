using System.Reflection;
using NAudio.Wave;

namespace Cm6206DualRouter;

public static class WaveFormatFactory
{
    // Channel mask for 7.1: FL FR FC LFE BL BR SL SR
    private const int Speaker7Point1Mask =
        0x00000001 | // FRONT_LEFT
        0x00000002 | // FRONT_RIGHT
        0x00000004 | // FRONT_CENTER
        0x00000008 | // LOW_FREQUENCY
        0x00000010 | // BACK_LEFT
        0x00000020 | // BACK_RIGHT
        0x00000200 | // SIDE_LEFT
        0x00000400;  // SIDE_RIGHT

    public static WaveFormat Create7Point1Float(int sampleRate)
    {
        // NAudio 2.2.x: WaveFormatExtensible doesn't expose public setters for channel mask / subformat.
        // The ctor sets SubFormat to IEEE float when bits==32.
        var ext = new WaveFormatExtensible(sampleRate, 32, 8);
        TrySetChannelMask(ext, Speaker7Point1Mask);
        return ext;
    }

    private static void TrySetChannelMask(WaveFormatExtensible ext, int mask)
    {
        // Best-effort: ensure Windows sees the correct speaker positions for 7.1 (side channels)
        // without taking a hard dependency on NAudio internals.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(WaveFormatExtensible).GetField("dwChannelMask", flags);
        if (field is null || field.FieldType != typeof(int))
            return;

        field.SetValue(ext, mask);
    }
}
