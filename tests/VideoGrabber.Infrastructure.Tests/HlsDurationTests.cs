using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class HlsDurationTests
{
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
