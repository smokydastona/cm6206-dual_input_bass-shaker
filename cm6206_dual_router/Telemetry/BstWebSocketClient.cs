using System.Net.WebSockets;
using System.Text;

namespace Cm6206DualRouter.Telemetry;

internal sealed class BstWebSocketClient : IDisposable
{
    private readonly Uri _uri;
    private readonly Action<string> _onMessage;

    private readonly CancellationTokenSource _cts = new();
    private Task? _task;

    public BstWebSocketClient(Uri uri, Action<string> onMessage)
    {
        _uri = uri;
        _onMessage = onMessage;
    }

    public void Start()
    {
        _task ??= Task.Run(RunAsync);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _task?.Wait(750); } catch { /* ignore */ }
        _cts.Dispose();
    }

    private async Task RunAsync()
    {
        // Simple reconnect loop. This is intentionally forgiving: the mod may start/stop its server.
        while (!_cts.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(_uri, _cts.Token).ConfigureAwait(false);
                await ReceiveLoopAsync(ws).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // swallow; we'll retry
            }

            try
            {
                await Task.Delay(500, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws)
    {
        var buf = new byte[16 * 1024];
        var sb = new StringBuilder(16 * 1024);

        while (!_cts.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult r;
            try
            {
                r = await ws.ReceiveAsync(buf, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (r.MessageType == WebSocketMessageType.Close)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false); }
                catch { /* ignore */ }
                return;
            }

            if (r.MessageType != WebSocketMessageType.Text)
            {
                // Server only sends text frames.
                continue;
            }

            sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
            if (!r.EndOfMessage)
                continue;

            var msg = sb.ToString();
            sb.Clear();

            if (!string.IsNullOrWhiteSpace(msg))
            {
                try { _onMessage(msg); } catch { /* ignore user callback */ }
            }
        }
    }
}
