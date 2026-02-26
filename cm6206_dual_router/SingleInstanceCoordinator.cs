using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace Cm6206DualRouter;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;

    private SingleInstanceCoordinator(Mutex mutex, string pipeName)
    {
        _mutex = mutex;
        _pipeName = pipeName;
    }

    public static bool TryCreate(string appId, out SingleInstanceCoordinator? coordinator)
    {
        coordinator = null;

        var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value ?? Environment.UserName;

        var mutexName = $"Local\\{appId}:{sid}";
        var pipeName = $"{appId}:{sid}";

        var mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            try { mutex.Dispose(); } catch { /* ignore */ }
            return false;
        }

        coordinator = new SingleInstanceCoordinator(mutex, pipeName);
        return true;
    }

    public async Task<bool> TrySignalPrimaryActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _pipeName,
                direction: PipeDirection.Out,
                options: PipeOptions.Asynchronous);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromMilliseconds(350));

            await client.ConnectAsync(linked.Token).ConfigureAwait(false);

            var payload = Encoding.UTF8.GetBytes("activate\n");
            await client.WriteAsync(payload, linked.Token).ConfigureAwait(false);
            await client.FlushAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void StartActivationServer(Action onActivate)
    {
        if (_serverTask is not null)
            return;

        _serverTask = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        pipeName: _pipeName,
                        direction: PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                    // We don't need to parse message contents today; any connection == activate.
                    try { onActivate(); } catch { /* ignore */ }

                    // Drain quickly (best-effort) so the client can exit cleanly.
                    var buffer = new byte[32];
                    _ = await server.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Keep the server alive even if a client misbehaves.
                    await Task.Delay(250).ConfigureAwait(false);
                }
            }
        });
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { /* ignore */ }
        try { _serverTask?.Wait(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
        try { _cts.Dispose(); } catch { /* ignore */ }
        try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
        try { _mutex.Dispose(); } catch { /* ignore */ }
    }
}
