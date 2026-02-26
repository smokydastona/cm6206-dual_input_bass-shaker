using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cm6206DualRouter.Telemetry;

internal sealed class AutoFallbackShakerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _primary;
    private readonly ISampleProvider _fallback;
    private readonly Func<bool> _primaryHealthy;

    private readonly int _sampleRate;

    // 0 => fallback, 1 => primary
    private float _mix01;

    public AutoFallbackShakerSampleProvider(ISampleProvider primary, ISampleProvider fallback, Func<bool> primaryHealthy)
    {
        if (primary.WaveFormat.Channels != 2) throw new ArgumentException("primary must be stereo", nameof(primary));
        if (fallback.WaveFormat.Channels != 2) throw new ArgumentException("fallback must be stereo", nameof(fallback));
        if (primary.WaveFormat.SampleRate != fallback.WaveFormat.SampleRate)
            throw new ArgumentException("primary and fallback must have the same sample rate");

        _primary = primary;
        _fallback = fallback;
        _primaryHealthy = primaryHealthy;
        _sampleRate = primary.WaveFormat.SampleRate;

        WaveFormat = primary.WaveFormat;
        _mix01 = 0f;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        if (frames <= 0)
            return 0;

        var tmpA = ArrayPool<float>.Shared.Rent(frames * 2);
        var tmpB = ArrayPool<float>.Shared.Rent(frames * 2);

        try
        {
            var aRead = _primary.Read(tmpA, 0, frames * 2);
            var bRead = _fallback.Read(tmpB, 0, frames * 2);

            var healthy = false;
            try { healthy = _primaryHealthy(); } catch { /* ignore */ }

            var target = healthy ? 1f : 0f;

            // Fast fade to fallback when telemetry drops; slower fade-in when it returns.
            var tauSec = target < _mix01 ? 0.10f : 0.25f;
            var alpha = 1.0f - (float)Math.Exp(-1.0 / (tauSec * _sampleRate));

            for (var i = 0; i < frames; i++)
            {
                _mix01 += (target - _mix01) * alpha;

                var aL = i * 2 < aRead ? tmpA[i * 2] : 0f;
                var aR = i * 2 + 1 < aRead ? tmpA[i * 2 + 1] : 0f;
                var bL = i * 2 < bRead ? tmpB[i * 2] : 0f;
                var bR = i * 2 + 1 < bRead ? tmpB[i * 2 + 1] : 0f;

                // Equal-power-ish crossfade (simple and stable).
                var wA = _mix01;
                var wB = 1f - _mix01;

                buffer[offset + i * 2] = aL * wA + bL * wB;
                buffer[offset + i * 2 + 1] = aR * wA + bR * wB;
            }

            return frames * 2;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(tmpA);
            ArrayPool<float>.Shared.Return(tmpB);
        }
    }
}
