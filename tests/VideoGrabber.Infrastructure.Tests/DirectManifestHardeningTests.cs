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
    public async Task Duration_probe_records_clear_leaf_and_returns_confirmed_duration()
    {
        var source = new Uri("https://audit.test/video.m3u8");
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        var client = new HlsPreflightClient(_ => new PreflightHandler((_, _) => Task.FromResult(
            Manifest("#EXTM3U\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST\n"))));
        var response = await client.FetchAsync(source, new(), CancellationToken.None);
        Assert.Equal(2d, HlsDownloadPolicy.RecordDurationProbeResult(cache, source, response));
        Assert.True(cache.ContainsKey(source.AbsoluteUri));
    }

    [Theory]
    [InlineData("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nchild.m3u8\n")]
    [InlineData("not a playlist")]
    [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key\"\n#EXTINF:2,\nsegment.ts\n")]
    [InlineData("#EXTM3U\n#EXT-X-KEY:URI=\"key\"\n#EXTINF:2,\nsegment.ts\n")]
    public async Task Duration_probe_revokes_prior_leaf_before_returning_without_duration(string body)
    {
        var source = new Uri("https://audit.test/video.m3u8");
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        cache[source.AbsoluteUri] = 0;
        var candidate = new MediaCandidate(new("https://audit.test/master.m3u8"), new("https://school.example/lesson"), "HLS",
            HlsManifest: new(true, [new HlsVariant(source, 1280, 720, 1, 30, null)], [], false, null, false));
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "720p", false);
        Assert.True(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>(cache.Keys)));
        var client = new HlsPreflightClient(_ => new PreflightHandler((_, _) => Task.FromResult(Manifest(body))));
        var response = await client.FetchAsync(source, new(), CancellationToken.None);
        var duration = HlsDownloadPolicy.RecordDurationProbeResult(cache, source, response);
        Assert.Null(duration);
        Assert.False(cache.ContainsKey(source.AbsoluteUri));
        var downloaderCalls = 0;
        var result = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => Task.FromResult(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>(cache.Keys))),
            () => { downloaderCalls++; return Task.FromResult(true); }, _ => false);
        Assert.False(result);
        Assert.Equal(0, downloaderCalls);
    }

    [Theory]
    [InlineData("devtools", "master", false)]
    [InlineData("webresource", "master", false)]
    [InlineData("devtools", "malformed", false)]
    [InlineData("webresource", "malformed", false)]
    [InlineData("devtools", "encrypted", false)]
    [InlineData("webresource", "encrypted", false)]
    [InlineData("devtools", "unknown-key", false)]
    [InlineData("webresource", "unknown-key", false)]
    [InlineData("devtools", "clear", true)]
    [InlineData("webresource", "clear", true)]
    public async Task Discovery_cache_requires_clear_leaf_and_revokes_nested_master(string discovery, string fixture, bool expected)
    {
        var body = fixture switch
        {
            "master" => "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nchild.m3u8\n",
            "malformed" => "not a playlist",
            "encrypted" => "#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key\"\n#EXTINF:2,\nsegment.ts\n",
            "unknown-key" => "#EXTM3U\n#EXT-X-KEY:URI=\"key\"\n#EXTINF:2,\nsegment.ts\n",
            _ => "#EXTM3U\n#EXTINF:2,\nsegment.ts\n"
        };
        var source = new Uri("https://audit.test/video.m3u8");
        var referer = new Uri("https://school.example/lesson");
        HlsManifestInfo? info;
        if (discovery == "devtools")
        {
            var response = System.Text.Json.JsonSerializer.Serialize(new { body, base64Encoded = false });
            Assert.True(DevToolsGetCourseResponseParser.TryDecodeBody(response, out var decoded));
            HlsManifestParser.TryParse(decoded!, source, out info);
        }
        else
        {
            HlsResponseCandidateResolver.TryResolve(body, source, referer, out var discovered, out _);
            info = discovered?.HlsManifest;
        }
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        cache[source.AbsoluteUri] = 0; // A prior clear response cannot validate a refreshed master.
        HlsDownloadPolicy.UpdateVerifiedClearLeafCache(cache, source, info);
        Assert.Equal(expected, cache.ContainsKey(source.AbsoluteUri));
        var candidate = new MediaCandidate(new("https://audit.test/master.m3u8"), referer, "HLS",
            HlsManifest: new(true, [new HlsVariant(source, 1280, 720, 1, 30, null)], [], false, null, false));
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "720p", false);
        var downloaderCalls = 0;
        var result = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => Task.FromResult(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>(cache.Keys))),
            () => { downloaderCalls++; return Task.FromResult(true); }, _ => false);
        Assert.Equal(expected, result);
        Assert.Equal(expected ? 1 : 0, downloaderCalls);
    }

    [Theory]
    [InlineData("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nchild.m3u8\n", false)]
    [InlineData("not a playlist", false)]
    [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key\"\n#EXTINF:2,\nsegment.ts\n", false)]
    [InlineData("#EXTM3U\n#EXT-X-KEY:URI=\"key\"\n#EXTINF:2,\nsegment.ts\n", false)]
    [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=UNKNOWN,URI=\"key\"\n#EXTINF:2,\nsegment.ts\n", false)]
    [InlineData("#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST\n", true)]
    public async Task Only_parsed_clear_media_leaf_is_verified(string body, bool expected)
    {
        var client = new HlsPreflightClient(_ => new PreflightHandler((_, _) => Task.FromResult(Manifest(body))));
        var result = await client.FetchAsync(new("https://audit.test/video.m3u8"), new(), CancellationToken.None);
        Assert.Equal(expected, HlsDownloadPolicy.IsVerifiedClearLeaf(result));
    }

    [Theory]
    [InlineData("#EXTM3U\n#EXTINF:2,\nsegment.ts\n", true)]
    [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key\"\n#EXTINF:2,\nsegment.ts\n", false)]
    [InlineData("#EXTM3U\n#EXT-X-KEY:URI=\"key\"\n#EXTINF:2,\nsegment.ts\n", false)]
    [InlineData("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nchild.m3u8\n", false)]
    public async Task Split_download_runs_only_after_both_selected_leaves_are_clear(string audioBody, bool expected)
    {
        var candidate = new MediaCandidate(new("https://audit.test/master.m3u8"), new("https://school.example/lesson"), "HLS",
            HlsManifest: new(true, [new HlsVariant(new("https://audit.test/video.m3u8"), 1280, 720, 1, 30, "aud")],
                [new HlsAudioRendition(new("https://audit.test/audio.m3u8"), "ru", "Russian", "aud", true)], false, null, false));
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "720p", false);
        Assert.Equal("https://audit.test/video.m3u8", plan.HlsVideoSource!.AbsoluteUri);
        Assert.Equal("https://audit.test/audio.m3u8", plan.HlsAudioSource!.AbsoluteUri);
        var verified = new HashSet<string>();
        var client = new HlsPreflightClient(_ => new PreflightHandler((request, _) => Task.FromResult(Manifest(
            request.RequestUri!.AbsolutePath == "/audio.m3u8" ? audioBody : "#EXTM3U\n#EXTINF:2,\nsegment.ts\n"))));
        var video = await client.FetchAsync(plan.HlsVideoSource, new(), CancellationToken.None);
        if (HlsDownloadPolicy.IsVerifiedClearLeaf(video)) verified.Add(plan.HlsVideoSource.AbsoluteUri);
        Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, verified));
        var audio = await client.FetchAsync(plan.HlsAudioSource, new(), CancellationToken.None);
        if (HlsDownloadPolicy.IsVerifiedClearLeaf(audio)) verified.Add(plan.HlsAudioSource.AbsoluteUri);
        var downloaderCalls = 0;
        var result = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => Task.FromResult(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, verified)),
            () => { downloaderCalls++; return Task.FromResult(true); }, _ => false);
        Assert.Equal(expected, result);
        Assert.Equal(expected ? 1 : 0, downloaderCalls);
    }

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
