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
            Assert.Contains("Cookie: audit_cookie=synthetic_only", observed.Source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Host: localhost:", observed.Target, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("audit_cookie=synthetic_only", observed.Target, StringComparison.Ordinal);
        }
        finally
        {
            deadline.Cancel();
            listener.Stop();
            try { await server; }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException or IOException) { }
        }
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task Cookie_raw_header_is_not_forwarded_to_changed_path(int status)
    {
        var observed = await RunCookieRedirectAsync("audit.test", "/source/master.m3u8", "/target/master.m3u8", status,
            _ => new(CookieHeader: "audit_cookie=synthetic_only"));
        Assert.True(observed.Result.Success, observed.Result.Error);
        Assert.Contains("Cookie: audit_cookie=synthetic_only", observed.Source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audit_cookie=synthetic_only", observed.Target, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?step=2")]
    [InlineData("/source/master.m3u8?step=2")]
    public async Task Cookie_raw_header_remains_on_same_origin_exact_path(string location)
    {
        var observed = await RunCookieRedirectAsync("audit.test", "/source/master.m3u8", location, 302,
            _ => new(CookieHeader: "audit_cookie=synthetic_only"));
        Assert.True(observed.Result.Success, observed.Result.Error);
        Assert.Contains("Cookie: audit_cookie=synthetic_only", observed.Source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cookie: audit_cookie=synthetic_only", observed.Target, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("audit.test", "sub.audit.test")]
    [InlineData("sub.audit.test", "audit.test")]
    public async Task Cookie_raw_header_is_not_forwarded_between_parent_and_subdomain(string sourceHost, string targetHost)
    {
        var observed = await RunCookieRedirectAsync(sourceHost, "/source/master.m3u8", "//" + targetHost + ":{port}/source/master.m3u8", 302,
            _ => new(CookieHeader: "audit_cookie=synthetic_only"));
        Assert.True(observed.Result.Success, observed.Result.Error);
        Assert.Contains("Cookie: audit_cookie=synthetic_only", observed.Source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audit_cookie=synthetic_only", observed.Target, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/source/leaf.m3u8", true)]
    [InlineData("/target/leaf.m3u8", false)]
    [InlineData("/source-other/leaf.m3u8", false)]
    public async Task Cookie_provider_recomputes_path_scope_on_each_hop(string location, bool expected)
    {
        var calls = new List<Uri>();
        var observed = await RunCookieRedirectAsync("audit.test", "/source/master.m3u8", location, 302, source =>
        {
            var jar = new CookieContainer();
            jar.Add(source, new Cookie("audit_cookie", "synthetic_only", "/source"));
            return WithCookieProvider((uri, _) => { calls.Add(uri); return Task.FromResult<string?>(jar.GetCookieHeader(uri)); });
        });
        Assert.True(observed.Result.Success, observed.Result.Error);
        Assert.Equal(2, calls.Count);
        Assert.Contains("Cookie: audit_cookie=synthetic_only", observed.Source, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expected, observed.Target.Contains("audit_cookie=synthetic_only", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cookie_provider_applies_domain_and_secure_scope_without_inheriting_previous_headers()
    {
        var observed = await RunCookieRedirectAsync("audit.test", "/source/master.m3u8", "//sub.audit.test:{port}/source/master.m3u8", 302, source =>
        {
            var jar = new CookieContainer();
            jar.Add(source, new Cookie("audit_cookie", "synthetic_only", "/"));
            jar.Add(new Cookie("audit_domain", "synthetic_only", "/", ".audit.test"));
            jar.Add(new Cookie("audit_secure", "synthetic_only", "/", ".audit.test") { Secure = true });
            return WithCookieProvider((uri, _) => Task.FromResult<string?>(jar.GetCookieHeader(uri)));
        });
        Assert.True(observed.Result.Success, observed.Result.Error);
        Assert.Contains("audit_cookie=synthetic_only", observed.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("audit_cookie=synthetic_only", observed.Target, StringComparison.Ordinal);
        Assert.Contains("audit_domain=synthetic_only", observed.Source, StringComparison.Ordinal);
        Assert.Contains("audit_domain=synthetic_only", observed.Target, StringComparison.Ordinal);
        Assert.DoesNotContain("audit_secure=synthetic_only", observed.Source + observed.Target, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 6)]
    public async Task Cookie_redirect_loop_and_hop_limit_are_bounded(bool loop, int expectedRequests)
    {
        var observed = await RunCookieRedirectAsync("audit.test", "/source/master.m3u8",
            loop ? "/source/master.m3u8" : "/target/master.m3u8", 302,
            _ => new(CookieHeader: "audit_cookie=synthetic_only"), endless: true);
        Assert.False(observed.Result.Success);
        Assert.Equal(expectedRequests, observed.RequestCount);
    }

    // Resolve the new public contract at runtime so these tests build and fail against the old client.
    internal static HlsPreflightFetchOptions WithCookieProvider(Func<Uri, CancellationToken, Task<string?>> provider)
    {
        var constructor = typeof(HlsPreflightFetchOptions).GetConstructors()
            .SingleOrDefault(item => item.GetParameters().Length == 5);
        Assert.NotNull(constructor);
        return (HlsPreflightFetchOptions)constructor.Invoke([null, null, null, "raw_cookie=must_not_win", provider]);
    }

    private static async Task<(HlsPreflightFetchResult Result, string Source, string Target, int RequestCount)> RunCookieRedirectAsync(
        string sourceHost, string sourcePath, string location, int status,
        Func<Uri, HlsPreflightFetchOptions> options, bool endless = false)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var initial = new Uri($"http://{sourceHost}:{port}{sourcePath}");
        var headers = new List<string>();
        var server = ServeAsync();
        try
        {
            var client = new HlsPreflightClient(fetchOptions =>
            {
                var factory = typeof(HlsPreflightClient).GetMethod("CreateHandler", BindingFlags.NonPublic | BindingFlags.Static)!;
                var handler = (SocketsHttpHandler)factory.Invoke(null, [fetchOptions])!;
                handler.UseProxy = false;
                handler.ConnectCallback = async (context, token) =>
                {
                    if (context.DnsEndPoint.Port != port || context.DnsEndPoint.Host is not ("audit.test" or "sub.audit.test"))
                        throw new InvalidOperationException("Cookie fixture refused a destination outside its explicit host and port list.");
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
            var result = await client.FetchAsync(initial, options(initial), deadline.Token);
            deadline.Cancel();
            await server;
            return (result, headers.ElementAtOrDefault(0) ?? "", headers.ElementAtOrDefault(1) ?? "", headers.Count);
        }
        finally
        {
            deadline.Cancel();
            listener.Stop();
            await server;
        }

        async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
                    var stream = connection.GetStream();
                    headers.Add(await ReadHeadersAsync(stream, deadline.Token));
                    var next = location.Replace("{port}", port.ToString(), StringComparison.Ordinal);
                    if (endless && headers.Count > 1 && next != sourcePath) next += "?hop=" + headers.Count;
                    const string body = "#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST\n";
                    var response = headers.Count == 1 || endless
                        ? $"HTTP/1.1 {status} Redirect\r\nLocation: {next}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : $"HTTP/1.1 200 OK\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(response), deadline.Token);
                    if (headers.Count >= 2 && !endless) return;
                }
            }
            catch (Exception ex) when (deadline.IsCancellationRequested && ex is OperationCanceledException or SocketException or ObjectDisposedException or IOException) { }
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
