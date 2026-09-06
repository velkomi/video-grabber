using System.Text.RegularExpressions;

namespace VideoGrabber.Core.Security;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = HeaderSecret().Replace(value, "$1=[СКРЫТО]");
        return QuerySecret().Replace(result, "$1=[СКРЫТО]");
    }

    [GeneratedRegex("(?i)(authorization|cookie|set-cookie)\\s*[:=]\\s*[^\\s;]+")]
    private static partial Regex HeaderSecret();

    [GeneratedRegex("(?i)(token|key|signature|sig|auth)=([^&\\s]+)")]
    private static partial Regex QuerySecret();
}
