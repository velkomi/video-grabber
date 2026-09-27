using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class MediaCandidateGetCourseTests
{
    private static readonly Uri Lesson = new("https://iglyrazuma.ru/pl/teach/control/lesson/view?id=1");

    [Theory]
    [InlineData("https://api1.gcvh.ru/sign-player/?json=x&s=y")]
    [InlineData("https://api3.gcvh.ru/sign-player/?json=x&s=y")]
    [InlineData("https://vhapi02.gcfiles.net/sign-player/?json=x&s=y")]
    [InlineData("https://player02.getcourse.ru/sign-player/?json=x&s=y")]
    [InlineData("https://cf-api-2.vhcdn.com/sign-player/?json=x&s=y")]
    public void Recognizes_current_GetCourse_sign_player_hosts(string url)
    {
        Assert.True(MediaCandidate.TryCreate(url, "text/html", Lesson, out var candidate));
        Assert.Equal("GetCourse", candidate!.Kind);
    }

    [Theory]
    [InlineData("https://kinescope.io/embed/9F6tRu6WyKHdfRWXntm1Fo")]
    [InlineData("https://kinescope.io/embed/abc123?watermark=test%40example.com")]
    public void Recognizes_Kinescope_embed_urls(string url)
    {
        Assert.True(MediaCandidate.TryCreate(url, "text/html", Lesson, out var candidate));
        Assert.Equal("Kinescope", candidate!.Kind);
    }

    [Theory]
    [InlineData("https://api3.gcvh.ru/not-player/?json=x")]
    [InlineData("https://evilgcvh.ru/sign-player/?json=x")]
    [InlineData("https://evil-kinescope.io/embed/abc123")]
    [InlineData("https://kinescope.io/not-embed/abc123")]
    public void Rejects_lookalike_or_wrong_path(string url)
        => Assert.False(MediaCandidate.TryCreate(url, "text/html", Lesson, out _));
}
