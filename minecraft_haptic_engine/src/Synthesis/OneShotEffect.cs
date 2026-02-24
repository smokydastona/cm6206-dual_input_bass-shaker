using MinecraftHapticEngine.Config;
using MinecraftHapticEngine.Synthesis.Generators;
using MinecraftHapticEngine.Utils;
using NAudio.Dsp;

namespace MinecraftHapticEngine.Synthesis;

public sealed class OneShotEffect
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly float _gain;
    private readonly float[] _route;

    private readonly IGenerator _gen;
    private readonly EnvelopeAhr _env;

    private readonly BiQuadFilter?[] _lp;
    private readonly BiQuadFilter?[] _hp;

    public OneShotEffect(int sampleRate, int channels, float gainDb, float[] route, IGenerator gen, EnvelopeAhr env, FilterConfig? filter)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _gain = MathUtil.DbToLinear(gainDb);
        _route = route;
        _gen = gen;
        _env = env;

        _lp = new BiQuadFilter?[channels];
        _hp = new BiQuadFilter?[channels];

        if (filter?.LowPassHz is not null)
        {
            for (var ch = 0; ch < channels; ch++)
                _lp[ch] = BiQuadFilter.LowPassFilter(sampleRate, filter.LowPassHz.Value, 0.707f);
        }
        if (filter?.HighPassHz is not null)
        {
            for (var ch = 0; ch < channels; ch++)
                _hp[ch] = BiQuadFilter.HighPassFilter(sampleRate, filter.HighPassHz.Value, 0.707f);
        }
    }

    public static OneShotEffect Create(EffectMapping mapping, int sampleRate, int channels)
    {
        if (mapping.Generator is null)
        {
            throw new InvalidOperationException($"Mapping '{mapping.Name}' missing Generator");
        }

        var route = RoutePresets.Resolve(mapping.Route, channels);
        var gainDb = mapping.GainDb;

        var gen = mapping.Generator.Type.ToLowerInvariant() switch
        {
            "sine" => new SineGenerator(sampleRate, () => mapping.Generator.FrequencyHz),
            "noise" => new NoiseGenerator(mapping.Generator.NoiseColor),
            "impulse" => new ImpulseGenerator(),
            "sweep" => new SweepGenerator(sampleRate, mapping.Generator.FrequencyHz, mapping.Generator.FrequencyHzTo, mapping.Generator.DurationMs),
            _ => throw new InvalidOperationException($"Unknown generator type '{mapping.Generator.Type}'")
        };

        var envCfg = mapping.Envelope ?? new EnvelopeConfig();
        var env = new EnvelopeAhr(sampleRate, envCfg.AttackMs, envCfg.HoldMs, envCfg.ReleaseMs);

        return new OneShotEffect(sampleRate, channels, gainDb, route, gen, env, mapping.Filter);
    }

    public bool MixInto(float[] buffer, int offsetSamples, int frames)
    {
        for (var f = 0; f < frames; f++)
        {
            if (_env.IsFinished)
            {
                return false;
            }

            var g = _env.NextGain() * _gain;
            var s = _gen.NextSample() * g;

            for (var ch = 0; ch < _channels; ch++)
            {
                var v = s * _route[ch];

                var hp = _hp[ch];
                if (hp is not null) v = hp.Transform(v);

                var lp = _lp[ch];
                if (lp is not null) v = lp.Transform(v);

                buffer[offsetSamples + f * _channels + ch] += v;
            }
        }

        return true;
    }
}
