using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cm6206DualRouter;

/// <summary>
/// Downmixes an N-channel float stream to stereo.
/// Strategy:
/// - If >=2 channels: take channel 0 as L and channel 1 as R, and add a small amount of the remaining channels.
/// - If 1 channel: duplicate mono to L/R.
/// </summary>
public sealed class DownmixToStereoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _sourceChannels;

    public DownmixToStereoSampleProvider(ISampleProvider source)
    {
        _source = source;
        _sourceChannels = source.WaveFormat.Channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var framesRequested = count / 2;
        if (framesRequested == 0)
            return 0;

        var sourceSamplesNeeded = framesRequested * _sourceChannels;
        var sourceBuffer = ArrayPool<float>.Shared.Rent(sourceSamplesNeeded);
        try
        {
            var sourceSamplesRead = _source.Read(sourceBuffer, 0, sourceSamplesNeeded);
            var framesRead = sourceSamplesRead / _sourceChannels;

            for (var frame = 0; frame < framesRequested; frame++)
            {
                var outIndex = offset + (frame * 2);
                if (frame >= framesRead)
                {
                    buffer[outIndex] = 0;
                    buffer[outIndex + 1] = 0;
                    continue;
                }

                if (_sourceChannels == 1)
                {
                    var mono = sourceBuffer[frame];
                    buffer[outIndex] = mono;
                    buffer[outIndex + 1] = mono;
                    continue;
                }

                var baseIndex = frame * _sourceChannels;
                var left = sourceBuffer[baseIndex];
                var right = sourceBuffer[baseIndex + 1];

                // Add a little of the remaining channels to both sides (keeps loopback from losing content)
                if (_sourceChannels > 2)
                {
                    var extraMix = 0f;
                    for (var c = 2; c < _sourceChannels; c++)
                    {
                        extraMix += sourceBuffer[baseIndex + c];
                    }

                    var extraScaled = extraMix * (0.15f / (_sourceChannels - 1));
                    left += extraScaled;
                    right += extraScaled;
                }

                buffer[outIndex] = left;
                buffer[outIndex + 1] = right;
            }

            return framesRequested * 2;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(sourceBuffer);
        }
    }
}
