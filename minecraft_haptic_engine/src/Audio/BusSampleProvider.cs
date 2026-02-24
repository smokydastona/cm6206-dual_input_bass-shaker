using MinecraftHapticEngine.Synthesis;
using NAudio.Wave;

namespace MinecraftHapticEngine.Audio;

public sealed class BusSampleProvider : ISampleProvider
{
    private readonly EffectMixer _mixer;
    private readonly int _channels;
    private readonly int _bufferFrames;

    public BusSampleProvider(EffectMixer mixer, int sampleRate, int channels, int bufferFrames)
    {
        _mixer = mixer;
        _channels = channels;
        _bufferFrames = bufferFrames;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / _channels;
        // Keep latency predictable: process in fixed chunks.
        var totalWritten = 0;

        while (frames > 0)
        {
            var chunkFrames = Math.Min(frames, _bufferFrames);
            var chunkSamples = chunkFrames * _channels;

            _mixer.Render(buffer, offset + totalWritten, chunkFrames);

            totalWritten += chunkSamples;
            frames -= chunkFrames;
        }

        return count;
    }
}
