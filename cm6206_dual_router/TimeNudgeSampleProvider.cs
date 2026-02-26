using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cm6206DualRouter;

/// <summary>
/// Applies small timing corrections ("nudges") to a pull-based audio stream.
///
/// This is meant for keeping a buffered input near a target fill level when the producer clock
/// drifts slightly vs. the consumer clock.
///
/// Strategy:
/// - If the upstream buffer is too full: read a few extra frames and discard them (speeds up consumption).
/// - If the upstream buffer is too empty: read fewer frames and duplicate the last frame to fill (slows consumption).
///
/// Designed primarily for low-frequency "shaker" audio where occasional micro-adjustments are acceptable.
/// </summary>
public sealed class TimeNudgeSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly Func<double?> _getUpstreamBufferMs;

    private readonly int _channels;

    private readonly int _targetBufferMs;
    private readonly int _deadbandMs;

    private float[] _scratch = Array.Empty<float>();

    private float[] _lastFrame;

    public long TotalDropFrames { get; private set; }
    public long TotalInsertFrames { get; private set; }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public TimeNudgeSampleProvider(
        ISampleProvider source,
        Func<double?> getUpstreamBufferMs,
        int targetBufferMs = 250,
        int deadbandMs = 50)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _getUpstreamBufferMs = getUpstreamBufferMs ?? throw new ArgumentNullException(nameof(getUpstreamBufferMs));

        if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat && source.WaveFormat.BitsPerSample != 32)
            throw new ArgumentException("TimeNudgeSampleProvider expects 32-bit float samples", nameof(source));

        if (targetBufferMs < 0 || targetBufferMs > 2000)
            throw new ArgumentOutOfRangeException(nameof(targetBufferMs), "targetBufferMs out of range (0..2000)");
        if (deadbandMs < 0 || deadbandMs > 1000)
            throw new ArgumentOutOfRangeException(nameof(deadbandMs), "deadbandMs out of range (0..1000)");

        _channels = source.WaveFormat.Channels;
        if (_channels <= 0)
            throw new ArgumentException("Invalid channel count", nameof(source));

        _targetBufferMs = targetBufferMs;
        _deadbandMs = deadbandMs;

        _lastFrame = new float[_channels];
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0)
            return 0;

        // Ensure we always output a whole number of frames.
        var requestedFrames = count / _channels;
        var requestedSamples = requestedFrames * _channels;
        if (requestedSamples == 0)
            return 0;

        var upstreamMs = _getUpstreamBufferMs();
        var adjustFrames = ComputeAdjustmentFrames(upstreamMs, requestedFrames);

        // Speed up: consume more than we output.
        if (adjustFrames > 0)
        {
            var readFrames = requestedFrames + adjustFrames;
            var readSamples = readFrames * _channels;
            EnsureScratch(readSamples);

            var n = _source.Read(_scratch, 0, readSamples);
            if (n < readSamples)
                Array.Clear(_scratch, n, readSamples - n);

            Array.Copy(_scratch, 0, buffer, offset, requestedSamples);
            UpdateLastFrameFromOutput(buffer, offset, requestedSamples);

            TotalDropFrames += adjustFrames;
            return requestedSamples;
        }

        // Slow down: consume less than we output.
        if (adjustFrames < 0)
        {
            var insertFrames = -adjustFrames;
            var readFrames = Math.Max(0, requestedFrames - insertFrames);
            var readSamples = readFrames * _channels;

            var n = 0;
            if (readSamples > 0)
            {
                n = _source.Read(buffer, offset, readSamples);
                if (n < readSamples)
                    Array.Clear(buffer, offset + n, readSamples - n);
            }

            // Fill the remainder by duplicating the last frame (or silence if we have none).
            var remainingSamples = requestedSamples - Math.Min(readSamples, requestedSamples);
            if (remainingSamples > 0)
            {
                var fillOffset = offset + (requestedSamples - remainingSamples);

                if (readSamples > 0)
                {
                    // Last frame is the last fully written frame in the output.
                    CopyFrame(buffer, offset + Math.Max(0, (readSamples / _channels - 1) * _channels), _lastFrame);
                }

                var remainingFrames = remainingSamples / _channels;
                for (var i = 0; i < remainingFrames; i++)
                {
                    for (var c = 0; c < _channels; c++)
                        buffer[fillOffset + i * _channels + c] = _lastFrame[c];
                }
            }

            UpdateLastFrameFromOutput(buffer, offset, requestedSamples);

            TotalInsertFrames += insertFrames;
            return requestedSamples;
        }

        // No adjustment.
        var read = _source.Read(buffer, offset, requestedSamples);
        if (read < requestedSamples)
            Array.Clear(buffer, offset + read, requestedSamples - read);

        UpdateLastFrameFromOutput(buffer, offset, requestedSamples);
        return requestedSamples;
    }

    private int ComputeAdjustmentFrames(double? upstreamMs, int requestedFrames)
    {
        if (upstreamMs is null)
            return 0;

        var errorMs = upstreamMs.Value - _targetBufferMs;
        if (Math.Abs(errorMs) <= _deadbandMs)
            return 0;

        // Clamp to small, gentle corrections per read.
        // Keep it bounded so we don't create audible artifacts.
        var maxAdjustFrames = Math.Clamp(requestedFrames / 200, 1, 64); // ~0.5% of the block, capped

        var severity = Math.Clamp(Math.Abs(errorMs) / 250.0, 0.0, 1.0);
        var adjust = (int)Math.Round(severity * maxAdjustFrames);

        // Align to a small frame quantum to avoid too-frequent tiny toggles.
        const int quantum = 8;
        adjust = Math.Max(quantum, (adjust / quantum) * quantum);
        adjust = Math.Min(adjust, maxAdjustFrames);

        return errorMs > 0 ? adjust : -adjust;
    }

    private void EnsureScratch(int neededSamples)
    {
        if (_scratch.Length < neededSamples)
            _scratch = new float[Math.Max(neededSamples, _scratch.Length * 2)];
    }

    private void UpdateLastFrameFromOutput(float[] buffer, int offset, int samples)
    {
        if (samples < _channels)
            return;

        CopyFrame(buffer, offset + samples - _channels, _lastFrame);
    }

    private void CopyFrame(float[] source, int frameOffset, float[] dest)
    {
        for (var c = 0; c < _channels; c++)
            dest[c] = source[frameOffset + c];
    }
}
