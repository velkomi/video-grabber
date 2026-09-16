using System.Net;
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
    [InlineData("http://[::ffff:127.0.0.1]/a")]
    [InlineData("http://[fd00::1]/a")]
    public void TryValidate_rejects_unsafe_or_invalid_urls(string value)
    {
        Assert.False(UrlPolicy.TryValidate(value, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("169.254.10.20", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    public void Public_address_policy_is_normalized(string value, bool expected)
    {
        Assert.Equal(expected, UrlPolicy.IsPublicAddress(IPAddress.Parse(value)));
    }
}
