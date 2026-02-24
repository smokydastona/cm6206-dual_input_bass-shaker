namespace MinecraftHapticEngine.Synthesis;

public sealed class EnvelopeAhr
{
    private readonly int _attackSamples;
    private readonly int _holdSamples;
    private readonly int _releaseSamples;

    private int _pos;

    public EnvelopeAhr(int sampleRate, float attackMs, float holdMs, float releaseMs)
    {
        _attackSamples = MsToSamples(sampleRate, attackMs);
        _holdSamples = MsToSamples(sampleRate, holdMs);
        _releaseSamples = Math.Max(1, MsToSamples(sampleRate, releaseMs));
    }

    public void Reset() => _pos = 0;

    public bool IsFinished => _pos >= _attackSamples + _holdSamples + _releaseSamples;

    public float NextGain()
    {
        var aEnd = _attackSamples;
        var hEnd = _attackSamples + _holdSamples;
        var rEnd = _attackSamples + _holdSamples + _releaseSamples;

        float g;
        if (_pos < aEnd)
        {
            g = _attackSamples <= 0 ? 1 : (float)_pos / _attackSamples;
        }
        else if (_pos < hEnd)
        {
            g = 1;
        }
        else if (_pos < rEnd)
        {
            var rPos = _pos - hEnd;
            g = 1 - (float)rPos / _releaseSamples;
        }
        else
        {
            g = 0;
        }

        _pos++;
        return g;
    }

    private static int MsToSamples(int sampleRate, float ms) => ms <= 0 ? 0 : (int)Math.Round(sampleRate * (ms / 1000f));
}
