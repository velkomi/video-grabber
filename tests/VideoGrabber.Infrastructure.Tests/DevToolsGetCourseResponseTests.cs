using System.Text;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed partial class DevToolsGetCourseResponseTests
{
    private static readonly Uri Lesson = new("https://iglyrazuma.ru/pl/teach/control/lesson/view?id=1");

    [Fact]
    public void Sign_player_response_keeps_request_id_for_body_lookup()
    {
        const string json = """
        {"requestId":"REQ-7","type":"Document","response":{"url":"https://api2.gcvh.ru/sign-player/?json=secret&s=token","status":200,"mimeType":"text/html"}}
        """;
        Assert.True(DevToolsGetCourseResponseParser.TryParsePlayerResponse(json, Lesson, out var response));
        Assert.Equal("REQ-7", response!.RequestId);
        Assert.Equal("api2.gcvh.ru", response.PlayerUri.IdnHost);
        Assert.Equal(Lesson, response.Referer);
        Assert.DoesNotContain("secret", response.SafeDisplay);
        Assert.DoesNotContain("/sign-player/", response.SafeDisplay);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Response_body_decoder_handles_plain_and_base64(bool encoded)
    {
        const string body = "<script>window.configs={\"masterPlaylistUrl\":\"https://gc77.vhcdn.com/master.m3u8?token=x\"}</script>";
        var payload = encoded ? Convert.ToBase64String(Encoding.UTF8.GetBytes(body)) : body;
        var json = System.Text.Json.JsonSerializer.Serialize(new { body = payload, base64Encoded = encoded });

        Assert.True(DevToolsGetCourseResponseParser.TryDecodeBody(json, out var decoded));
        Assert.Equal(body, decoded);
    }

    [Fact]
    public void Non_player_response_is_rejected()
    {
        const string json = """
        {"requestId":"REQ-8","type":"XHR","response":{"url":"https://mc.yandex.ru/watch","status":200,"mimeType":"application/json"}}
        """;
        Assert.False(DevToolsGetCourseResponseParser.TryParsePlayerResponse(json, Lesson, out _));
    }
}

public sealed partial class DevToolsGetCourseResponseTests
{
    [Fact]
    public void Loading_finished_returns_request_id_only()
    {
        Assert.True(DevToolsGetCourseResponseParser.TryParseLoadingFinished("{\"requestId\":\"42\",\"encodedDataLength\":123}", out var requestId));
        Assert.Equal("42", requestId);
        Assert.False(DevToolsGetCourseResponseParser.TryParseLoadingFinished("{\"requestId\":\"\"}", out _));
    }
}


public sealed partial class DevToolsGetCourseResponseTests
{
    [Fact]
    public void Loading_failed_returns_request_id_only()
    {
        Assert.True(DevToolsGetCourseResponseParser.TryParseLoadingFailed("{\"requestId\":\"FAILED-1\",\"errorText\":\"net::ERR_ABORTED\"}", out var requestId));
        Assert.Equal("FAILED-1", requestId);
    }
}
