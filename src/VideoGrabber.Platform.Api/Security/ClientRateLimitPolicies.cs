using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace VideoGrabber.Platform.Api.Security;

public static class ClientRateLimitPolicies
{
    public static void ConfigureAuthPolicies(RateLimiterOptions options)
    {
        AddCallerPolicy(options, "auth", 20);
        AddCallerPolicy(options, "auth-config", 60);
        AddCallerPolicy(options, "auth-desktop-poll", 120);
    }

    private static void AddCallerPolicy(RateLimiterOptions options, string name, int permits)
        => options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            CallerKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    public static string CallerKey(HttpContext context)
    {
        var account = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("account_id")?.Value : null;
        if (Guid.TryParse(account, out var accountId)) return "account:" + accountId.ToString("N");
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        return "ip:" + (address?.ToString() ?? "unknown");
    }

    public static void ConfigureProxyOptions(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 2;
        var configured = (configuration["VG_TRUSTED_PROXY_IPS"] ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (configured.Length == 0) return; // Keep the framework's loopback-only trust defaults.
        var proxies = configured.Select(value => IPAddress.TryParse(value, out var ip)
            ? ip : throw new InvalidOperationException("Trusted proxy entries must be IP addresses.")).ToArray();
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var proxy in proxies) options.KnownProxies.Add(proxy);
    }
}
