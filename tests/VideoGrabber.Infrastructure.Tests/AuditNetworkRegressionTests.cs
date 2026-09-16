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
[Collection("ProcessTreeSerial")]
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
        using var egress = ManagedEgressFixture.RegisterListener("cross-host-preflight", initial);
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
            }, egress.Registry);
            var response = await client.FetchAsync(initial, new HlsPreflightFetchOptions(
                LocalProxy: egress.Lease.ProxyUri.AbsoluteUri, CookieHeader: cookie,
                EgressCapabilityId: egress.Lease.Id, EgressEndpoint: egress.Lease.ProxyUri), deadline.Token);
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

    internal static HlsPreflightFetchOptions WithCookieProvider(Func<Uri, CancellationToken, Task<string?>> provider)
        => new(CookieHeader: "raw_cookie=must_not_win", CookieProvider: provider);

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
        var root = Path.Combine(Path.GetTempPath(), "VG-process-tree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var statePath = Path.Combine(root, "child-state.txt");
        var readyPath = Path.Combine(root, "child-ready.txt");
        var goPath = Path.Combine(root, "allow-child-stdout.signal");
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "ProcessTreeFixture.ps1"));
        Assert.True(File.Exists(fixture), $"Process-tree fixture not found: {fixture}");

        using var cancel = new CancellationTokenSource();
        var parentPid = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var childPidFromParent = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inheritedPipeChildPid = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Process? ownedChild = null;
        var work = new ProcessRunner().RunAsync(new ProcessSpec("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-File", fixture,
                "-StatePath", statePath, "-ReadyPath", readyPath, "-GoPath", goPath], Timeout: TimeSpan.FromSeconds(30)), line =>
            {
                TryCapturePid(line, "AUDIT_PARENT_PID:", parentPid);
                TryCapturePid(line, "AUDIT_CHILD_PID_FROM_PARENT:", childPidFromParent);
                TryCapturePid(line, "AUDIT_CHILD_PIPE_HELD:", inheritedPipeChildPid);
            }, cancel.Token);
        try
        {
            var parentId = await parentPid.Task.WaitAsync(TimeSpan.FromSeconds(8));
            var evidence = await WaitForOwnedChildEvidenceAsync(statePath, readyPath, TimeSpan.FromSeconds(8));
            Assert.Equal(evidence.Pid, await childPidFromParent.Task.WaitAsync(TimeSpan.FromSeconds(3)));

            var parentExited = false;
            try
            {
                using var parent = Process.GetProcessById(parentId);
                await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                parentExited = parent.HasExited;
            }
            catch (ArgumentException)
            {
                // The fixture parent may have already exited before its stdout marker is pumped.
                parentExited = true;
            }
            Assert.True(parentExited, "Synthetic parent must actually exit before cancellation.");

            ownedChild = Process.GetProcessById(evidence.Pid);
            Assert.Equal(evidence.StartTicks, ownedChild.StartTime.ToUniversalTime().Ticks);
            Assert.False(ownedChild.HasExited, "Synthetic owned child must still be alive after its parent exits.");

            File.WriteAllText(goPath, "parent-exited");
            Assert.Equal(evidence.Pid, await inheritedPipeChildPid.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(work.IsCompleted,
                "Runner unexpectedly completed while the verified descendant still held the inherited output pipe.");

            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(TimeSpan.FromSeconds(6)));
            ownedChild.Refresh();
            Assert.True(ownedChild.HasExited, "Cancellation must reap the verified owned descendant.");
        }
        finally
        {
            cancel.Cancel();
            if (ownedChild is not null)
            {
                try
                {
                    ownedChild.Refresh();
                    if (!ownedChild.HasExited) ownedChild.Kill(entireProcessTree: true);
                    await ownedChild.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException) { }
                ownedChild.Dispose();
            }
            try { await work.WaitAsync(TimeSpan.FromSeconds(6)); }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { }
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }

        static void TryCapturePid(string line, string prefix, TaskCompletionSource<int> target)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(line[prefix.Length..], out var pid))
                target.TrySetResult(pid);
        }

        static async Task<(int Pid, long StartTicks)> WaitForOwnedChildEvidenceAsync(
            string statePath, string readyPath, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                if (TryReadEvidence(statePath, out var state) && TryReadEvidence(readyPath, out var ready))
                {
                    Assert.Equal(state.Pid, ready.Pid);
                    Assert.Equal(state.StartTicks, ready.StartTicks);
                    return state;
                }
                await Task.Delay(25);
            }
            throw new TimeoutException("Synthetic child did not publish PID/start-time readiness evidence.");
        }

        static bool TryReadEvidence(string path, out (int Pid, long StartTicks) evidence)
        {
            evidence = default;
            if (!File.Exists(path)) return false;
            try
            {
                var parts = File.ReadAllText(path).Trim().Split('|');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var pid) || !long.TryParse(parts[1], out var ticks)) return false;
                evidence = (pid, ticks);
                return true;
            }
            catch (IOException) { return false; }
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
