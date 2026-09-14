using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed partial class DevToolsTargetEventParserTests
{
    [Fact]
    public void Attached_iframe_returns_session_and_safe_target_metadata()
    {
        const string json = """
        {"sessionId":"ABC","targetInfo":{"targetId":"T1","type":"iframe","url":"https://api3.gcvh.ru/sign-player/?json=secret&s=token"},"waitingForDebugger":false}
        """;

        Assert.True(DevToolsTargetEventParser.TryParseAttached(json, out var target));
        Assert.Equal("ABC", target!.SessionId);
        Assert.Equal("iframe", target.Type);
        Assert.Equal("api3.gcvh.ru", target.Host);
        Assert.Equal("/sign-player/", target.Path);
        Assert.DoesNotContain("secret", target.SafeDisplay);
        Assert.DoesNotContain("token", target.SafeDisplay);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"sessionId\":\"\",\"targetInfo\":{\"type\":\"iframe\",\"url\":\"https://api3.gcvh.ru/\"}}")]
    [InlineData("{\"sessionId\":\"A\",\"targetInfo\":{\"type\":\"page\",\"url\":\"javascript:alert(1)\"}}")]
    public void Invalid_or_unsafe_target_is_rejected(string json)
        => Assert.False(DevToolsTargetEventParser.TryParseAttached(json, out _));
}

public sealed partial class DevToolsTargetEventParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("about:blank")]
    public void Attached_iframe_without_final_url_is_still_enabled(string url)
    {
        var json = "{\"sessionId\":\"S2\",\"targetInfo\":{\"targetId\":\"T2\",\"type\":\"iframe\",\"url\":\"" + url + "\"}}";
        Assert.True(DevToolsTargetEventParser.TryParseAttached(json, out var target));
        Assert.Equal("S2", target!.SessionId);
        Assert.Equal("iframe", target.Type);
        Assert.Equal(string.Empty, target.Host);
        Assert.Equal(string.Empty, target.Path);
    }
}

public sealed partial class DevToolsTargetEventParserTests
{
    [Fact]
    public void Extracts_session_id_even_for_blob_worker_that_is_not_a_media_target()
    {
        const string json = """
        {"sessionId":"worker-session","targetInfo":{"type":"worker","url":"blob:https://example.test/id"}}
        """;
        Assert.True(DevToolsTargetEventParser.TryParseSessionId(json, out var sessionId));
        Assert.Equal("worker-session", sessionId);
        Assert.False(DevToolsTargetEventParser.TryParseAttached(json, out _));
    }
}
