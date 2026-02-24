using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Cm6206DualRouter;

public static class OutputFormatNegotiator
{
    // Common rates worth trying for UAC1-ish devices.
    private static readonly int[] FallbackRates = [44100, 48000, 88200, 96000, 176400, 192000];

    public static IReadOnlyList<int> CandidateSampleRates => FallbackRates;

    public sealed record NegotiationResult(RouterConfig EffectiveConfig, string? Warning);

    public static NegotiationResult Negotiate(RouterConfig config, MMDevice outputDevice)
    {
        var effective = config.Clone();
        var blacklist = new HashSet<int>(effective.BlacklistedSampleRates ?? [], EqualityComparer<int>.Default);

        // Mix format describes what shared-mode actually runs at.
        var mix = outputDevice.AudioClient.MixFormat;

        if (!effective.UseExclusiveMode)
        {
            // Shared mode: the engine runs at mix format. If the device is set to stereo in Windows,
            // a 7.1 stream will fail.
            if (mix.Channels != 8)
            {
                throw new InvalidOperationException(
                    $"Output device mix format is {mix.Channels}ch. Set Windows Sound settings for this device to 7.1 (Configure speakers / Advanced format), then retry.");
            }

            // Prefer using the mix sample rate for shared mode to avoid unsupported-format errors.
            if (effective.SampleRate != mix.SampleRate)
            {
                var warn = $"Shared mode uses Windows mix format ({mix.SampleRate} Hz). sampleRate={effective.SampleRate} only applies in Exclusive mode.";
                effective.SampleRate = mix.SampleRate;
                return new NegotiationResult(effective, warn);
            }

            return new NegotiationResult(effective, null);
        }

        // Exclusive mode: we must open exactly the format we request.
        if (blacklist.Contains(effective.SampleRate) || !IsExclusiveSupported(outputDevice, effective.SampleRate))
        {
            var original = effective.SampleRate;

            foreach (var sr in FallbackRates)
            {
                if (blacklist.Contains(sr))
                    continue;
                if (IsExclusiveSupported(outputDevice, sr))
                {
                    effective.SampleRate = sr;
                    return new NegotiationResult(
                        effective,
                        $"Exclusive mode format {original} Hz is not supported (or blacklisted). Using {sr} Hz instead.");
                }
            }

            throw new InvalidOperationException(
                $"Exclusive mode could not find a supported 7.1 float sample rate (requested {original} Hz). Try Shared mode, or change Windows device format, or clear blacklistedSampleRates.");
        }

        return new NegotiationResult(effective, null);
    }

    public static bool IsExclusiveSupported(MMDevice outputDevice, int sampleRate)
    {
        try
        {
            var fmt = WaveFormatFactory.Create7Point1Float(sampleRate);
            var ac = outputDevice.AudioClient;
            return ac.IsFormatSupported(AudioClientShareMode.Exclusive, fmt);
        }
        catch
        {
            return false;
        }
    }
}
