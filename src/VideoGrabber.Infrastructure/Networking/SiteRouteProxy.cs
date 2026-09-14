using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VideoGrabber.Infrastructure.Diagnostics;
namespace VideoGrabber.Infrastructure.Networking;

// Loopback-only SOCKS5 CONNECT relay. TLS bytes are copied, never decrypted.
public sealed class SiteRouteProxy : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<TcpClient, byte> _clients = new();
    private readonly ConcurrentDictionary<Stream, byte> _upstreams = new();
    private readonly SemaphoreSlim _slots = new(32, 32);
    private readonly Func<string, int, CancellationToken, Task<Stream>> _connect;
    private int _disposed;
    public int Port { get; }
    public string ProxyUrl => "socks5://127.0.0.1:" + Port;
    public SiteRouteProxy(Func<string, int, CancellationToken, Task<Stream>> connect)
    {
        _connect = connect;
        _listener.Start(32);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptAsync();
    }
    private async Task AcceptAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
                if (!_slots.Wait(0)) { client.Dispose(); continue; }
                _clients.TryAdd(client, 0);
                _ = HandleAsync(client);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            if (!_stopping.IsCancellationRequested) DiagnosticHub.Log.Write("network.proxy", "failed", ex.GetType().Name);
        }
    }
    private async Task HandleAsync(TcpClient client)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        var stream = client.GetStream();
        var connected = false;
        Stream? upstream = null;
        string? targetHost = null;
        var targetPort = 0;
        try
        {
            var hello = await ReadAsync(stream, 2, deadline.Token).ConfigureAwait(false);
            if (hello[0] != 5 || hello[1] is 0 or > 16) return;
            var methods = await ReadAsync(stream, hello[1], deadline.Token).ConfigureAwait(false);
            if (!methods.Contains((byte)0)) { await stream.WriteAsync(new byte[] { 5, 255 }, deadline.Token).ConfigureAwait(false); return; }
            await stream.WriteAsync(new byte[] { 5, 0 }, deadline.Token).ConfigureAwait(false);
            var request = await ReadAsync(stream, 4, deadline.Token).ConfigureAwait(false);
            if (request[0] != 5 || request[1] != 1 || request[2] != 0) throw new ArgumentException("CONNECT required.");
            string host;
            if (request[3] == 3)
            {
                var size = (await ReadAsync(stream, 1, deadline.Token).ConfigureAwait(false))[0];
                if (size == 0) throw new ArgumentException("Empty hostname.");
                var bytes = await ReadAsync(stream, size, deadline.Token).ConfigureAwait(false);
                if (bytes.Any(b => b > 127)) throw new ArgumentException("Use an ASCII/punycode hostname.");
                host = Encoding.ASCII.GetString(bytes).TrimEnd('.').ToLowerInvariant();
            }
            else if (request[3] is 1 or 4)
                host = new IPAddress(await ReadAsync(stream, request[3] == 1 ? 4 : 16, deadline.Token).ConfigureAwait(false)).ToString();
            else throw new ArgumentException("Unknown address type.");
            var portBytes = await ReadAsync(stream, 2, deadline.Token).ConfigureAwait(false);
            var port = portBytes[0] * 256 + portBytes[1];
            targetHost = host;
            targetPort = port;
            RouteConnector.ValidateProxyTarget(host, port);
            upstream = await _connect(host, port, deadline.Token).ConfigureAwait(false);
            _upstreams.TryAdd(upstream, 0);
            await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, deadline.Token).ConfigureAwait(false);
            connected = true;
            deadline.CancelAfter(TimeSpan.FromHours(12));
            var upload = CopyAsync(stream, upstream, deadline.Token);
            var download = CopyAsync(upstream, stream, deadline.Token);
            await Task.WhenAny(upload, download).ConfigureAwait(false);
            deadline.Cancel();
            try { await Task.WhenAll(upload, download).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or ArgumentException or InvalidOperationException or TimeoutException)
        {
            if (!connected && !_stopping.IsCancellationRequested)
            {
                try { await stream.WriteAsync(new byte[] { 5, 1, 0, 1, 0, 0, 0, 0, 0, 0 }, _stopping.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                catch (Exception writeError) when (writeError is IOException or OperationCanceledException or TimeoutException or ObjectDisposedException) { }
            }
            if (!_stopping.IsCancellationRequested)
            {
                var target = targetHost is null ? "target=unknown" : "target=" + targetHost + ":" + targetPort;
                DiagnosticHub.Log.Write("network.proxy", "failed", target + " " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        finally
        {
            deadline.Cancel(); client.Dispose();
            _clients.TryRemove(client, out _);
            if (upstream is not null)
            {
                upstream.Dispose();
                _upstreams.TryRemove(upstream, out _);
            }
            _slots.Release();
        }
    }
    private static async Task<byte[]> ReadAsync(Stream stream, int length, CancellationToken token)
    {
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return bytes;
    }
    private static async Task CopyAsync(Stream input, Stream output, CancellationToken token)
    {
        var buffer = new byte[16384];
        while (true)
        {
            var read = await input.ReadAsync(buffer, token).AsTask().WaitAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
            if (read == 0) return;
            await output.WriteAsync(buffer.AsMemory(0, read), token).AsTask().WaitAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
        }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stopping.Cancel(); _listener.Stop();
        foreach (var client in _clients.Keys) client.Dispose();
        foreach (var upstream in _upstreams.Keys) upstream.Dispose();
    }
}
