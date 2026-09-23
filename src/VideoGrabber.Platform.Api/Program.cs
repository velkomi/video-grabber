using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VideoGrabber.Platform.Api.Accounts;
using VideoGrabber.Platform.Api.Admin;
using VideoGrabber.Platform.Api.Access;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Api.Jobs;
using VideoGrabber.Platform.Api.Payments;
using VideoGrabber.Platform.Api.Operations;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;
using VideoGrabber.Platform.Core.Operations;
using VideoGrabber.Platform.Persistence;

if (args.Length > 0 && string.Equals(args[0], "--validate-stage-config", StringComparison.Ordinal))
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("StageConfigPath");
        Environment.ExitCode = 1;
        return;
    }
    try
    {
        var config = DeploymentConfiguration.Load(args[1]);
        var errors = DeploymentConfiguration.Validate(config);
        if (errors.Count == 0)
        {
            Console.WriteLine("PASS");
            Environment.ExitCode = 0;
        }
        else
        {
            foreach (var field in errors) Console.Error.WriteLine(field);
            Environment.ExitCode = 1;
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine("StageConfig");
        Environment.ExitCode = 1;
    }
    return;
}
if (args.Length > 0 && string.Equals(args[0], "--migrate-platform", StringComparison.Ordinal))
{
    var migrationDsn = Environment.GetEnvironmentVariable("VG_PLATFORM_MIGRATION_DSN");
    if (string.IsNullOrWhiteSpace(migrationDsn))
    {
        Console.Error.WriteLine("MigrationDsn");
        Environment.ExitCode = 1;
        return;
    }
    await using var migrationDataSource = NpgsqlDataSource.Create(migrationDsn);
    await MigrationRunner.ApplyAsync(migrationDataSource, CancellationToken.None);
    Console.WriteLine("PASS");
    return;
}
if (args.Length > 0 && string.Equals(args[0], "--consistency-report", StringComparison.Ordinal))
{
    var consistencyDsn = Environment.GetEnvironmentVariable("VG_PLATFORM_CONSISTENCY_DSN");
    if (string.IsNullOrWhiteSpace(consistencyDsn))
    {
        Console.Error.WriteLine("ConsistencyDsn");
        Environment.ExitCode = 1;
        return;
    }
    await using var consistencyDataSource = NpgsqlDataSource.Create(consistencyDsn);
    var report = await ConsistencyReport.RunAsync(consistencyDataSource, CancellationToken.None);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report));
    Environment.ExitCode = report.Values.All(value => value == 0) ? 0 : 2;
    return;
}
var builder = WebApplication.CreateBuilder(args);
// ASP.NET Hosting.Diagnostics logs the raw request target, including query strings.
// Signed URLs/OAuth-like query values must never be copied into ordinary request logs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
var sessionJwt = SessionJwtOptions.FromConfiguration(builder.Configuration);
var telegramSecurity = TelegramSecurityOptions.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(sessionJwt);
builder.Services.AddSingleton(telegramSecurity);
builder.Services.AddSingleton<IMiniAppAssertionValidator>(_ => new MiniAppAssertionValidator(telegramSecurity.BotToken));
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
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token)
                    && context.Request.Cookies.TryGetValue("vg_session", out var cookieToken)
                    && !string.IsNullOrWhiteSpace(cookieToken))
                    context.Token = cookieToken;
                return Task.CompletedTask;
            }
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
builder.Services.AddHttpClient("TelegramBotApi").RemoveAllLoggers();
builder.Services.AddHttpClient("SupabaseAuth").RemoveAllLoggers();
builder.Services.AddHttpClient("YooKassa").RemoveAllLoggers();
builder.Services.AddHttpClient("OperationsAlerts").RemoveAllLoggers();
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
builder.Services.AddSingleton<SupabaseAuthService>();
builder.Services.AddSingleton<DesktopAuthHandoffStore>();
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
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var ledgerDsn = configuration.GetConnectionString("PlatformLedger")
        ?? configuration["VG_PLATFORM_LEDGER_DSN"]
        ?? throw new InvalidOperationException("Platform ledger database DSN is not configured.");
    return CreditLedger.CreateOwned(ledgerDsn, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddSingleton<JobStore>();
builder.Services.AddSingleton<PaymentStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    PaymentCatalog? catalog = null;
    var catalogPath = configuration["VG_PAYMENT_CATALOG_PATH"];
    if (!string.IsNullOrWhiteSpace(catalogPath))
        catalog = PaymentCatalog.Load(catalogPath);
    return new PaymentStore(
        sp.GetRequiredService<CreditLedger>(),
        sp.GetRequiredService<TimeProvider>(),
        catalog);
});
builder.Services.AddSingleton<SubscriptionStore>();
builder.Services.AddSingleton<SubscriptionService>();
builder.Services.AddSingleton<StarsPaymentAdapter>();
builder.Services.AddSingleton<YooKassaPaymentAdapter>();
builder.Services.AddSingleton<StarsUpdateHandler>();
builder.Services.AddHostedService<PaymentReconciliationWorker>();
builder.Services.AddSingleton<EgressProxy>();
builder.Services.AddSingleton<SourceAnalysisService>();
builder.Services.AddSingleton<ArtifactUploadService>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var deviceDsn = configuration.GetConnectionString("PlatformDevice")
        ?? configuration["VG_PLATFORM_DEVICE_DSN"]
        ?? throw new InvalidOperationException("Platform device database DSN is not configured.");
    return DeviceStore.CreateOwned(deviceDsn, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddSingleton(sp =>
    LeaseSigningKeyOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<OfflineLeaseService>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var adminDsn = configuration.GetConnectionString("PlatformAdmin")
        ?? configuration["VG_PLATFORM_ADMIN_DSN"]
        ?? throw new InvalidOperationException("Platform admin database DSN is not configured.");
    return AdminService.CreateOwned(adminDsn, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddSingleton<IdentityLinkService>();
builder.Services.AddSingleton<IIdentityAccountResolver, IdentityAccountResolver>();
builder.Services.AddSingleton(sp => new TelegramUpdateInbox(
    sp.GetRequiredService<NpgsqlDataSource>(), telegramSecurity.InboxEncryptionKey,
    sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<ITelegramUpdateInbox>(sp => sp.GetRequiredService<TelegramUpdateInbox>());
builder.Services.AddSingleton<TelegramAssertionStore>();
builder.Services.AddSingleton<ITelegramAccountResolver, TelegramAccountResolver>();
builder.Services.AddSingleton<BotCallbackStore>();
builder.Services.AddSingleton<TelegramAccountLinkService>();
builder.Services.AddSingleton<IBotApiClient, BotApiClient>();
builder.Services.AddSingleton<BotMediaHandler>();
builder.Services.AddSingleton<BotCommandHandler>();
builder.Services.AddSingleton<TelegramInboxWorker>();
builder.Services.AddSingleton<DestinationService>();
builder.Services.AddSingleton<ArtifactDeliveryService>();
builder.Services.AddSingleton<ArtifactRetentionService>();
builder.Services.AddSingleton<OperationsDataSource>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var dsn = configuration.GetConnectionString("PlatformOperations")
        ?? configuration["VG_PLATFORM_OPERATIONS_DSN"]
        ?? throw new InvalidOperationException("Platform operations database DSN is not configured.");
    return OperationsDataSource.CreateOwned(dsn);
});
builder.Services.AddSingleton<PlatformOperationalCounters>();
builder.Services.AddSingleton<PlatformMetrics>();
builder.Services.AddSingleton<IAlertTransport, HttpAlertTransport>();
builder.Services.AddSingleton<PlatformAlertDispatcher>();
builder.Services.AddHostedService<PlatformAlertHostedService>();
builder.Services.AddHostedService<DeliveryWorker>();
builder.Services.AddHostedService<TelegramInboxHostedService>();

var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => value!)
    .Concat((builder.Configuration["VG_PLATFORM_ALLOWED_ORIGINS"] ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .ToHashSet(StringComparer.Ordinal);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/", () => Results.Redirect("/web/", permanent: false));
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
            context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization,Content-Type,X-CSRF-Token,X-VideoGrabber-Api-Version,X-VideoGrabber-Protocol";
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
    var csrfExempt = context.Request.Path.Equals("/v1/telegram/session", StringComparison.Ordinal);
    if (isMutation && !csrfExempt && context.Request.Cookies.ContainsKey("vg_session"))
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
    var legacyVersion = context.Request.Headers["X-VideoGrabber-Api-Version"].ToString();
    var protocolRaw = context.Request.Headers["X-VideoGrabber-Protocol"].ToString();
    var legacyInvalid = !string.IsNullOrWhiteSpace(legacyVersion)
        && legacyVersion != PlatformProtocol.LegacyApiVersion.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    var protocolInvalid = !string.IsNullOrWhiteSpace(protocolRaw)
        && !PlatformProtocol.TryParseSupported(protocolRaw, out _);

    context.Response.Headers["X-VideoGrabber-Protocol-Current"] =
        PlatformProtocol.Current.ToString(System.Globalization.CultureInfo.InvariantCulture);
    context.Response.Headers["X-VideoGrabber-Protocol-Minimum"] =
        PlatformProtocol.MinimumSupported.ToString(System.Globalization.CultureInfo.InvariantCulture);

    if (legacyInvalid || protocolInvalid)
    {
        context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Client upgrade required",
            status = StatusCodes.Status426UpgradeRequired,
            code = "client_upgrade_required",
            currentProtocol = PlatformProtocol.Current,
            minimumProtocol = PlatformProtocol.MinimumSupported
        });
        return;
    }
    await next();
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAdminEndpoints();
app.MapAccountEndpoints();
app.MapAccessEndpoints();
app.MapReservationEndpoints();
app.MapDeviceEndpoints();
app.MapIdentityEndpoints();
app.MapSessionEndpoints();
app.MapTelegramSessionEndpoints();
app.MapTelegramAccountLinkEndpoints();
app.MapTelegramWebhookEndpoints();
app.MapCapabilityEndpoints();
app.MapTelegramAdminLinkEndpoints();
app.MapDestinationEndpoints();
app.MapDeliveryEndpoints();
app.MapRetentionEndpoints();
app.MapPaymentEndpoints();
app.MapSubscriptionEndpoints();
app.MapYooKassaWebhookEndpoints();
app.MapJobEndpoints();
app.MapJobEventEndpoints();
app.MapAttemptEndpoints();
app.MapSourceEndpoints();
app.MapArtifactUploadEndpoints();
app.MapPlatformHealthEndpoints();
app.MapOperationsEndpoints();
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
