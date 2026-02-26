using NAudio.Wave;

namespace Cm6206DualRouter;

/// <summary>
/// Adapts an <see cref="ISampleProvider"/> (float samples) to <see cref="IWaveProvider"/>
/// without requiring <see cref="WaveFormatEncoding.IeeeFloat"/>.
///
/// NAudio's built-in SampleToWaveProvider rejects WaveFormatExtensible (Encoding=Extensible)
/// even when SubFormat is IEEE float. WASAPI multichannel commonly uses extensible formats.
/// </summary>
public sealed class FloatSampleToWaveProvider : IWaveProvider
{
    // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT
    private static readonly Guid IeeeFloatSubFormat = new(0x00000003, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71);

    private readonly ISampleProvider _source;
    private float[] _floatBuffer;

    public FloatSampleToWaveProvider(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        WaveFormat = ValidateAndGetWaveFormat(source.WaveFormat);
        _floatBuffer = Array.Empty<float>();
    }

    public WaveFormat WaveFormat { get; }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

        // We only support 32-bit float (4 bytes per sample).
        var alignedByteCount = count - (count % 4);
        if (alignedByteCount == 0)
            return 0;

        var samplesRequested = alignedByteCount / 4;
        if (_floatBuffer.Length < samplesRequested)
            _floatBuffer = new float[samplesRequested];

        var samplesRead = _source.Read(_floatBuffer, 0, samplesRequested);
        if (samplesRead <= 0)
            return 0;

        Buffer.BlockCopy(_floatBuffer, 0, buffer, offset, samplesRead * 4);
        return samplesRead * 4;
    }

    private static WaveFormat ValidateAndGetWaveFormat(WaveFormat waveFormat)
    {
        if (waveFormat.BitsPerSample != 32)
            throw new ArgumentException($"ISampleProvider must be 32-bit float; got {waveFormat.BitsPerSample} bits", nameof(waveFormat));

        if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            return waveFormat;

        if (waveFormat.Encoding == WaveFormatEncoding.Extensible)
        {
            if (waveFormat is WaveFormatExtensible ext && ext.SubFormat == IeeeFloatSubFormat)
                return waveFormat;
        }

        throw new ArgumentException($"ISampleProvider must be IEEE float (IeeeFloat or Extensible/IEEE_FLOAT); got {waveFormat.Encoding}", nameof(waveFormat));
    }
}
