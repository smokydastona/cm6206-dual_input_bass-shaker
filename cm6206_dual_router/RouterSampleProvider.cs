using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cm6206DualRouter;

/// <summary>
/// Mixes two stereo inputs into 7.1 float output.
/// Output channel order (WAVEFORMATEXTENSIBLE): FL, FR, FC, LFE, BL, BR, SL, SR
/// </summary>
public sealed class RouterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _musicStereo;
    private readonly ISampleProvider _shakerStereo;
    private readonly RouterConfig _config;

    private readonly int _sampleRate;

    private readonly float _musicGain;
    private readonly float _shakerGain;
    private readonly float _lfeGain;
    private readonly float _rearGain;
    private readonly float _sideGain;

    private readonly BiQuadFilter _shakerHpL;
    private readonly BiQuadFilter _shakerHpR;
    private readonly BiQuadFilter _shakerLpL;
    private readonly BiQuadFilter _shakerLpR;

    private readonly BiQuadFilter? _musicHpL;
    private readonly BiQuadFilter? _musicHpR;
    private readonly BiQuadFilter? _musicLpL;
    private readonly BiQuadFilter? _musicLpR;

    public RouterSampleProvider(
        ISampleProvider musicStereo,
        ISampleProvider shakerStereo,
        WaveFormat outputFormat,
        RouterConfig config)
    {
        if (musicStereo.WaveFormat.Channels != 2) throw new ArgumentException("musicStereo must be stereo");
        if (shakerStereo.WaveFormat.Channels != 2) throw new ArgumentException("shakerStereo must be stereo");

        _musicStereo = musicStereo;
        _shakerStereo = shakerStereo;
        WaveFormat = outputFormat;
        _config = config;

        _sampleRate = outputFormat.SampleRate;

        _musicGain = DbToGain(config.MusicGainDb);
        _shakerGain = DbToGain(config.ShakerGainDb);
        _lfeGain = DbToGain(config.LfeGainDb);
        _rearGain = DbToGain(config.RearGainDb);
        _sideGain = DbToGain(config.SideGainDb);

        _shakerHpL = BiQuadFilter.HighPassFilter(_sampleRate, config.ShakerHighPassHz, 0.707f);
        _shakerHpR = BiQuadFilter.HighPassFilter(_sampleRate, config.ShakerHighPassHz, 0.707f);
        _shakerLpL = BiQuadFilter.LowPassFilter(_sampleRate, config.ShakerLowPassHz, 0.707f);
        _shakerLpR = BiQuadFilter.LowPassFilter(_sampleRate, config.ShakerLowPassHz, 0.707f);

        if (config.MusicHighPassHz is not null)
        {
            _musicHpL = BiQuadFilter.HighPassFilter(_sampleRate, config.MusicHighPassHz.Value, 0.707f);
            _musicHpR = BiQuadFilter.HighPassFilter(_sampleRate, config.MusicHighPassHz.Value, 0.707f);
        }

        if (config.MusicLowPassHz is not null)
        {
            _musicLpL = BiQuadFilter.LowPassFilter(_sampleRate, config.MusicLowPassHz.Value, 0.707f);
            _musicLpR = BiQuadFilter.LowPassFilter(_sampleRate, config.MusicLowPassHz.Value, 0.707f);
        }
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var outChannels = WaveFormat.Channels;
        var framesRequested = count / outChannels;
        if (framesRequested <= 0)
            return 0;

        var musicTmp = ArrayPool<float>.Shared.Rent(framesRequested * 2);
        var shakerTmp = ArrayPool<float>.Shared.Rent(framesRequested * 2);

        try
        {
            var musicRead = _musicStereo.Read(musicTmp, 0, framesRequested * 2);
            var shakerRead = _shakerStereo.Read(shakerTmp, 0, framesRequested * 2);

            var musicFrames = musicRead / 2;
            var shakerFrames = shakerRead / 2;

            for (var frame = 0; frame < framesRequested; frame++)
            {
                var mL = frame < musicFrames ? musicTmp[frame * 2] : 0f;
                var mR = frame < musicFrames ? musicTmp[frame * 2 + 1] : 0f;

                var sL = frame < shakerFrames ? shakerTmp[frame * 2] : 0f;
                var sR = frame < shakerFrames ? shakerTmp[frame * 2 + 1] : 0f;

                // Apply gains
                mL *= _musicGain;
                mR *= _musicGain;

                sL *= _shakerGain;
                sR *= _shakerGain;

                // Optional music filtering
                if (_musicHpL is not null)
                {
                    mL = _musicHpL.Transform(mL);
                    mR = _musicHpR!.Transform(mR);
                }
                if (_musicLpL is not null)
                {
                    mL = _musicLpL.Transform(mL);
                    mR = _musicLpR!.Transform(mR);
                }

                // Shaker filtering (HP + LP)
                sL = _shakerHpL.Transform(sL);
                sR = _shakerHpR.Transform(sR);
                sL = _shakerLpL.Transform(sL);
                sR = _shakerLpR.Transform(sR);

                // Mix strategy:
                // - Front channels carry BOTH streams (so you can still keep separate level control, but one physical output)
                // - Rear/Side channels carry shaker (distributed)
                // - LFE gets mono sum of shaker (+ optionally music if you set musicLowPassHz)
                var frontL = mL + sL;
                var frontR = mR + sR;

                var lfe = (sL + sR) * 0.5f * _lfeGain;

                var backL = sL * _rearGain;
                var backR = sR * _rearGain;

                var sideL = sL * _sideGain;
                var sideR = sR * _sideGain;

                var center = 0f;
                if (_config.UseCenterChannel)
                {
                    // Light mono feed (optional). If you don’t wire anything to center, leave disabled.
                    center = (sL + sR) * 0.5f;
                }

                var outBase = offset + (frame * outChannels);

                // FL, FR, FC, LFE, BL, BR, SL, SR
                buffer[outBase + 0] = Clamp(frontL);
                buffer[outBase + 1] = Clamp(frontR);
                buffer[outBase + 2] = Clamp(center);
                buffer[outBase + 3] = Clamp(lfe);
                buffer[outBase + 4] = Clamp(backL);
                buffer[outBase + 5] = Clamp(backR);
                buffer[outBase + 6] = Clamp(sideL);
                buffer[outBase + 7] = Clamp(sideR);
            }

            return framesRequested * outChannels;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(musicTmp);
            ArrayPool<float>.Shared.Return(shakerTmp);
        }
    }

    private static float DbToGain(float db) => (float)Math.Pow(10.0, db / 20.0);

    private static float Clamp(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x;
    }
}
