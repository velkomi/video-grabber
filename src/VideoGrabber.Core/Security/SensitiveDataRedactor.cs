using System.Text.RegularExpressions;

namespace VideoGrabber.Core.Security;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        try
        {
            var safe = Headers().Replace(value, "$1: [СКРЫТО]");
            safe = Passwords().Replace(safe, "$1=[СКРЫТО]");
            safe = Bearer().Replace(safe, "Bearer [СКРЫТО]");
            safe = Urls().Replace(safe, match =>
                Uri.TryCreate(match.Value, UriKind.Absolute, out var uri)
                    ? $"{uri.Scheme}://{uri.IdnHost}/[СКРЫТО]"
                    : "[URL СКРЫТО]");
            return QueryValues().Replace(safe, "$1=[СКРЫТО]");
        }
        catch (RegexMatchTimeoutException) { return "[СООБЩЕНИЕ СКРЫТО: превышен лимит обработки]"; }
    }

    [GeneratedRegex(@"(?im)\b(authorization|proxy-authorization|cookie|set-cookie)\s*[:=]\s*[^\r\n]*", RegexOptions.None, 200)]
    private static partial Regex Headers();
    [GeneratedRegex("(?i)(--password|password|passwd|pwd|api[_-]?key|access[_-]?token|refresh[_-]?token)(?:\\s*[:=]\\s*|\\s+)(?:\"[^\"\\r\\n]*\"|'[^'\\r\\n]*'|[^\\s&;]+)", RegexOptions.None, 200)]
    private static partial Regex Passwords();
    [GeneratedRegex(@"(?i)\bBearer\s+[^\s""']+", RegexOptions.None, 200)]
    private static partial Regex Bearer();
    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase, 200)]
    private static partial Regex Urls();
    [GeneratedRegex(@"(?i)(token|key|signature|sig|auth|s|X-Amz-[\w-]+)=([^&\s]+)", RegexOptions.None, 200)]
    private static partial Regex QueryValues();
}
