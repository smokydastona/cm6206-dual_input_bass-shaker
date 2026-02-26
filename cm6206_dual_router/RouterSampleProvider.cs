using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cm6206DualRouter;

/// <summary>
/// Mixes a (possibly multi-channel) Music input plus a stereo Shaker input into 7.1 float output.
/// Output channel order (WAVEFORMATEXTENSIBLE): FL, FR, FC, LFE, BL, BR, SL, SR
/// </summary>
public sealed class RouterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _music;
    private readonly ISampleProvider _shakerStereo;
    private readonly RouterConfig _config;

    private readonly int _sampleRate;

    private readonly float _musicGain;
    private readonly float _shakerGain;
    private readonly float _masterGain;
    private readonly float _lfeGain;
    private readonly float _rearGain;
    private readonly float _sideGain;

    private readonly float[] _channelGains;
    private readonly int[] _channelMap;
    private readonly bool[] _channelMute;
    private readonly bool[] _channelInvert;
    private readonly bool[] _channelSolo;
    private readonly bool _anySolo;

    private readonly BiQuadFilter _shakerHpL;
    private readonly BiQuadFilter _shakerHpR;
    private readonly BiQuadFilter _shakerLpL;
    private readonly BiQuadFilter _shakerLpR;

    private readonly BiQuadFilter[]? _musicHp;
    private readonly BiQuadFilter[]? _musicLp;

    private readonly int _musicChannels;

    // Priority switching state (read-only, computed on the audio thread).
    private bool _priorityUseMusic;
    private float _musicEnv;
    private float _shakerEnv;
    private readonly float _envDecay;

    public RouterSampleProvider(
        ISampleProvider music,
        ISampleProvider shakerStereo,
        WaveFormat outputFormat,
        RouterConfig config)
    {
        if (shakerStereo.WaveFormat.Channels != 2) throw new ArgumentException("shakerStereo must be stereo");

        if (music.WaveFormat.Channels < 1) throw new ArgumentException("music must have >= 1 channel", nameof(music));
        if (music.WaveFormat.Channels > 8) throw new ArgumentException("music must have <= 8 channels", nameof(music));

        _music = music;
        _shakerStereo = shakerStereo;
        WaveFormat = outputFormat;
        _config = config;

        _musicChannels = music.WaveFormat.Channels;

        _sampleRate = outputFormat.SampleRate;

        _musicGain = DbToGain(config.MusicGainDb);
        _shakerGain = DbToGain(config.ShakerGainDb);
        _masterGain = DbToGain(config.MasterGainDb);
        _lfeGain = DbToGain(config.LfeGainDb);
        _rearGain = DbToGain(config.RearGainDb);
        _sideGain = DbToGain(config.SideGainDb);

        _channelGains = new float[8];
        if (config.ChannelGainsDb is null)
        {
            for (var i = 0; i < 8; i++) _channelGains[i] = 1.0f;
        }
        else
        {
            for (var i = 0; i < 8; i++) _channelGains[i] = DbToGain(config.ChannelGainsDb[i]);
        }

        _channelMap = config.OutputChannelMap is null
            ? [0, 1, 2, 3, 4, 5, 6, 7]
            : config.OutputChannelMap.ToArray();

        _channelMute = config.ChannelMute is null
            ? [false, false, false, false, false, false, false, false]
            : config.ChannelMute.ToArray();

        _channelInvert = config.ChannelInvert is null
            ? [false, false, false, false, false, false, false, false]
            : config.ChannelInvert.ToArray();

        _channelSolo = config.ChannelSolo is null
            ? [false, false, false, false, false, false, false, false]
            : config.ChannelSolo.ToArray();

        _anySolo = _channelSolo.Any(x => x);

        _shakerHpL = BiQuadFilter.HighPassFilter(_sampleRate, config.ShakerHighPassHz, 0.707f);
        _shakerHpR = BiQuadFilter.HighPassFilter(_sampleRate, config.ShakerHighPassHz, 0.707f);
        _shakerLpL = BiQuadFilter.LowPassFilter(_sampleRate, config.ShakerLowPassHz, 0.707f);
        _shakerLpR = BiQuadFilter.LowPassFilter(_sampleRate, config.ShakerLowPassHz, 0.707f);

        if (config.MusicHighPassHz is not null)
        {
            _musicHp = new BiQuadFilter[_musicChannels];
            for (var i = 0; i < _musicChannels; i++)
                _musicHp[i] = BiQuadFilter.HighPassFilter(_sampleRate, config.MusicHighPassHz.Value, 0.707f);
        }

        if (config.MusicLowPassHz is not null)
        {
            _musicLp = new BiQuadFilter[_musicChannels];
            for (var i = 0; i < _musicChannels; i++)
                _musicLp[i] = BiQuadFilter.LowPassFilter(_sampleRate, config.MusicLowPassHz.Value, 0.707f);
        }

        var mode = (config.MixingMode ?? "FrontBoth").Trim();
        _priorityUseMusic = mode.Equals("PriorityMusic", StringComparison.OrdinalIgnoreCase);
        _musicEnv = 0f;
        _shakerEnv = 0f;
        // Peak envelope decay (~150ms time constant)
        var tauSec = 0.15f;
        _envDecay = (float)Math.Exp(-1.0 / (tauSec * _sampleRate));
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var outChannels = WaveFormat.Channels;
        var framesRequested = count / outChannels;
        if (framesRequested <= 0)
            return 0;

        var musicTmp = ArrayPool<float>.Shared.Rent(framesRequested * _musicChannels);
        var shakerTmp = ArrayPool<float>.Shared.Rent(framesRequested * 2);

        try
        {
            // CA2014: avoid stackalloc inside the per-frame loop.
            Span<float> raw = stackalloc float[8];

            var musicRead = _music.Read(musicTmp, 0, framesRequested * _musicChannels);
            var shakerRead = _shakerStereo.Read(shakerTmp, 0, framesRequested * 2);

            var musicFrames = musicRead / _musicChannels;
            var shakerFrames = shakerRead / 2;

            for (var frame = 0; frame < framesRequested; frame++)
            {
                var hasMusic = frame < musicFrames;

                // Music base channels (up to 7.1) are passed through.
                // When the input is stereo, only FL/FR are non-zero.
                var mFL = hasMusic ? musicTmp[frame * _musicChannels] : 0f;
                var mFR = hasMusic && _musicChannels >= 2 ? musicTmp[frame * _musicChannels + 1] : mFL;
                var mFC = hasMusic && _musicChannels >= 3 ? musicTmp[frame * _musicChannels + 2] : 0f;
                var mLfe = hasMusic && _musicChannels >= 4 ? musicTmp[frame * _musicChannels + 3] : 0f;
                var mBL = hasMusic && _musicChannels >= 5 ? musicTmp[frame * _musicChannels + 4] : 0f;
                var mBR = hasMusic && _musicChannels >= 6 ? musicTmp[frame * _musicChannels + 5] : 0f;
                var mSL = hasMusic && _musicChannels >= 7 ? musicTmp[frame * _musicChannels + 6] : 0f;
                var mSR = hasMusic && _musicChannels >= 8 ? musicTmp[frame * _musicChannels + 7] : 0f;

                var sL = frame < shakerFrames ? shakerTmp[frame * 2] : 0f;
                var sR = frame < shakerFrames ? shakerTmp[frame * 2 + 1] : 0f;

                // Apply gains
                mFL *= _musicGain;
                mFR *= _musicGain;
                mFC *= _musicGain;
                mLfe *= _musicGain;
                mBL *= _musicGain;
                mBR *= _musicGain;
                mSL *= _musicGain;
                mSR *= _musicGain;

                sL *= _shakerGain;
                sR *= _shakerGain;

                // Optional music filtering
                if (_musicHp is not null)
                {
                    mFL = _musicHp[0].Transform(mFL);
                    if (_musicChannels >= 2) mFR = _musicHp[1].Transform(mFR);
                    if (_musicChannels >= 3) mFC = _musicHp[2].Transform(mFC);
                    if (_musicChannels >= 4) mLfe = _musicHp[3].Transform(mLfe);
                    if (_musicChannels >= 5) mBL = _musicHp[4].Transform(mBL);
                    if (_musicChannels >= 6) mBR = _musicHp[5].Transform(mBR);
                    if (_musicChannels >= 7) mSL = _musicHp[6].Transform(mSL);
                    if (_musicChannels >= 8) mSR = _musicHp[7].Transform(mSR);
                }
                if (_musicLp is not null)
                {
                    mFL = _musicLp[0].Transform(mFL);
                    if (_musicChannels >= 2) mFR = _musicLp[1].Transform(mFR);
                    if (_musicChannels >= 3) mFC = _musicLp[2].Transform(mFC);
                    if (_musicChannels >= 4) mLfe = _musicLp[3].Transform(mLfe);
                    if (_musicChannels >= 5) mBL = _musicLp[4].Transform(mBL);
                    if (_musicChannels >= 6) mBR = _musicLp[5].Transform(mBR);
                    if (_musicChannels >= 7) mSL = _musicLp[6].Transform(mSL);
                    if (_musicChannels >= 8) mSR = _musicLp[7].Transform(mSR);
                }

                // Shaker filtering (HP + LP)
                sL = _shakerHpL.Transform(sL);
                sR = _shakerHpR.Transform(sR);
                sL = _shakerLpL.Transform(sL);
                sR = _shakerLpR.Transform(sR);

                // Mix strategy:
                // - Front channels can carry BOTH streams or just Music (see mixingMode)
                // - Rear/Side channels carry shaker (distributed)
                // - LFE gets mono sum of shaker (+ optionally music if you set musicLowPassHz)
                // Base music layout is already 7.1 ordered when the endpoint is multi-channel.
                // If music is stereo, only FL/FR are populated.
                float frontL = mFL;
                float frontR = mFR;

                // If the music stream doesn't provide a center channel, optionally derive a light mono center from FL/FR.
                var musicCenter = mFC;
                if (_config.UseCenterChannel && _musicChannels < 3)
                    musicCenter = (mFL + mFR) * 0.5f;

                float center = musicCenter;

                // If the music stream doesn't provide LFE, we do not synthesize one by default.
                float lfe = mLfe;
                float backL = mBL;
                float backR = mBR;
                float sideL = mSL;
                float sideR = mSR;

                if (_config.GroupRouting is not null)
                {
                    // Explicit group routing override (6x2).
                    // Row order: Front, Center, LFE, Rear, Side, Reserved.
                    // Col order: A (Music), B (Shaker).
                    var gr = _config.GroupRouting;
                    bool R(int row, int col) => gr[row * 2 + col];

                    var frontA = R(0, 0);
                    var frontB = R(0, 1);
                    var centerA = R(1, 0);
                    var centerB = R(1, 1);
                    var lfeA = R(2, 0);
                    var lfeB = R(2, 1);
                    var rearA = R(3, 0);
                    var rearB = R(3, 1);
                    var sideA = R(4, 0);
                    var sideB = R(4, 1);

                    frontL = (frontA ? mFL : 0f) + (frontB ? sL : 0f);
                    frontR = (frontA ? mFR : 0f) + (frontB ? sR : 0f);

                    center = 0f;
                    if (_config.UseCenterChannel)
                    {
                        var monoA = _musicChannels >= 3 ? mFC : (mFL + mFR) * 0.5f;
                        var monoB = (sL + sR) * 0.5f;
                        center = (centerA ? monoA : 0f) + (centerB ? monoB : 0f);
                    }

                    var lfeMono = 0f;
                    if (lfeA) lfeMono += _musicChannels >= 4 ? mLfe : (mFL + mFR) * 0.5f;
                    if (lfeB) lfeMono += (sL + sR) * 0.5f;
                    lfe = lfeMono * _lfeGain;

                    backL = ((rearA ? mBL : 0f) + (rearB ? sL : 0f)) * _rearGain;
                    backR = ((rearA ? mBR : 0f) + (rearB ? sR : 0f)) * _rearGain;

                    sideL = ((sideA ? mSL : 0f) + (sideB ? sL : 0f)) * _sideGain;
                    sideR = ((sideA ? mSR : 0f) + (sideB ? sR : 0f)) * _sideGain;
                }
                else
                {
                    // Legacy behavior: mixingMode controls the mix strategy.
                    var mode = (_config.MixingMode ?? "FrontBoth").Trim();
                    var isDedicated = mode.Equals("Dedicated", StringComparison.OrdinalIgnoreCase);
                    var isMusicOnly = mode.Equals("MusicOnly", StringComparison.OrdinalIgnoreCase);
                    var isShakerOnly = mode.Equals("ShakerOnly", StringComparison.OrdinalIgnoreCase);

                    // Priority logic is based on the front channels.
                    var mL = mFL;
                    var mR = mFR;

                    var isPriorityMusic = mode.Equals("PriorityMusic", StringComparison.OrdinalIgnoreCase);
                    var isPriorityShaker = mode.Equals("PriorityShaker", StringComparison.OrdinalIgnoreCase);

                    if (isPriorityMusic || isPriorityShaker)
                    {
                        // Update simple peak envelopes.
                        var curMusic = Math.Max(Math.Abs(mL), Math.Abs(mR));
                        var curShaker = Math.Max(Math.Abs(sL), Math.Abs(sR));
                        _musicEnv = Math.Max(curMusic, _musicEnv * _envDecay);
                        _shakerEnv = Math.Max(curShaker, _shakerEnv * _envDecay);

                        // Hysteresis to avoid rapid flipping.
                        const float ratio = 1.5f; // ~3.5 dB
                        if (_priorityUseMusic)
                        {
                            if (_shakerEnv > _musicEnv * ratio)
                                _priorityUseMusic = false;
                        }
                        else
                        {
                            if (_musicEnv > _shakerEnv * ratio)
                                _priorityUseMusic = true;
                        }

                        // Bias when envelopes are close.
                        if (isPriorityMusic && _musicEnv >= _shakerEnv)
                            _priorityUseMusic = true;
                        if (isPriorityShaker && _shakerEnv >= _musicEnv)
                            _priorityUseMusic = false;

                        isMusicOnly = _priorityUseMusic;
                        isShakerOnly = !_priorityUseMusic;
                    }

                    if (isMusicOnly) { sL = 0f; sR = 0f; }
                    if (isShakerOnly)
                    {
                        // Shaker-only should suppress ALL music channels.
                        frontL = 0f;
                        frontR = 0f;
                        center = 0f;
                        lfe = 0f;
                        backL = 0f;
                        backR = 0f;
                        sideL = 0f;
                        sideR = 0f;
                    }

                    // Fronts: dedicated = keep music fronts only; otherwise mix shaker into fronts.
                    if (!isShakerOnly)
                    {
                        frontL = isDedicated ? mFL : (mFL + sL);
                        frontR = isDedicated ? mFR : (mFR + sR);
                    }

                    // If shaker-only, FrontBoth behavior should still feed shaker to the fronts.
                    if (isShakerOnly)
                    {
                        frontL = sL;
                        frontR = sR;
                    }

                    // Shaker distribution is always stereo-based.
                    var shakerMono = (sL + sR) * 0.5f;

                    // Only add shaker to other channels if not MusicOnly.
                    if (!isMusicOnly)
                    {
                        lfe = (isShakerOnly ? 0f : lfe) + (shakerMono * _lfeGain);
                        backL = (isShakerOnly ? 0f : backL) + (sL * _rearGain);
                        backR = (isShakerOnly ? 0f : backR) + (sR * _rearGain);
                        sideL = (isShakerOnly ? 0f : sideL) + (sL * _sideGain);
                        sideR = (isShakerOnly ? 0f : sideR) + (sR * _sideGain);

                        if (_config.UseCenterChannel)
                        {
                            // Optional mono feed to center (in addition to any music center channel).
                            center = (isShakerOnly ? 0f : center) + shakerMono;
                        }
                    }
                }

                var outBase = offset + (frame * outChannels);

                // Source order (raw): FL, FR, FC, LFE, BL, BR, SL, SR
                raw[0] = frontL;
                raw[1] = frontR;
                raw[2] = center;
                raw[3] = lfe;
                raw[4] = backL;
                raw[5] = backR;
                raw[6] = sideL;
                raw[7] = sideR;

                for (var outCh = 0; outCh < 8; outCh++)
                {
                    var srcCh = _channelMap[outCh];
                    var sample = raw[srcCh];

                    if (_anySolo && !_channelSolo[outCh]) sample = 0f;
                    if (_channelInvert[outCh]) sample = -sample;
                    if (_channelMute[outCh]) sample = 0f;

                    sample *= _channelGains[outCh];
                    sample *= _masterGain;
                    buffer[outBase + outCh] = Clamp(sample);
                }
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
