using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MinecraftHapticEngine.Telemetry;

public sealed class UdpTelemetryListener : IDisposable
{
    private readonly int _port;
    private readonly Action<string> _onMessage;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runner;

    public UdpTelemetryListener(int port, Action<string> onMessage)
    {
        _port = port;
        _onMessage = onMessage;
    }

    public void Start()
    {
        if (_runner is not null) return;
        _runner = Task.Run(RunAsync);
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_runner is not null)
        {
            try { await _runner.ConfigureAwait(false); } catch { }
        }
    }

    private async Task RunAsync()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, _port));
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                var msg = Encoding.UTF8.GetString(result.Buffer);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    _onMessage(msg);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    public void Dispose()
    {
        _ = StopAsync();
        _cts.Dispose();
    }
}
