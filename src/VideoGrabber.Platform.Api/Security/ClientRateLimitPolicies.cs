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
        AddCallerPolicy(options, "support-challenge", 6);
        AddCallerPolicy(options, "support-submit", 10);
        AddCallerPolicy(options, "csp-report", 10);
        var concurrency = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path.Value ?? "";
            if (path == "/v1/security/csp-report")
                return RateLimitPartition.GetConcurrencyLimiter("csp-cap", _ => new ConcurrencyLimiterOptions
                { PermitLimit = 4, QueueLimit = 0 });
            if (path.StartsWith("/v1/support/", StringComparison.Ordinal))
                return RateLimitPartition.GetConcurrencyLimiter("support-cap", _ => new ConcurrencyLimiterOptions
                { PermitLimit = 8, QueueLimit = 0 });
            if (path.StartsWith("/v1/auth/", StringComparison.Ordinal) || path == "/v1/telegram/session")
                return RateLimitPartition.GetConcurrencyLimiter("auth-cap", _ => new ConcurrencyLimiterOptions
                { PermitLimit = 32, QueueLimit = 0 });
            return RateLimitPartition.GetNoLimiter("other");
        });
        var supportIpCeiling = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path.Value ?? "";
            if (!path.StartsWith("/v1/support/", StringComparison.Ordinal)) return RateLimitPartition.GetNoLimiter("other");
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(path + ":" + ip, _ => new FixedWindowRateLimiterOptions
            { PermitLimit = path.EndsWith("challenge", StringComparison.Ordinal) ? 60 : 120,
              Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
        });
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(concurrency, supportIpCeiling);
    }

    private static void AddCallerPolicy(RateLimiterOptions options, string name, int permits)
        => options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            name.StartsWith("support-", StringComparison.Ordinal)
                ? context.RequestServices.GetRequiredService<VideoGrabber.Platform.Api.Support.SupportGuestIdentity>().CallerKey(context)
                : CallerKey(context), _ => new FixedWindowRateLimiterOptions
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
