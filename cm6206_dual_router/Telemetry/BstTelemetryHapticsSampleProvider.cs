using System.Collections.Generic;
using System.Text.Json;
using NAudio.Wave;

namespace Cm6206DualRouter.Telemetry;

internal sealed class BstTelemetryHapticsSampleProvider : ISampleProvider, IDisposable
{
    private readonly RouterConfig _config;
    private readonly WaveFormat _format;

    private readonly object _lock = new();

    private readonly BstWebSocketClient _ws;

    // Continuous telemetry state
    private float _targetRumble01;
    private float _currentRumble01;
    private bool _elytra;

    // One-shots
    private readonly List<OneShot> _shots = new();

    // Oscillator
    private double _phase;

    private readonly float _telemetryGain;

    private long _lastMessageTicks;

    private sealed class OneShot
    {
        public int RemainingSamples;
        public float Gain;
        public float F0;
        public float F1;
        public float Noise;
        public int TotalSamples;
        public double Phase;
        public string Pattern = "";
        public int PulsePeriodSamples;
        public int PulseWidthSamples;
        public int DelaySamples;
        public int AgeSamples;
        public uint NoiseState;
    }

    public BstTelemetryHapticsSampleProvider(RouterConfig config)
    {
        _config = config;
        _format = WaveFormat.CreateIeeeFloatWaveFormat(config.SampleRate, 2);
        _telemetryGain = DbToGain(config.TelemetryGainDb);

        var host = (config.TelemetryWebSocketHost ?? "127.0.0.1").Trim();
        var port = config.TelemetryWebSocketPort;
        var uri = new Uri($"ws://{host}:{port}/");

        _ws = new BstWebSocketClient(uri, OnJsonMessage);
        _ws.Start();
    }

    public WaveFormat WaveFormat => _format;

    public bool IsReceivingRecently
    {
        get
        {
            var age = Environment.TickCount64 - Interlocked.Read(ref _lastMessageTicks);
            return age >= 0 && age < 1500;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        if (frames <= 0)
            return 0;

        // If telemetry isn't coming in, keep output quiet.
        // (This prevents surprise rumble if the server isn't running.)
        var receiving = IsReceivingRecently;

        var sampleRate = _format.SampleRate;

        // Smoothing: ~60ms time constant.
        var alpha = 1.0f - (float)Math.Exp(-1.0 / (0.06 * sampleRate));

        lock (_lock)
        {
            for (var i = 0; i < frames; i++)
            {
                _currentRumble01 += (_targetRumble01 - _currentRumble01) * alpha;

                var rumbleGain = receiving ? _currentRumble01 : 0f;

                // Base rumble frequency: 25..55 Hz depending on gain.
                var hz = 25.0 + 30.0 * Math.Clamp(rumbleGain, 0f, 1f);

                _phase += (2.0 * Math.PI * hz) / sampleRate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

                var rumble = (float)Math.Sin(_phase) * rumbleGain;

                // Elytra adds a little broadband texture.
                if (_elytra && rumbleGain > 0.001f)
                {
                    var n = (NextNoise(ref _shotsNoiseState) * 2f - 1f) * 0.08f;
                    rumble += n * rumbleGain;
                }

                float oneShot = 0f;
                if (_shots.Count > 0)
                {
                    for (var s = _shots.Count - 1; s >= 0; s--)
                    {
                        var sh = _shots[s];

                        sh.AgeSamples++;
                        if (sh.DelaySamples > 0)
                        {
                            sh.DelaySamples--;
                            continue;
                        }

                        if (sh.RemainingSamples <= 0)
                        {
                            _shots.RemoveAt(s);
                            continue;
                        }

                        var t01 = 1f - (float)sh.RemainingSamples / Math.Max(1, sh.TotalSamples);

                        // Simple exp-ish decay envelope.
                        var env = (float)Math.Exp(-4.0f * t01);

                        // Optional pulse gating.
                        if (sh.PulsePeriodSamples > 0 && sh.PulseWidthSamples > 0)
                        {
                            var ph = sh.AgeSamples % sh.PulsePeriodSamples;
                            if (ph >= sh.PulseWidthSamples)
                                env = 0f;
                        }

                        // Frequency sweep.
                        var f = sh.F0 + (sh.F1 - sh.F0) * t01;
                        sh.NoiseState = sh.NoiseState == 0 ? 0x12345678u : sh.NoiseState;
                        sh.Phase += (2.0 * Math.PI * f) / sampleRate;
                        if (sh.Phase > 2.0 * Math.PI) sh.Phase -= 2.0 * Math.PI;
                        var osc = (float)Math.Sin(sh.Phase);

                        var noise = (NextNoise(ref sh.NoiseState) * 2f - 1f);

                        var sample = osc * (1f - sh.Noise) + noise * sh.Noise;

                        oneShot += sample * sh.Gain * env;

                        sh.RemainingSamples--;
                    }
                }

                var outSample = (rumble + oneShot) * _telemetryGain;
                buffer[offset + i * 2] = outSample;
                buffer[offset + i * 2 + 1] = outSample;
            }
        }

        return frames * 2;
    }

    // Workaround: keep a separate noise state for the continuous generator.
    private uint _shotsNoiseState = 0xA1B2C3D4u;

    public void Dispose()
    {
        _ws.Dispose();
    }

    private void OnJsonMessage(string json)
    {
        Interlocked.Exchange(ref _lastMessageTicks, Environment.TickCount64);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch
        {
            return;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("type", out var typeEl))
                return;

            var type = typeEl.GetString() ?? "";
            if (type.Length == 0)
                return;

            if (type.Equals("telemetry", StringComparison.OrdinalIgnoreCase))
            {
                if (!_config.TelemetryConsumeTelemetry)
                    return;

                var speed = TryGetDouble(doc.RootElement, "speed");
                var accel = TryGetDouble(doc.RootElement, "accel");
                var elytra = TryGetBool(doc.RootElement, "elytra");

                // Very simple mapping: speed controls rumble, accel spikes add a bit.
                var rumble = (float)Math.Clamp(speed * 0.20, 0.0, 1.0);
                if (Math.Abs(accel) > 0.05)
                    rumble = Math.Clamp(rumble + (float)(Math.Min(1.0, Math.Abs(accel)) * 0.35), 0f, 1f);

                lock (_lock)
                {
                    _targetRumble01 = rumble;
                    _elytra = elytra;
                }

                return;
            }

            if (type.Equals("event", StringComparison.OrdinalIgnoreCase))
            {
                if (!_config.TelemetryConsumeUnifiedEvents)
                    return;

                var kind = TryGetString(doc.RootElement, "kind");
                var intensity = (float)Math.Clamp(TryGetDouble(doc.RootElement, "intensity"), 0.0, 1.0);

                // Map kind to a plausible thump band.
                var f0 = kind switch
                {
                    "danger" => 28f,
                    "continuous" => 22f,
                    "ui" => 70f,
                    "environmental" => 40f,
                    "modded" => 45f,
                    _ => 45f // impact
                };

                AddOneShot(new OneShot
                {
                    TotalSamples = (int)(_format.SampleRate * 0.10),
                    RemainingSamples = (int)(_format.SampleRate * 0.10),
                    Gain = intensity,
                    F0 = f0,
                    F1 = f0,
                    Noise = 0.10f,
                    Pattern = "",
                    PulsePeriodSamples = 0,
                    PulseWidthSamples = 0,
                    DelaySamples = 0,
                    AgeSamples = 0,
                    NoiseState = 0xC0FFEE01u
                });

                return;
            }

            if (type.Equals("haptic", StringComparison.OrdinalIgnoreCase))
            {
                if (!_config.TelemetryConsumeHapticCommands)
                    return;

                var f0 = (float)Math.Clamp(TryGetDouble(doc.RootElement, "f0"), 5.0, 160.0);
                var f1 = (float)Math.Clamp(TryGetDouble(doc.RootElement, "f1"), 5.0, 160.0);
                var ms = (int)Math.Clamp(TryGetDouble(doc.RootElement, "ms"), 0, 5000);
                var gain = (float)Math.Clamp(TryGetDouble(doc.RootElement, "gain"), 0.0, 2.0);
                var noise = (float)Math.Clamp(TryGetDouble(doc.RootElement, "noise"), 0.0, 1.0);
                var pattern = TryGetString(doc.RootElement, "pattern");
                var pulsePeriodMs = (int)Math.Clamp(TryGetDouble(doc.RootElement, "pulsePeriodMs"), 0, 5000);
                var pulseWidthMs = (int)Math.Clamp(TryGetDouble(doc.RootElement, "pulseWidthMs"), 0, 5000);
                var delayMs = (int)Math.Clamp(TryGetDouble(doc.RootElement, "delayMs"), 0, 5000);

                var totalSamples = (int)(_format.SampleRate * (ms / 1000.0));
                if (totalSamples <= 0)
                    return;

                var pulsePeriodSamples = pulsePeriodMs <= 0 ? 0 : (int)(_format.SampleRate * (pulsePeriodMs / 1000.0));
                var pulseWidthSamples = pulseWidthMs <= 0 ? 0 : (int)(_format.SampleRate * (pulseWidthMs / 1000.0));
                var delaySamples = delayMs <= 0 ? 0 : (int)(_format.SampleRate * (delayMs / 1000.0));

                AddOneShot(new OneShot
                {
                    TotalSamples = totalSamples,
                    RemainingSamples = totalSamples,
                    Gain = gain,
                    F0 = f0,
                    F1 = f1,
                    Noise = noise,
                    Pattern = pattern,
                    PulsePeriodSamples = pulsePeriodSamples,
                    PulseWidthSamples = pulseWidthSamples,
                    DelaySamples = delaySamples,
                    AgeSamples = 0,
                    NoiseState = 0xBADC0DEu
                });
            }
        }
    }

    private void AddOneShot(OneShot shot)
    {
        lock (_lock)
        {
            // Keep the list bounded; drop oldest if flooded.
            if (_shots.Count >= 64)
                _shots.RemoveAt(0);
            _shots.Add(shot);
        }
    }

    private static double TryGetDouble(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return 0.0;

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetDouble(out var d) ? d : 0.0,
            JsonValueKind.String => double.TryParse(el.GetString(), out var d) ? d : 0.0,
            JsonValueKind.True => 1.0,
            JsonValueKind.False => 0.0,
            _ => 0.0
        };
    }

    private static bool TryGetBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return false;

        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => el.TryGetDouble(out var d) && d != 0.0,
            JsonValueKind.String => bool.TryParse(el.GetString(), out var b) && b,
            _ => false
        };
    }

    private static string TryGetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return "";
        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.ToString();
    }

    private static float DbToGain(float db)
    {
        // Same convention used elsewhere in the router.
        return (float)Math.Pow(10.0, db / 20.0);
    }

    private static float NextNoise(ref uint state)
    {
        // xorshift32
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        // 0..1
        return (state & 0x00FFFFFF) / (float)0x01000000;
    }
}
