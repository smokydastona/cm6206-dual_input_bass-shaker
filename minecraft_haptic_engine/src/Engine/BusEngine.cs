using MinecraftHapticEngine.Audio;
using MinecraftHapticEngine.Config;
using MinecraftHapticEngine.Synthesis;

namespace MinecraftHapticEngine.Engine;

public sealed class BusEngine : IDisposable
{
    private readonly string _name;
    private readonly BusConfig _config;
    private readonly object _gate = new();

    private readonly List<ContinuousEffect> _continuous = new();
    private readonly EffectMixer _mixer;
    private WasapiBusOutput? _output;
    private CancellationTokenSource? _calibrationCts;

    public BusEngine(string name, BusConfig config)
    {
        _name = name;
        _config = config;
        _mixer = new EffectMixer(config.SampleRate, config.Channels, config.GainDb, config.LowPassHz, config.HighPassHz, config.DelayMs);
    }

    public void StartOutput()
    {
        _output = new WasapiBusOutput(_name, _config, _mixer);
        _output.Start();
    }

    public void RegisterContinuous(EffectMapping mapping)
    {
        var effect = ContinuousEffect.Create(mapping, _config.SampleRate, _config.Channels);
        lock (_gate)
        {
            _continuous.Add(effect);
            _mixer.AddContinuous(effect);
        }
    }

    public void UpdateContinuous(float speed, float accel, bool elytra)
    {
        lock (_gate)
        {
            foreach (var c in _continuous)
            {
                c.UpdateFromTelemetry(speed, accel, elytra);
            }
        }
    }

    public void TriggerOneShot(EffectMapping mapping)
    {
        var inst = OneShotEffect.Create(mapping, _config.SampleRate, _config.Channels);
        _mixer.AddOneShot(inst);
    }

    public void StartCalibrationClicks(int intervalMs = 800)
    {
        _calibrationCts?.Cancel();
        _calibrationCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!_calibrationCts.IsCancellationRequested)
            {
                var clickMapping = new EffectMapping(
                    Name: "calibration_click",
                    Bus: _name,
                    Match: new MatchRule(Type: "event", Id: "calibration"),
                    Mode: "oneshot",
                    GainDb: -3,
                    Route: new RouteConfig(Preset: "all", Weights: null),
                    Generator: new GeneratorConfig(Type: "impulse", FrequencyHz: 0, DurationMs: 20),
                    Envelope: new EnvelopeConfig(AttackMs: 0, HoldMs: 0, ReleaseMs: 30),
                    Filter: null,
                    Continuous: null);

                TriggerOneShot(clickMapping);

                try { await Task.Delay(intervalMs, _calibrationCts.Token).ConfigureAwait(false); } catch { }
            }
        });
    }

    public void Stop()
    {
        _calibrationCts?.Cancel();
        _output?.Stop();
    }

    public void Dispose()
    {
        _calibrationCts?.Cancel();
        _calibrationCts?.Dispose();
        _output?.Dispose();
    }
}
