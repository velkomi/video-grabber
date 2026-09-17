using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VideoGrabber.Platform.Api.Accounts;
using VideoGrabber.Platform.Api.Access;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

var builder = WebApplication.CreateBuilder(args);
var sessionJwt = SessionJwtOptions.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(sessionJwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = sessionJwt.Issuer,
            ValidateAudience = true,
            ValidAudience = sessionJwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(sessionJwt.SigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IReadOnlyDictionary<string, BrokerPartitionOptions>>(sp =>
    BrokerPartitionConfiguration.Load(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<IBrokerCodeExchange, UnavailableBrokerCodeExchange>();
builder.Services.AddSingleton<IBrokerSigningKeySource, HttpBrokerSigningKeySource>();
builder.Services.AddSingleton<IBrokerUserInfoSource, HttpBrokerUserInfoSource>();
builder.Services.AddSingleton<IBrokerBindingStore>(sp => sp.GetRequiredService<SessionStore>());
builder.Services.AddSingleton<IBrokerTokenValidator, BrokerTokenValidator>();
builder.Services.AddSingleton<ProviderFlow>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var dsn = configuration.GetConnectionString("PlatformApi")
        ?? configuration["VG_PLATFORM_API_DSN"]
        ?? throw new InvalidOperationException("Platform API database DSN is not configured.");
    return NpgsqlDataSource.Create(dsn);
});
builder.Services.AddSingleton<IAccountStore, AccountStore>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var adminDsn = configuration.GetConnectionString("PlatformAdmin")
        ?? configuration["VG_PLATFORM_ADMIN_DSN"]
        ?? throw new InvalidOperationException("Platform admin database DSN is not configured.");
    return GrantStore.CreateOwned(sp.GetRequiredService<NpgsqlDataSource>(), adminDsn,
        sp.GetRequiredService<IAccountStore>(), sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddSingleton<IdentityLinkService>();
builder.Services.AddSingleton<IIdentityAccountResolver, IdentityAccountResolver>();

var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => value!)
    .Concat((builder.Configuration["VG_PLATFORM_ALLOWED_ORIGINS"] ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .ToHashSet(StringComparer.Ordinal);

var app = builder.Build();

app.UseCookiePolicy(new CookiePolicyOptions
{
    HttpOnly = HttpOnlyPolicy.Always,
    Secure = CookieSecurePolicy.Always,
    MinimumSameSitePolicy = SameSiteMode.Strict
});
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].ToString();
    if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
        correlationId = Guid.NewGuid().ToString("N");
    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    await next();
});
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();
    if (!string.IsNullOrWhiteSpace(origin))
    {
        if (!allowedOrigins.Contains(origin))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status403Forbidden, "origin_not_allowed");
            return;
        }
        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        context.Response.Headers.Append("Vary", "Origin");
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,PATCH,DELETE,OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization,Content-Type,X-CSRF-Token,X-VideoGrabber-Api-Version";
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }
    }
    await next();
});

app.Use(async (context, next) =>
{
    var isMutation = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)
        || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
    if (isMutation && context.Request.Cookies.ContainsKey("vg_session"))
    {
        var cookieToken = context.Request.Cookies["vg_csrf"];
        var headerToken = context.Request.Headers["X-CSRF-Token"].ToString();
        if (!FixedTextEquals(cookieToken, headerToken))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status403Forbidden, "csrf_required");
            return;
        }
    }
    await next();
});

app.Use(async (context, next) =>
{
    var version = context.Request.Headers["X-VideoGrabber-Api-Version"].ToString();
    if (!string.IsNullOrWhiteSpace(version) && version != "1")
    {
        context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Client upgrade required",
            status = StatusCodes.Status426UpgradeRequired,
            code = "client_upgrade_required"
        });
        return;
    }
    await next();
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAccountEndpoints();
app.MapAccessEndpoints();
app.MapIdentityEndpoints();
app.MapSessionEndpoints();
app.Run();

static bool FixedTextEquals(string? left, string? right)
{
    if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
    var a = Encoding.UTF8.GetBytes(left);
    var b = Encoding.UTF8.GetBytes(right);
    return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
}

static async Task WriteSecurityErrorAsync(HttpContext context, int status, string code)
{
    context.Response.StatusCode = status;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new { type = "about:blank", title = "Request rejected", status, code });
}

public partial class Program;
