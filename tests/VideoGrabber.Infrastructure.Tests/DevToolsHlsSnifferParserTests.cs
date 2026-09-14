using System.Text.Json;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DevToolsHlsSnifferParserTests
{
    private static readonly Uri Page = new("https://school.example/lesson?id=1");

    [Fact]
    public void Request_context_prefers_referer_and_keeps_signed_source_only_in_memory()
    {
        const string json = """
        {"requestId":"R1","documentURL":"https://school.example/lesson?id=1","type":"XHR","request":{"url":"https://cdn.example/private-secret/master.m3u8?token=secret","headers":{"Referer":"https://player.example/embed?id=9"}}}
        """;
        Assert.True(DevToolsHlsSnifferParser.TryParseRequest(json, Page, out var context));
        Assert.Equal("R1", context!.RequestId);
        Assert.Equal("https://cdn.example/private-secret/master.m3u8?token=secret", context.Source.AbsoluteUri);
        Assert.Equal("https://player.example/embed?id=9", context.Referer.AbsoluteUri);
        Assert.DoesNotContain("secret", context.SafeDisplay);
        Assert.DoesNotContain("private-secret", context.SafeDisplay);
    }

    [Fact]
    public void Request_context_falls_back_to_document_url_then_page()
    {
        const string json = """
        {"requestId":"R2","documentURL":"https://player.example/embed?id=7","request":{"url":"https://cdn.example/x"}}
        """;
        Assert.True(DevToolsHlsSnifferParser.TryParseRequest(json, Page, out var context));
        Assert.Equal("https://player.example/embed?id=7", context!.Referer.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://cdn.example/master.m3u8?x=1", "application/octet-stream", 200)]
    [InlineData("https://cdn.example/manifest?id=7", "application/vnd.apple.mpegurl", 204)]
    [InlineData("https://cdn.example/manifest?id=7", "application/x-mpegurl; charset=utf-8", 206)]
    public void Successful_hls_response_is_detected_by_url_or_mime(string url, string mime, int status)
    {
        var json = JsonSerializer.Serialize(new { requestId = "H1", type = "XHR", response = new { url, status, mimeType = mime } });
        Assert.True(DevToolsHlsSnifferParser.TryParseHlsResponse(json, out var response));
        Assert.Equal("H1", response!.RequestId);
        Assert.Equal(new Uri(url).IdnHost, response.Source.IdnHost);
        Assert.DoesNotContain(response.Source.AbsolutePath, response.SafeDisplay);
    }

    [Theory]
    [InlineData(199, "application/vnd.apple.mpegurl")]
    [InlineData(404, "application/vnd.apple.mpegurl")]
    [InlineData(200, "application/json")]
    public void Non_success_or_non_hls_response_is_rejected(int status, string mime)
    {
        var json = JsonSerializer.Serialize(new { requestId = "H2", response = new { url = "https://cdn.example/manifest", status, mimeType = mime } });
        Assert.False(DevToolsHlsSnifferParser.TryParseHlsResponse(json, out _));
    }
}
