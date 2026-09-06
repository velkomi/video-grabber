using VideoGrabber.Core.Security;

namespace VideoGrabber.Core.Tests;

public sealed class UrlPolicyTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://rutube.ru/video/example/")]
    [InlineData("https://www.1tv.ru/shows/example")]
    [InlineData("https://cdn.example.org/master.m3u8")]
    public void TryValidate_accepts_public_http_urls(string value)
    {
        Assert.True(UrlPolicy.TryValidate(value, out var uri, out _));
        Assert.NotNull(uri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("file:///C:/video.mp4")]
    [InlineData("http://localhost/video")]
    [InlineData("http://192.168.1.10/video")]
    public void TryValidate_rejects_unsafe_or_invalid_urls(string value)
    {
        Assert.False(UrlPolicy.TryValidate(value, out _, out var error));
        Assert.NotEmpty(error);
    }
}

