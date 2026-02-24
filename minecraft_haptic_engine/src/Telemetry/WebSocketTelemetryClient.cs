using System.Net.WebSockets;
using System.Text;

namespace MinecraftHapticEngine.Telemetry;

public sealed class WebSocketTelemetryClient : IDisposable
{
    private readonly Uri _uri;
    private readonly int _reconnectDelayMs;
    private readonly Action<string> _onMessage;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runner;

    public WebSocketTelemetryClient(string url, int reconnectDelayMs, Action<string> onMessage)
    {
        _uri = new Uri(url);
        _reconnectDelayMs = reconnectDelayMs;
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
        while (!_cts.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(_uri, _cts.Token).ConfigureAwait(false);

                var buffer = new byte[64 * 1024];
                var segment = new ArraySegment<byte>(buffer);
                while (ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(segment, _cts.Token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None).ConfigureAwait(false);
                            break;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    var msg = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        _onMessage(msg);
                    }
                }
            }
            catch
            {
                // ignore and reconnect
            }

            try { await Task.Delay(_reconnectDelayMs, _cts.Token).ConfigureAwait(false); } catch { }
        }
    }

    public void Dispose()
    {
        _ = StopAsync();
        _cts.Dispose();
    }
}
