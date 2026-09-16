using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Networking;
using VideoGrabber.Core.Security;
namespace VideoGrabber.Infrastructure.Tests;
public sealed class RoutingDownloadTests
{
    [Fact]
    public async Task Sends_configured_loopback_proxy_with_remote_dns_and_keeps_progress()
    {
        var root = Path.Combine(Path.GetTempPath(), "vg-route-test-" + Guid.NewGuid().ToString("N"));
        var runner = new Recorder();
        try
        {
            var registry = new ManagedEgressSessionRegistry();
            using var session = new DownloadEgressSession(registry, (_, _, _) => Task.FromResult<Stream>(Stream.Null),
                new EgressPolicy("routing-test", true));
            await new YtDlpDownloader(runner, new ToolLocator(root, root), egressRegistry: registry).DownloadAsync(
                new DownloadRequest(new Uri("https://example.com/video"), root, "best",
                    LocalProxy: session.Lease.ProxyUri.AbsoluteUri, EgressCapabilityId: session.Lease.Id,
                    EgressEndpoint: session.Lease.ProxyUri), null, CancellationToken.None);
            Assert.NotNull(runner.Spec);
            Assert.Contains("--proxy", runner.Spec.Arguments);
            Assert.Contains("socks5h://127.0.0.1:" + session.Lease.ProxyUri.Port, runner.Spec.Arguments);
            Assert.Contains("--progress", runner.Spec.Arguments);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData("http://random-proxy.example:80")]
    [InlineData("socks5://user:secret@127.0.0.1:1234")]
    [InlineData("socks5://127.0.0.1:1234/inject")]
    public async Task Rejects_injected_external_proxy_or_credentials(string proxy)
    {
        var root = Path.Combine(Path.GetTempPath(), "vg-route-test-" + Guid.NewGuid().ToString("N"));
        var runner = new Recorder();
        try
        {
            var result = await new YtDlpDownloader(runner, new ToolLocator(root, root)).DownloadAsync(
                new DownloadRequest(new Uri("https://example.com/video"), root, "best", LocalProxy: proxy), null, CancellationToken.None);
            Assert.False(result.Success); Assert.Null(runner.Spec);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class Recorder : IProcessRunner
    {
        public ProcessSpec? Spec { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? output, CancellationToken token)
        { Spec = spec; return Task.FromResult(new ProcessResult(1, "", "No download in this argument-contract test.")); }
    }
}
