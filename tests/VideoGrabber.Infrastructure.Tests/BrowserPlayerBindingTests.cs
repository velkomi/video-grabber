using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserPlayerBindingTests
{
    private static MediaCandidate Master(string frameId) => new(
        new Uri("https://cdn.example/master.m3u8"),
        new Uri("https://school.example/lesson"),
        "HLS",
        HlsManifest: new HlsManifestInfo(true,
        [
            new(new Uri("https://cdn.example/360.m3u8"), 640, 360, 500_000, 30, null),
            new(new Uri("https://cdn.example/720.m3u8"), 1280, 720, 1_500_000, 30, null)
        ], [], false, null, false),
        FrameId: frameId);

    [Fact]
    public void Frame_id_and_exact_dom_url_bind_master_to_real_page_part()
    {
        const string json = """
        {"frameTree":{"frame":{"id":"root","url":"https://school.example/lesson"},"childFrames":[
          {"frame":{"id":"part-1","url":"https://api1.gcvh.ru/sign-player/?one=1"}},
          {"frame":{"id":"analytics","url":"https://mc.yandex.com/watch/1"}},
          {"frame":{"id":"part-2","url":"https://api2.gcvh.ru/sign-player/?two=1"}}
        ]}}
        """;
        Assert.True(DevToolsFrameTreeParser.TryParse(json, out var frames));
        var metadata = new BrowserPageMetadata("День 1", ["Часть 1", "Часть 2"],
            [new BrowserPlayerSlot(1, "Часть 1", new Uri("https://api1.gcvh.ru/sign-player/?one=1")),
             new BrowserPlayerSlot(2, "Часть 2", new Uri("https://api2.gcvh.ru/sign-player/?two=1"))]);

        var bound = BrowserFrameBindingResolver.Bind(Master("part-2"), frames!, metadata);

        Assert.Equal(2, bound.PageOrdinal);
        Assert.Equal("Часть 2", bound.PageSectionTitle);
        Assert.Contains("Видео 02", MediaCandidatePresentation.DisplayName(bound, 1, metadata));
        Assert.Contains("Часть 2", MediaCandidatePresentation.DisplayName(bound, 1, metadata));
    }

    [Fact]
    public void Bound_candidate_is_inserted_before_later_page_part_even_if_discovered_late()
    {
        var metadata = new BrowserPageMetadata("День 1", ["Часть 1", "Часть 2"]);
        var part2 = Master("b") with { PageOrdinal = 2, PageSectionTitle = "Часть 2" };
        var part1 = Master("a") with { Source = new Uri("https://cdn.example/other.m3u8"), PageOrdinal = 1, PageSectionTitle = "Часть 1" };
        Assert.Equal(0, MediaCandidatePresentation.GetInsertIndex(part1, [part2]));
    }

    [Fact]
    public void Hls_response_parser_keeps_frame_id()
    {
        const string json = """
        {"requestId":"77","frameId":"frame-5","response":{"status":200,"url":"https://api1.gcvh.ru/master.m3u8","mimeType":"application/vnd.apple.mpegurl"}}
        """;
        Assert.True(DevToolsHlsSnifferParser.TryParseHlsResponse(json, out var response));
        Assert.Equal("frame-5", response!.FrameId);
    }
}
