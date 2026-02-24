namespace MinecraftHapticEngine.Synthesis;

public sealed class MultiChannelDelayLine
{
    private readonly int _channels;
    private readonly float[] _buffer;
    private int _writeIndex;

    public MultiChannelDelayLine(int channels, int maxDelaySamples)
    {
        _channels = channels;
        _buffer = new float[maxDelaySamples * channels];
    }

    public void ProcessInPlace(float[] audio, int offsetSamples, int frames, float delaySamples)
    {
        // Fractional delay via linear interpolation.
        var delayInt = (int)Math.Floor(delaySamples);
        var frac = delaySamples - delayInt;

        var samples = frames * _channels;
        for (var i = 0; i < samples; i++)
        {
            var input = audio[offsetSamples + i];

            var readIndex0 = _writeIndex - delayInt * _channels;
            while (readIndex0 < 0) readIndex0 += _buffer.Length;

            var readIndex1 = readIndex0 - _channels;
            while (readIndex1 < 0) readIndex1 += _buffer.Length;

            var y0 = _buffer[readIndex0];
            var y1 = _buffer[readIndex1];
            var delayed = y0 + (y1 - y0) * frac;

            _buffer[_writeIndex] = input;
            _writeIndex++;
            if (_writeIndex >= _buffer.Length) _writeIndex = 0;

            audio[offsetSamples + i] = delayed;
        }
    }
}
