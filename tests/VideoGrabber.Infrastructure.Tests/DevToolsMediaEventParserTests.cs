using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DevToolsMediaEventParserTests
{
    private static readonly Uri Lesson = new("https://iglyrazuma.ru/pl/teach/control/lesson/view?id=1");

    [Fact]
    public void Response_event_discovers_signed_HLS_without_logging_assumptions()
    {
        const string json = """
        {"requestId":"1","type":"Media","response":{"url":"https://gc79.vhcdn.com/path/master.m3u8?token=secret","status":200,"mimeType":"application/vnd.apple.mpegurl"}}
        """;
        Assert.True(DevToolsMediaEventParser.TryParseResponse(json, Lesson, out var candidate));
        Assert.Equal("HLS", candidate!.Kind);
        Assert.Equal("gc79.vhcdn.com", candidate.Source.IdnHost);
        Assert.Equal("token=secret", candidate.Source.Query.TrimStart('?'));
        Assert.DoesNotContain("secret", candidate.DisplayName);
    }

    [Fact]
    public void Request_event_uses_safe_referer_and_discovers_direct_MP4()
    {
        const string json = """
        {"requestId":"2","type":"Media","request":{"url":"https://cdn.example.com/video?id=7","headers":{"Referer":"https://iglyrazuma.ru/lesson"}}}
        """;
        Assert.True(DevToolsMediaEventParser.TryParseRequest(json, Lesson, out var candidate));
        Assert.Equal("Media", candidate!.Kind);
        Assert.Equal("https://iglyrazuma.ru/lesson", candidate.Referer.AbsoluteUri);
    }

    [Theory]
    [InlineData("{\"type\":\"XHR\",\"response\":{\"url\":\"https://mc.yandex.com/watch\",\"status\":200,\"mimeType\":\"application/json\"}}")]
    [InlineData("{\"type\":\"Media\",\"response\":{\"url\":\"https://cdn.example.com/video\",\"status\":403,\"mimeType\":\"video/mp4\"}}")]
    public void Response_event_rejects_non_media_or_failed_responses(string json)
        => Assert.False(DevToolsMediaEventParser.TryParseResponse(json, Lesson, out _));

    [Fact]
    public void Malformed_event_is_rejected_without_throwing()
        => Assert.False(DevToolsMediaEventParser.TryParseResponse("{broken", Lesson, out _));
}

public sealed class DevToolsMediaSummaryTests
{
    [Fact]
    public void Safe_response_summary_strips_query_and_flags_media_like_event()
    {
        const string json = """
        {"type":"Media","response":{"url":"https://gc77.vhcdn.com/path/master.m3u8?token=secret","status":206,"mimeType":"application/vnd.apple.mpegurl"}}
        """;
        Assert.True(DevToolsMediaEventParser.TrySummarizeResponse(json, out var summary));
        Assert.True(summary!.IsMediaLike);
        Assert.Equal("Media gc77.vhcdn.com application/vnd.apple.mpegurl HTTP=206", summary.SafeDisplay);
        Assert.DoesNotContain("/path/master.m3u8", summary.SafeDisplay);
        Assert.DoesNotContain("secret", summary.SafeDisplay);
    }
}
