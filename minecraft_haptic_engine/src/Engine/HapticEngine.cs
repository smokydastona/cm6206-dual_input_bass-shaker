using System.Collections.Concurrent;
using MinecraftHapticEngine.Audio;
using MinecraftHapticEngine.Config;
using MinecraftHapticEngine.Telemetry;

namespace MinecraftHapticEngine.Engine;

public sealed class HapticEngine : IDisposable
{
    private readonly HapticEngineConfig _config;
    private readonly Dictionary<string, BusEngine> _buses;
    private readonly ConcurrentQueue<TelemetryPacket> _incoming = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private WebSocketTelemetryClient? _ws;
    private UdpTelemetryListener? _udp;
    private Task? _pumpTask;

    private float _speed;
    private float _accel;
    private bool _elytra;

    public HapticEngine(HapticEngineConfig config)
    {
        _config = config;
        _buses = config.Buses.ToDictionary(
            kvp => kvp.Key,
            kvp => new BusEngine(kvp.Key, kvp.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in _config.Mappings)
        {
            if (mapping.Mode.Equals("continuous", StringComparison.OrdinalIgnoreCase))
            {
                if (!_buses.TryGetValue(mapping.Bus, out var bus))
                {
                    throw new InvalidOperationException($"Unknown bus '{mapping.Bus}' for mapping '{mapping.Name}'.");
                }

                bus.RegisterContinuous(mapping);
            }
        }
    }

    public void Start()
    {
        foreach (var bus in _buses.Values)
        {
            bus.StartOutput();
        }

        void OnText(string text)
        {
            if (TelemetryParser.TryParse(text, out var pkt))
            {
                if (_incoming.Count < _config.Telemetry.ReceiveQueueLimit)
                {
                    _incoming.Enqueue(pkt);
                }
            }
        }

        if (_config.Telemetry.EnableWebSocket)
        {
            _ws = new WebSocketTelemetryClient(_config.Telemetry.WebSocketUrl, _config.Telemetry.ReconnectDelayMs, OnText);
            _ws.Start();
        }

        if (_config.Telemetry.EnableUdp)
        {
            _udp = new UdpTelemetryListener(_config.Telemetry.UdpPort, OnText);
            _udp.Start();
        }

        _pumpTask = Task.Run(PumpLoop);
    }

    public void StartCalibration(string busName)
    {
        if (!_buses.TryGetValue(busName, out var bus))
        {
            throw new InvalidOperationException($"Unknown bus '{busName}'.");
        }

        bus.StartCalibrationClicks();
    }

    public void Stop()
    {
        _cts.Cancel();
        foreach (var bus in _buses.Values)
        {
            bus.Stop();
        }
    }

    public Task WaitForStopAsync() => _stopped.Task;

    private async Task PumpLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                while (_incoming.TryDequeue(out var pkt))
                {
                    HandlePacket(pkt);
                }

                foreach (var bus in _buses.Values)
                {
                    bus.UpdateContinuous(_speed, _accel, _elytra);
                }

                await Task.Delay(5, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            try
            {
                if (_ws is not null) await _ws.StopAsync().ConfigureAwait(false);
                if (_udp is not null) await _udp.StopAsync().ConfigureAwait(false);
            }
            catch { }
            _stopped.TrySetResult();
        }
    }

    private void HandlePacket(TelemetryPacket pkt)
    {
        if (pkt.Type.Equals("telemetry", StringComparison.OrdinalIgnoreCase))
        {
            if (pkt.Speed is not null) _speed = pkt.Speed.Value;
            if (pkt.Accel is not null) _accel = pkt.Accel.Value;
            if (pkt.Elytra is not null) _elytra = pkt.Elytra.Value;
            return;
        }

        // Match + trigger oneshots
        foreach (var mapping in _config.Mappings)
        {
            if (!mapping.Mode.Equals("oneshot", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Matches(mapping.Match, pkt))
            {
                continue;
            }

            if (_buses.TryGetValue(mapping.Bus, out var bus))
            {
                bus.TriggerOneShot(mapping);
            }
        }
    }

    private static bool Matches(MatchRule rule, TelemetryPacket pkt)
    {
        if (!string.IsNullOrWhiteSpace(rule.Type) && !pkt.Type.Equals(rule.Type, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(rule.Id) && !string.Equals(pkt.Id, rule.Id, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(rule.Kind) && !string.Equals(pkt.Kind, rule.Kind, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        _ws?.Dispose();
        _udp?.Dispose();
        foreach (var bus in _buses.Values) bus.Dispose();
    }
}
