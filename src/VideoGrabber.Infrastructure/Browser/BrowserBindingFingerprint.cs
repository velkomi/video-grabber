using System.Security.Cryptography;
using System.Text;

namespace VideoGrabber.Infrastructure.Browser;

public static class BrowserBindingFingerprint
{
    public static string Describe(Uri? uri)
    {
        if (uri is null) return "none";
        var queryHash = string.IsNullOrEmpty(uri.Query) ? "none" : Hash(uri.Query);
        return $"{uri.IdnHost}{uri.AbsolutePath} q={queryHash}";
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 5));
    }
}
