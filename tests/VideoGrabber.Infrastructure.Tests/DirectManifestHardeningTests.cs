using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DirectManifestHardeningTests
{
    [Fact]
    public async Task Hls_relative_tracks_resolve_against_final_response_uri()
    {
        var requests = new List<Uri>();
        var client = new HlsPreflightClient(_ => new PreflightHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(requests.Count == 1
                ? Redirect("../final/master.m3u8")
                : Manifest("#EXTM3U\n#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio\",NAME=\"main\",URI=\"audio.m3u8\"\n#EXT-X-STREAM-INF:BANDWIDTH=100000,AUDIO=\"audio\"\nvideo.m3u8\n"));
        }));
        var result = await client.FetchAsync(new("https://audit.test/source/master.m3u8"), new(), CancellationToken.None);
        Assert.True(result.Success, result.Error);
        Assert.Equal(new Uri("https://audit.test/final/video.m3u8"), Assert.Single(result.Info!.Variants).Uri);
        Assert.Equal(new Uri("https://audit.test/final/audio.m3u8"), Assert.Single(result.Info.AudioRenditions).Uri);
        Assert.Equal(2, requests.Count);
    }

    [Theory]
    [InlineData("http://audit.test/master.m3u8")]
    [InlineData("https://user:pass@audit.test/master.m3u8")]
    [InlineData("file:///tmp/master.m3u8")]
    [InlineData("ftp://audit.test/master.m3u8")]
    public async Task Hls_unsafe_redirect_never_reaches_provider_or_transport(string location)
    {
        var providerCalls = 0;
        var requests = 0;
        var client = new HlsPreflightClient(_ => new PreflightHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(Redirect(location));
        }));
        var options = AuditNetworkRegressionTests.WithCookieProvider((_, _) =>
        {
            providerCalls++;
            return Task.FromResult<string?>("audit_cookie=synthetic_only");
        });
        var result = await client.FetchAsync(new("https://audit.test/source/master.m3u8"), options, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(1, requests);
        Assert.Equal(1, providerCalls);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task Hls_allows_at_most_five_redirects(int redirects, bool success)
    {
        var requests = 0;
        var client = new HlsPreflightClient(_ => new PreflightHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(requests <= redirects
                ? Redirect("/hop/" + requests)
                : Manifest("#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST\n"));
        }));
        var result = await client.FetchAsync(new("https://audit.test/master.m3u8"), new(), CancellationToken.None);
        Assert.Equal(success, result.Success);
        Assert.Equal(6, requests);
    }

    [Fact]
    public async Task Hls_deadline_cancels_cookie_provider_even_when_provider_ignores_token()
    {
        var requests = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerResult = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var client = new HlsPreflightClient(_ => new PreflightHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(Manifest("invalid"));
        }));
        var options = AuditNetworkRegressionTests.WithCookieProvider((_, token) =>
        {
            Assert.True(token.CanBeCanceled);
            started.SetResult();
            return providerResult.Task;
        });
        var fetch = client.FetchAsync(new("https://audit.test/master.m3u8"), options, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, requests);
        providerResult.TrySetResult(null);
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.Found);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static HttpResponseMessage Manifest(string body)
        => new(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class PreflightHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }

    [Fact]
    public void Browser_discovered_extensionless_HLS_is_marked_direct_manifest()
    {
        var candidate = new MediaCandidate(
            new Uri("https://vhapi02.getcourse.ru/api/playlist/master/abc?sig=secret"),
            new Uri("https://iglyrazuma.ru/lesson"), "HLS", "master",
            HlsManifest: ClearMaster());
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "best", false);
        Assert.True(plan.DirectManifest);
    }

    [Fact]
    public void Raw_or_encrypted_HLS_is_not_queueable_but_clear_manifest_is()
    {
        var source = new Uri("https://cdn.example/master");
        var referer = new Uri("https://school.example/lesson");
        Assert.False(MediaCandidatePolicy.CanQueue(new(source, referer, "HLS")));
        Assert.True(MediaCandidatePolicy.CanQueue(new(source, referer, "HLS", HlsManifest: ClearMaster())));
        Assert.False(MediaCandidatePolicy.CanQueue(new(source, referer, "HLS", HlsManifest: EncryptedMaster())));
        Assert.True(MediaCandidatePolicy.CanQueue(new(new Uri("https://cdn.example/video.mp4"), referer, "MP4")));
    }

    [Fact]
    public async Task Direct_manifest_download_forces_generic_extractor()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-direct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new RecordingRunner();
            var downloader = new YtDlpDownloader(runner, new ToolLocator(root, root), new ValidProbe());
            var result = await downloader.DownloadAsync(new DownloadRequest(
                new Uri("https://vhapi02.getcourse.ru/api/playlist/master/abc"), root, "best",
                DirectManifest: true), null, CancellationToken.None);
            Assert.True(result.Success, result.Message);
            Assert.Contains("--use-extractors", runner.Arguments);
            var index = runner.Arguments.IndexOf("--use-extractors");
            Assert.Equal("generic", runner.Arguments[index + 1]);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static HlsManifestInfo ClearMaster() => new(true,
        [new HlsVariant(new Uri("https://cdn.example/v.m3u8"), 1280, 720, 1_000_000, 30, null)],
        [], false, null, false);

    private static HlsManifestInfo EncryptedMaster() => new(true,
        [new HlsVariant(new Uri("https://cdn.example/v.m3u8"), 1280, 720, 1_000_000, 30, null)],
        [], true, "AES-128", false);

    private sealed class RecordingRunner : IProcessRunner
    {
        public List<string> Arguments { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            Arguments.AddRange(spec.Arguments);
            var output = Path.Combine(spec.WorkingDirectory!, "direct.mp4");
            File.WriteAllBytes(output, [1, 2, 3]);
            onOutput?.Invoke("filepath:" + output);
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class ValidProbe : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(new MediaProbeResult(true, true, true, "aac"));
    }
}
