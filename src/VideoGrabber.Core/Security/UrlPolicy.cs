using System.Net;

namespace VideoGrabber.Core.Security;

public static class UrlPolicy
{
    public static bool TryValidate(string? value, out Uri? uri, out string error)
    {
        uri = null;
        error = string.Empty;

        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(parsed.UserInfo))
        {
            error = "Введите полную ссылку http:// или https://.";
            return false;
        }

        if (parsed.IsLoopback || parsed.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(parsed.Host, out var address) && !IsPublicAddress(address)))
        {
            error = "Локальные и приватные сетевые адреса не поддерживаются.";
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 16)
        {
            // Public unicast is 2000::/3; documentation 2001:db8::/32 is not routable.
            if ((bytes[0] & 0xe0) != 0x20) return false;
            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)
                return false;
            return true;
        }

        if (bytes.Length != 4) return false;
        return bytes[0] is > 0 and < 224
            && bytes[0] != 10
            && bytes[0] != 127
            && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            && !(bytes[0] == 169 && bytes[1] == 254)
            && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            && !(bytes[0] == 192 && (bytes[1] == 168 || bytes[1] == 0 || bytes[1] == 2))
            && !(bytes[0] == 198 && (bytes[1] is 18 or 19 || bytes[1] == 51 && bytes[2] == 100))
            && !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }
}
