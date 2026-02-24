using MinecraftHapticEngine.Utils;
using NAudio.Dsp;

namespace MinecraftHapticEngine.Synthesis;

public sealed class EffectMixer
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly float _busGain;
    private readonly float _delaySamples;

    private readonly object _gate = new();
    private readonly List<OneShotEffect> _oneShots = new();
    private readonly List<ContinuousEffect> _continuous = new();

    private readonly BiQuadFilter?[] _lp;
    private readonly BiQuadFilter?[] _hp;

    private readonly MultiChannelDelayLine? _delay;

    public EffectMixer(int sampleRate, int channels, float gainDb, float? lowPassHz, float? highPassHz, float delayMs)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _busGain = MathUtil.DbToLinear(gainDb);

        _lp = new BiQuadFilter?[channels];
        _hp = new BiQuadFilter?[channels];

        if (lowPassHz is not null)
        {
            for (var ch = 0; ch < channels; ch++)
            {
                _lp[ch] = BiQuadFilter.LowPassFilter(sampleRate, lowPassHz.Value, 0.707f);
            }
        }

        if (highPassHz is not null)
        {
            for (var ch = 0; ch < channels; ch++)
            {
                _hp[ch] = BiQuadFilter.HighPassFilter(sampleRate, highPassHz.Value, 0.707f);
            }
        }

        _delaySamples = delayMs <= 0 ? 0 : delayMs * sampleRate / 1000f;
        if (_delaySamples >= 1)
        {
            _delay = new MultiChannelDelayLine(channels, (int)Math.Ceiling(_delaySamples) + 2);
        }
    }

    public void AddOneShot(OneShotEffect effect)
    {
        lock (_gate)
        {
            _oneShots.Add(effect);
        }
    }

    public void AddContinuous(ContinuousEffect effect)
    {
        // Continuous effects are created on startup and persist.
        lock (_gate)
        {
            _continuous.Add(effect);
        }
    }

    public void Render(float[] output, int offsetSamples, int frames)
    {
        Array.Clear(output, offsetSamples, frames * _channels);

        lock (_gate)
        {
            for (var i = _oneShots.Count - 1; i >= 0; i--)
            {
                var alive = _oneShots[i].MixInto(output, offsetSamples, frames);
                if (!alive)
                {
                    _oneShots.RemoveAt(i);
                }
            }

            foreach (var c in _continuous)
            {
                c.MixInto(output, offsetSamples, frames);
            }
        }

        // Bus-level filters and gain.
        var totalSamples = frames * _channels;
        for (var s = 0; s < totalSamples; s++)
        {
            var ch = s % _channels;
            var v = output[offsetSamples + s] * _busGain;

            var hp = _hp[ch];
            if (hp is not null) v = hp.Transform(v);

            var lp = _lp[ch];
            if (lp is not null) v = lp.Transform(v);

            output[offsetSamples + s] = v;
        }

        if (_delay is not null)
        {
            _delay.ProcessInPlace(output, offsetSamples, frames, _delaySamples);
        }
    }
}
