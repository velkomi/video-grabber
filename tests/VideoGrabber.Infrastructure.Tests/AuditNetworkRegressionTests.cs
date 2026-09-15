using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

// Audit-only network fixtures. All sockets are pinned to this test's loopback listener.
public sealed class AuditNetworkRegressionTests
{
    [Fact]
    public async Task Cross_host_hls_redirect_must_not_forward_source_cookie()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(2);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var initial = new Uri($"http://127.0.0.1:{port}/source/master.m3u8");
        var redirected = new Uri($"http://localhost:{port}/target/master.m3u8");
        const string cookie = "audit_cookie=synthetic_only";
        var server = ServeRedirectAsync(listener, redirected, deadline.Token);
        try
        {
            var client = new HlsPreflightClient(options =>
            {
                // Preserve production defaults for redirect and cookie behavior. Only transport is
                // isolated: no system proxy, DNS, adapter routing, or external destination is used.
                var factory = typeof(HlsPreflightClient).GetMethod("CreateHandler", BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("Production handler factory not found.");
                var handler = (SocketsHttpHandler)factory.Invoke(null, [options])!;
                handler.UseProxy = false;
                handler.ConnectCallback = async (context, token) =>
                {
                    if (context.DnsEndPoint.Port != port || context.DnsEndPoint.Host is not ("127.0.0.1" or "localhost"))
                        throw new InvalidOperationException("Audit fixture refused a non-local target.");
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), token);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch { socket.Dispose(); throw; }
                };
                return handler;
            });
            var response = await client.FetchAsync(initial, new HlsPreflightFetchOptions(CookieHeader: cookie), deadline.Token);
            var observed = await server;
            Assert.True(response.Success, response.Error);
            Assert.Contains("Cookie: " + cookie, observed.Source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Host: localhost:", observed.Target, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(cookie, observed.Target, StringComparison.Ordinal);
        }
        finally
        {
            deadline.Cancel();
            listener.Stop();
            try { await server; }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException or IOException) { }
        }
    }

    [Fact]
    public async Task Cancellation_after_parent_exit_must_stop_pipe_holding_descendant_promptly()
    {
        Assert.True(OperatingSystem.IsWindows(), "This regression is intended for the audited Windows runtime.");
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var startedChild = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var childScript = "[Console]::WriteLine('AUDIT_CHILD_STARTED:' + $PID); Start-Sleep -Seconds 5";
        var encodedChild = Convert.ToBase64String(Encoding.Unicode.GetBytes(childScript));
        var parentScript = "$p = [Diagnostics.ProcessStartInfo]::new(); "
            + "$p.FileName = 'powershell.exe'; $p.UseShellExecute = $false; $p.CreateNoWindow = $true; "
            + "$p.Arguments = '-NoProfile -NonInteractive -EncodedCommand " + encodedChild + "'; "
            + "$child = [Diagnostics.Process]::Start($p); "
            + "[Console]::WriteLine('AUDIT_PARENT_EXITING'); exit 0";
        var work = new ProcessRunner().RunAsync(new ProcessSpec("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", parentScript], Timeout: TimeSpan.FromSeconds(12)), line =>
            {
                const string prefix = "AUDIT_CHILD_STARTED:";
                if (line.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(line[prefix.Length..], out var pid))
                    startedChild.TrySetResult(pid);
            }, cancel.Token);
        try
        {
            _ = await startedChild.Task.WaitAsync(TimeSpan.FromSeconds(8));
            var watch = Stopwatch.StartNew();
            cancel.CancelAfter(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4.5),
                "Cancel waited for descendant's independent 5-second sleep instead of reaping the owned tree.");
        }
        finally
        {
            cancel.Cancel();
            try { await work.WaitAsync(TimeSpan.FromSeconds(12)); }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { }
            // The child independently exits after five seconds. Never kill an unverified foreign PID.
        }
    }

    private static async Task<(string Source, string Target)> ServeRedirectAsync(
        TcpListener listener, Uri target, CancellationToken token)
    {
        string sourceHeaders;
        using (var source = await listener.AcceptTcpClientAsync(token))
        {
            var stream = source.GetStream();
            sourceHeaders = await ReadHeadersAsync(stream, token);
            var response = Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: " + target.AbsoluteUri
                + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, token);
        }
        using var destination = await listener.AcceptTcpClientAsync(token);
        var targetStream = destination.GetStream();
        var targetHeaders = await ReadHeadersAsync(targetStream, token);
        const string manifest = "#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXT-X-MEDIA-SEQUENCE:0\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST\n";
        var body = Encoding.UTF8.GetBytes(manifest);
        var headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/vnd.apple.mpegurl\r\nContent-Length: "
            + body.Length + "\r\nConnection: close\r\n\r\n");
        await targetStream.WriteAsync(headers, token);
        await targetStream.WriteAsync(body, token);
        return (sourceHeaders, targetHeaders);
    }

    private static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (bytes.Count < 32768)
        {
            var count = await stream.ReadAsync(one.AsMemory(), token);
            if (count == 0) throw new IOException("Unexpected end of audit HTTP headers.");
            bytes.Add(one[0]);
            var n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == 13 && bytes[n - 3] == 10 && bytes[n - 2] == 13 && bytes[n - 1] == 10)
                return Encoding.ASCII.GetString(bytes.ToArray());
        }
        throw new InvalidDataException("Audit HTTP headers exceeded 32 KB.");
    }
}
