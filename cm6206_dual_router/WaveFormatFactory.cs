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
        var ext = new WaveFormatExtensible(sampleRate, 32, 8)
        {
            ChannelMask = (WaveFormatExtensible.ChannelMask)Speaker7Point1Mask,
            SubFormat = AudioSubtypes.IeeeFloat
        };

        return ext;
    }
}
