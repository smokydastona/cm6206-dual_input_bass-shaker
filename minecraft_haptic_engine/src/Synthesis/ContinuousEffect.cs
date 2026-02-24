using MinecraftHapticEngine.Config;
using MinecraftHapticEngine.Synthesis.Generators;
using MinecraftHapticEngine.Utils;
using NAudio.Dsp;

namespace MinecraftHapticEngine.Synthesis;

public sealed class ContinuousEffect
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly float _gain;
    private readonly float[] _route;

    private readonly IGenerator _gen;

    private readonly BiQuadFilter?[] _lp;
    private readonly BiQuadFilter?[] _hp;

    private float _amp;
    private float _freq;

    private readonly EffectMapping _mapping;

    private ContinuousEffect(EffectMapping mapping, int sampleRate, int channels, float[] route, IGenerator gen, float gainDb, FilterConfig? filter)
    {
        _mapping = mapping;
        _sampleRate = sampleRate;
        _channels = channels;
        _route = route;
        _gen = gen;
        _gain = MathUtil.DbToLinear(gainDb);

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

        _freq = mapping.Generator?.FrequencyHz ?? 60;
        _amp = 0;
    }

    public static ContinuousEffect Create(EffectMapping mapping, int sampleRate, int channels)
    {
        if (mapping.Generator is null)
        {
            throw new InvalidOperationException($"Continuous mapping '{mapping.Name}' missing Generator");
        }

        var route = RoutePresets.Resolve(mapping.Route, channels);

        // Continuous generators must be able to read a dynamically-updated frequency.
        // We create the effect first, then build generators that capture its fields.
        ContinuousEffect? effect = null;

        IGenerator gen = mapping.Generator.Type.ToLowerInvariant() switch
        {
            "sine" => new SineGenerator(sampleRate, () => Volatile.Read(ref effect!._freq)),
            "noise" => new NoiseGenerator(mapping.Generator.NoiseColor),
            // Impulse / sweep are typically one-shots; keep them unsupported to avoid confusion.
            _ => throw new InvalidOperationException($"Continuous generator '{mapping.Generator.Type}' not supported (use 'sine' or 'noise')")
        };

        effect = new ContinuousEffect(mapping, sampleRate, channels, route, gen, mapping.GainDb, mapping.Filter);
        effect._freq = mapping.Generator.FrequencyHz;
        return effect;
    }

    public void UpdateFromTelemetry(float speed, float accel, bool elytra)
    {
        var cont = _mapping.Continuous;
        if (cont?.AmplitudeFrom is not null)
        {
            var val = GetTelemetryField(cont.AmplitudeFrom.Field, speed, accel, elytra);
            var a = MathUtil.MapTelemetry(val, cont.AmplitudeFrom.Min, cont.AmplitudeFrom.Max, cont.AmplitudeFrom.Curve, cont.AmplitudeFrom.Scale, cont.AmplitudeFrom.Offset);
            _amp = a;
        }

        if (cont?.FrequencyFrom is not null)
        {
            var val = GetTelemetryField(cont.FrequencyFrom.Field, speed, accel, elytra);
            var f = MathUtil.MapTelemetry(val, cont.FrequencyFrom.Min, cont.FrequencyFrom.Max, cont.FrequencyFrom.Curve, cont.FrequencyFrom.Scale, cont.FrequencyFrom.Offset);
            _freq = Math.Max(0, f);
        }
    }

    public void MixInto(float[] buffer, int offsetSamples, int frames)
    {
        var amp = _amp;
        if (amp <= 0.0001f)
        {
            // still advance generator to avoid phase lock when amplitude returns
            for (var i = 0; i < frames; i++) _gen.NextSample();
            return;
        }

        for (var f = 0; f < frames; f++)
        {
            var s = _gen.NextSample() * amp * _gain;
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
    }

    private static float GetTelemetryField(string field, float speed, float accel, bool elytra) => field.ToLowerInvariant() switch
    {
        "speed" => speed,
        "accel" => accel,
        "elytra" => elytra ? 1 : 0,
        _ => 0,
    };
}
