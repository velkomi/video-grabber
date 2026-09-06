using VideoGrabber.Core.Security;

namespace VideoGrabber.Core.Tests;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void Redact_hides_query_tokens_and_cookie_headers()
    {
        const string source = "https://example.test/v.m3u8?token=secret&x=1 Cookie: session-value";
        var result = SensitiveDataRedactor.Redact(source);

        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("session-value", result);
        Assert.Contains("[СКРЫТО]", result);
    }
}

