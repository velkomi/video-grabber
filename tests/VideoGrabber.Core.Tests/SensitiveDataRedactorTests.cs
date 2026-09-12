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

    [Fact]
    public void Redact_hides_complete_headers_passwords_bearer_and_private_urls()
    {
        const string source = "Cookie: sid=one; prefs=two; third=three\n" +
                              "Authorization: Bearer header-secret\n" +
                              "password=hunter2 --password cli-secret\n" +
                              "https://private.example/course/lesson-7788/master.m3u8?X-Amz-Signature=signed-value&token=query-secret";

        var result = SensitiveDataRedactor.Redact(source);

        Assert.DoesNotContain("one", result);
        Assert.DoesNotContain("two", result);
        Assert.DoesNotContain("three", result);
        Assert.DoesNotContain("header-secret", result);
        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("cli-secret", result);
        Assert.DoesNotContain("lesson-7788", result);
        Assert.DoesNotContain("signed-value", result);
        Assert.DoesNotContain("query-secret", result);
        Assert.Contains("private.example", result);
    }
}
