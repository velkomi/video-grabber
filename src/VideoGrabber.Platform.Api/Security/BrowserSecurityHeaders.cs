namespace VideoGrabber.Platform.Api.Security;

public static class BrowserSecurityHeaders
{
    public static void UseBrowserSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                if (context.Request.IsHttps) headers["Strict-Transport-Security"] = "max-age=86400";
                var telegram = context.Request.Path.StartsWithSegments("/miniapp");
                var ancestors = telegram ? "'self' https://web.telegram.org https://*.telegram.org" : "'self'";
                // Report-only rollout measures existing WebGL/auth/Telegram flows before enforcement.
                headers["Content-Security-Policy-Report-Only"] =
                    "default-src 'self'; base-uri 'self'; object-src 'none'; " +
                    "script-src 'self' 'unsafe-inline' https://telegram.org https://esm.sh https://accounts.google.com; " +
                    "style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; " +
                    "font-src 'self' data:; connect-src 'self' https://*.supabase.co https://esm.sh https://telegram.org https://www.googleapis.com https://accounts.google.com; " +
                    "worker-src 'self' blob:; frame-src 'self' https://accounts.google.com https://telegram.org https://web.telegram.org; " +
                    "frame-ancestors " + ancestors + "; form-action 'self' https://accounts.google.com; " +
                    "report-uri /v1/security/csp-report";
                return Task.CompletedTask;
            });
            await next();
        });
    }

    public static void MapCspReports(this WebApplication app)
    {
        app.MapPost("/v1/security/csp-report", async (HttpContext context, ILoggerFactory loggerFactory) =>
        {
            const int maximumBytes = 16384;
            if (context.Request.ContentLength > maximumBytes) return Results.StatusCode(413);
            var buffer = new byte[1024];
            var total = 0;
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                total += count;
                if (total > maximumBytes) return Results.StatusCode(413);
            }
            // Reports can contain URLs, query credentials and customer text. Retain a counter only.
            loggerFactory.CreateLogger("BrowserCsp").LogInformation("CSP report received; content discarded");
            return Results.NoContent();
        }).RequireRateLimiting("csp-report");
    }
}
