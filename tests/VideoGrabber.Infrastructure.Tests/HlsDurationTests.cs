using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class HlsDurationTests
{
    [Theory]
    [InlineData("Infinity")]
    [InlineData("1e309")]
    [InlineData("NaN")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("invalid")]
    [InlineData("31536001")]
    [InlineData("15768001", "15768000")]
    [InlineData("1e308", "1e308")]
    public void Unsafe_segment_invalidates_whole_sum_without_hiding_encryption(string first, string second = "10")
    {
        var text = $"#EXTM3U\n#EXTINF:{first},\na.ts\n#EXTINF:{second},\nb.ts\n#EXT-X-KEY:METHOD=SAMPLE-AES,URI=\"key\"\n#EXT-X-ENDLIST\n";
        Assert.True(HlsManifestParser.TryParse(text, new Uri("https://cdn.example/media.m3u8"), out var info));
        Assert.Null(info!.DurationSeconds);
        Assert.Null(info.WindowDurationSeconds);
        Assert.True(info.DurationInvalid);
        Assert.True(info.HasEndList);
        Assert.True(info.IsEncrypted);
        Assert.True(info.UsesDrmLikeEncryption);
    }

    [Fact]
    public void Exact_parser_cap_is_valid_and_live_window_becomes_total_only_at_endlist()
    {
        const string text = "#EXTM3U\n#EXTINF:15768000,\na.ts\n#EXTINF:15768000,\nb.ts\n";
        var source = new Uri("https://cdn.example/live.m3u8");
        Assert.True(HlsManifestParser.TryParse(text, source, out var live));
        Assert.True(live!.IsLive);
        Assert.Null(live.DurationSeconds);
        Assert.Equal(31536000d, live.WindowDurationSeconds);
        Assert.False(live.DurationInvalid);
        Assert.True(HlsManifestParser.TryParse(text + "#EXT-X-ENDLIST\n", source, out var vod));
        Assert.False(vod!.IsLive);
        Assert.Equal(31536000d, vod.DurationSeconds);
        Assert.False(vod.DurationInvalid);
    }

    [Theory]
    [InlineData(false, 10d, false)]
    [InlineData(true, double.PositiveInfinity, false)]
    [InlineData(true, 31536001d, false)]
    [InlineData(true, 10d, true)]
    public void Duration_probe_caches_clear_leaf_but_rejects_live_or_unsafe_total(bool endList, double duration, bool invalid)
    {
        var source = new Uri("https://cdn.example/leaf.m3u8");
        var leaf = new HlsManifestInfo(false, [], [], false, null, false, duration,
            HasEndList: endList, WindowDurationSeconds: 10, DurationInvalid: invalid);
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        Assert.Null(HlsDownloadPolicy.RecordDurationProbeResult(cache, source, new(true, false, leaf, null)));
        Assert.True(cache.ContainsKey(source.AbsoluteUri));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parsed_manifest_maps_live_to_unknown_and_completed_vod_to_exact_request_duration(bool endList)
    {
        var source = new Uri("https://cdn.example/leaf.m3u8");
        var text = "#EXTM3U\n#EXTINF:10.5,\na.ts\n#EXTINF:10.25,\nb.ts\n" + (endList ? "#EXT-X-ENDLIST\n" : "");
        Assert.True(HlsManifestParser.TryParse(text, source, out var info));
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        var total = HlsDownloadPolicy.RecordDurationProbeResult(cache, source, new(true, false, info, null));
        Assert.Equal(endList ? 20.75 : (double?)null, total);
        var prepared = new VideoGrabber.Core.Downloads.PreparedDownload(source, null, null, null, null,
            null, null, true, true, "video", info!.DurationSeconds, null);
        var request = VideoGrabber.Core.Downloads.DownloadRequestFactory.Create(new(source, "best", false, "output", null, 0), prepared);
        Assert.Equal(endList ? 20.75 : (double?)null, request.ExpectedDurationSeconds);
    }

    [Fact]
    public void Media_playlist_sums_EXTINF_duration()
    {
        const string text = """
        #EXTM3U
        #EXT-X-TARGETDURATION:11
        #EXTINF:10.5,
        a.ts
        #EXTINF:20.25,
        b.ts
        #EXT-X-ENDLIST
        """;
        Assert.True(HlsManifestParser.TryParse(text, new Uri("https://cdn.example/media.m3u8"), out var info));
        Assert.NotNull(info);
        Assert.False(info!.IsMaster);
        Assert.NotNull(info.DurationSeconds);
        Assert.Equal(30.75, info.DurationSeconds.Value, precision: 2);
    }

    [Theory]
    [InlineData(3559, "59:19")]
    [InlineData(3723, "1:02:03")]
    public void Candidate_label_includes_compact_duration(double seconds, string expected)
    {
        var manifest = new HlsManifestInfo(true,
            [new HlsVariant(new Uri("https://cdn.example/720.m3u8"), 1280, 720, 1_000_000, 30, null)],
            [], false, null, false, DurationSeconds: seconds);
        var candidate = new MediaCandidate(
            new Uri("https://cdn.example/master.m3u8"),
            new Uri("https://school.example/lesson"),
            "HLS",
            HlsManifest: manifest,
            PageOrdinal: 3,
            PageSectionTitle: "Часть 3");
        var metadata = new BrowserPageMetadata("День 1", ["Часть 1", "Часть 2", "Часть 3"]);

        var label = MediaCandidatePresentation.DisplayName(candidate, 3, metadata);

        Assert.Contains(expected, label, StringComparison.Ordinal);
        Assert.Contains("720p", label, StringComparison.Ordinal);
    }
}

public sealed class HlsDurationWiringTests
{
    [Fact]
    public void App_enriches_master_candidates_with_media_playlist_duration()
    {
        var root = FindRepoRoot();
        var preflight = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.HlsPreflight.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("EnrichMediaDurationAsync", preflight);
        Assert.Contains("EnrichMediaDurationAsync", devtools);
    }
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src", "VideoGrabber.App"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
