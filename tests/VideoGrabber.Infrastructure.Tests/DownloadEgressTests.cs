using System.Net;
using System.Net.Sockets;
using System.Text;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadEgressTests
{
    [Fact]
    public void Managed_registry_rejects_unknown_mismatch_and_revoked_capability()
    {
        var registry = new ManagedEgressSessionRegistry();
        var endpoint = new Uri("socks5://127.0.0.1:32123");
        var lease = registry.Issue(endpoint, new EgressPolicy("test", PublicOnly: true));

        Assert.Equal(lease, registry.Resolve(lease.Id, endpoint));
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(Guid.NewGuid(), endpoint));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Resolve(lease.Id, new Uri("socks5://127.0.0.1:32124")));

        registry.Revoke(lease.Id);
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(lease.Id, endpoint));
    }

    [Fact]
    public void Registry_refuses_unowned_non_loopback_proxy_endpoints()
    {
        var registry = new ManagedEgressSessionRegistry();
        Assert.Throws<ArgumentException>(() => registry.Issue(
            new Uri("socks5://8.8.8.8:32123"), new EgressPolicy("bad", PublicOnly: true)));
    }

    [Fact]
    public void Owned_session_registers_exact_proxy_and_revokes_on_dispose()
    {
        var registry = new ManagedEgressSessionRegistry();
        var session = new DownloadEgressSession(registry,
            (_, _, _) => Task.FromException<Stream>(new InvalidOperationException("not used")),
            new EgressPolicy("owned", PublicOnly: true));
        var lease = session.Lease;

        Assert.Equal("socks5", lease.ProxyUri.Scheme);
        Assert.Equal("127.0.0.1", lease.ProxyUri.Host);
        Assert.Equal(lease, registry.Resolve(lease.Id, lease.ProxyUri));

        session.Dispose();
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(lease.Id, lease.ProxyUri));
    }
}

public sealed partial class DownloadEgressBoundaryTests
{
    [Fact]
    public async Task Raw_proxy_without_capability_is_rejected_before_process_start()
    {
        var root = Path.Combine(Path.GetTempPath(), "vg-egress-raw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new RecordingRunner();
            var request = new VideoGrabber.Core.Downloads.DownloadRequest(
                new Uri("https://example.com/video"), root, "best",
                LocalProxy: "socks5://127.0.0.1:32123");
            var result = await new VideoGrabber.Infrastructure.Downloads.YtDlpDownloader(
                runner, new VideoGrabber.Infrastructure.Components.ToolLocator(root, root))
                .DownloadAsync(request, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, runner.Calls);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task Active_capability_allows_only_its_exact_proxy_endpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), "vg-egress-cap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var registry = new ManagedEgressSessionRegistry();
            var endpoint = new Uri("socks5://127.0.0.1:32123");
            var lease = registry.Issue(endpoint, new EgressPolicy("test", PublicOnly: true));
            var runner = new RecordingRunner();
            var request = new VideoGrabber.Core.Downloads.DownloadRequest(
                new Uri("https://example.com/video"), root, "best",
                LocalProxy: endpoint.AbsoluteUri,
                EgressCapabilityId: lease.Id, EgressEndpoint: endpoint);
            var result = await new VideoGrabber.Infrastructure.Downloads.YtDlpDownloader(
                runner, new VideoGrabber.Infrastructure.Components.ToolLocator(root, root), egressRegistry: registry)
                .DownloadAsync(request, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(1, runner.Calls);
            Assert.NotNull(runner.LastSpec);
            Assert.Contains("socks5h://127.0.0.1:32123", runner.LastSpec!.Arguments);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task Unknown_preflight_capability_fails_before_handler_creation()
    {
        var registry = new ManagedEgressSessionRegistry();
        var endpoint = new Uri("socks5://127.0.0.1:32123");
        var handlerCalls = 0;
        var client = new VideoGrabber.Infrastructure.Browser.HlsPreflightClient(_ =>
        {
            handlerCalls++;
            throw new InvalidOperationException("network must not start");
        }, registry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchAsync(
            new Uri("https://example.com/master.m3u8"),
            new VideoGrabber.Infrastructure.Browser.HlsPreflightFetchOptions(
                LocalProxy: endpoint.AbsoluteUri, EgressCapabilityId: Guid.NewGuid(), EgressEndpoint: endpoint),
            CancellationToken.None));
        Assert.Equal(0, handlerCalls);
    }

    private sealed class RecordingRunner : VideoGrabber.Core.Processes.IProcessRunner
    {
        public int Calls { get; private set; }
        public VideoGrabber.Core.Processes.ProcessSpec? LastSpec { get; private set; }
        public Task<VideoGrabber.Core.Processes.ProcessResult> RunAsync(
            VideoGrabber.Core.Processes.ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            Calls++;
            LastSpec = spec;
            return Task.FromResult(new VideoGrabber.Core.Processes.ProcessResult(1, "", "synthetic failure"));
        }
    }
}


public sealed class DownloadEgressSsrfTests
{
    [Fact]
    public async Task Redirect_to_private_literal_is_blocked_before_second_request()
    {
        var handler = new RedirectHandler();
        var client = new VideoGrabber.Infrastructure.Browser.HlsPreflightClient(_ => handler);
        var result = await client.FetchAsync(new Uri("https://example.com/master.m3u8"), new(), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Domain_resolving_to_private_is_rejected_before_connect()
    {
        var policy = new VideoGrabber.Infrastructure.Networking.SiteRoutePolicy(
            new VideoGrabber.Infrastructure.Networking.SiteRouteSettings([]));
        var connector = new VideoGrabber.Infrastructure.Networking.RouteConnector(policy,
            (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connector.OpenAsync("public.example", 443, CancellationToken.None));
    }

    [MediaToolsFact]
    public async Task DefaultRouteNestedManifestMustNotReachLoopback()
    {
        var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath();
        var root = Path.Combine(evidence, "egress-nested-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var rootListener = new TcpListener(IPAddress.Loopback, 0);
        using var forbiddenListener = new TcpListener(IPAddress.Loopback, 0);
        rootListener.Start(); forbiddenListener.Start();
        var rootPort = ((IPEndPoint)rootListener.LocalEndpoint).Port;
        var forbiddenPort = ((IPEndPoint)forbiddenListener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource();
        var rootHits = 0; var forbiddenHits = 0;
        var rootServer = ServeRootAsync();
        var forbiddenServer = CountForbiddenAsync();
        var registry = new VideoGrabber.Infrastructure.Networking.ManagedEgressSessionRegistry();
        using var session = new VideoGrabber.Infrastructure.Networking.DownloadEgressSession(registry,
            ConnectOwnedPublicAliasAsync, new EgressPolicy("nested-fixture", PublicOnly: false), ValidateTarget);
        try
        {
            var tools = new VideoGrabber.Infrastructure.Components.ToolLocator(
                Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
            var runner = new VideoGrabber.Infrastructure.Processes.ProcessRunner();
            var source = new Uri($"http://public-fixture.example:{rootPort}/master.m3u8");
            var request = new VideoGrabber.Core.Downloads.DownloadRequest(source, Path.Combine(root, "output"), "best",
                LocalProxy: session.Lease.ProxyUri.AbsoluteUri, DirectManifest: true,
                EgressCapabilityId: session.Lease.Id, EgressEndpoint: session.Lease.ProxyUri);
            var result = await new VideoGrabber.Infrastructure.Downloads.YtDlpDownloader(
                runner, tools, egressRegistry: registry).DownloadAsync(request, null, CancellationToken.None);
            Assert.False(result.Success);
            Assert.True(Volatile.Read(ref rootHits) > 0);
            Assert.Equal(0, Volatile.Read(ref forbiddenHits));
        }
        finally
        {
            stop.Cancel(); rootListener.Stop(); forbiddenListener.Stop();
            try { await rootServer; } catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
            try { await forbiddenServer; } catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
        }

        void ValidateTarget(string host, int port)
        {
            if (!host.Equals("public-fixture.example", StringComparison.OrdinalIgnoreCase) || port != rootPort)
                throw new InvalidOperationException("Nested target is outside the named fixture capability.");
        }

        async Task<Stream> ConnectOwnedPublicAliasAsync(string host, int port, CancellationToken token)
        {
            ValidateTarget(host, port);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(IPAddress.Loopback, rootPort, token); return new NetworkStream(socket, true); }
            catch { socket.Dispose(); throw; }
        }

        async Task ServeRootAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await rootListener.AcceptTcpClientAsync(stop.Token);
                Interlocked.Increment(ref rootHits);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(stop.Token))) { }
                var body = Encoding.UTF8.GetBytes($"#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=128000\nhttp://127.0.0.1:{forbiddenPort}/leaf.m3u8\n");
                var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/vnd.apple.mpegurl\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(head, stop.Token); await stream.WriteAsync(body, stop.Token);
            }
        }

        async Task CountForbiddenAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await forbiddenListener.AcceptTcpClientAsync(stop.Token);
                Interlocked.Increment(ref forbiddenHits);
            }
        }
    }

    private sealed class RedirectHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls > 1) throw new InvalidOperationException("Private redirect reached a second request.");
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("http://127.0.0.1/private.m3u8");
            return Task.FromResult(response);
        }
    }
}

public sealed partial class DownloadEgressBoundaryTests
{
    [Fact]
    public async Task RedirectToPrivateMustFail()
    {
        var sends = 0;
        var client = new VideoGrabber.Infrastructure.Browser.HlsPreflightClient(_ =>
            new DelegateHandler((_, _) =>
            {
                sends++;
                var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("http://127.0.0.1/private.m3u8");
                return Task.FromResult(response);
            }));
        var result = await client.FetchAsync(new Uri("https://public.example/master.m3u8"),
            new VideoGrabber.Infrastructure.Browser.HlsPreflightFetchOptions(), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task DefaultRouteNestedManifestMustNotReachLoopback()
    {
        var sends = 0;
        const string body = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=100000\nhttp://127.0.0.1/private.m3u8\n";
        var client = new VideoGrabber.Infrastructure.Browser.HlsPreflightClient(_ =>
            new DelegateHandler((_, _) =>
            {
                sends++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body)
                });
            }));
        var result = await client.FetchAsync(new Uri("https://public.example/master.m3u8"),
            new VideoGrabber.Infrastructure.Browser.HlsPreflightFetchOptions(), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(1, sends);
        Assert.Contains("вложенную", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DomainResolvingToPrivateMustFail()
    {
        var connector = new RouteConnector(
            new SiteRoutePolicy(new SiteRouteSettings([])),
            (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connector.OpenAsync("public.example", 443, CancellationToken.None));
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
