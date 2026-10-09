using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Platform.Api.Auth;

namespace VideoGrabber.Platform.Api.Support;

public sealed class SupportGuestIdentity(SessionJwtOptions session, TimeProvider clock)
{
    private const string CookieName = "vg_support_guest";
    private readonly byte[] _key = HMACSHA256.HashData(session.SigningKey, Encoding.UTF8.GetBytes("VideoGrabber.support.guest.v1"));

    public string CallerKey(HttpContext http)
    {
        if (http.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var account)) return "account:" + account.ToString("N");
        if (http.Items.TryGetValue(CookieName, out var cached)) return (string)cached!;
        var value = http.Request.Cookies[CookieName];
        if (!Valid(value))
        {
            var content = Guid.NewGuid().ToString("N") + "." + clock.GetUtcNow().AddDays(1).ToUnixTimeSeconds();
            value = content + "." + Sign(content);
            http.Response.Cookies.Append(CookieName, value, new CookieOptions
            { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/v1/support", MaxAge = TimeSpan.FromDays(1) });
        }
        var key = "guest:" + value![..32];
        http.Items[CookieName] = key;
        return key;
    }

    private bool Valid(string? value)
    {
        if (value is null || value.Length > 130) return false;
        var fields = value.Split('.');
        if (fields.Length != 3 || !Guid.TryParseExact(fields[0], "N", out _) || !long.TryParse(fields[1], out var expires)
            || expires < clock.GetUtcNow().ToUnixTimeSeconds() || expires > clock.GetUtcNow().AddDays(1).ToUnixTimeSeconds()) return false;
        var expected = Encoding.ASCII.GetBytes(Sign(fields[0] + "." + fields[1]));
        var provided = Encoding.ASCII.GetBytes(fields[2]);
        return expected.Length == provided.Length && CryptographicOperations.FixedTimeEquals(expected, provided);
    }
    private string Sign(string content) => Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(content)));
}
