using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
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
using VideoGrabber.Platform.Api.Promotions;
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
// These HTTP paths contain bot credentials or signed source URLs.
builder.Logging.AddFilter("System.Net.Http.HttpClient.TelegramBotApi", LogLevel.None);
builder.Logging.AddFilter("System.Net.Http.HttpClient.DirectMediaResolver", LogLevel.None);
var sessionJwt = SessionJwtOptions.FromConfiguration(builder.Configuration);
var telegramSecurity = TelegramSecurityOptions.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(sessionJwt);
builder.Services.AddSingleton(telegramSecurity);
builder.Services.AddSingleton<IMiniAppAssertionValidator>(_ =>
{
    var cutoff = builder.Configuration["VG_TELEGRAM_ASSERTION_NOT_BEFORE_UTC"];
    DateTimeOffset? notBefore = null;
    if (!string.IsNullOrWhiteSpace(cutoff))
    {
        if (!DateTimeOffset.TryParse(cutoff, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            throw new InvalidOperationException("Telegram assertion cutoff must be a UTC timestamp.");
        notBefore = parsed.ToUniversalTime();
    }
    return new MiniAppAssertionValidator(telegramSecurity.BotToken, notBefore: notBefore);
});
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    VideoGrabber.Platform.Api.Security.ClientRateLimitPolicies.ConfigureProxyOptions(options, builder.Configuration));
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
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/javascript",
        "text/javascript",
        "text/css",
        "application/json",
        "application/problem+json"
    ]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("consents", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst("account_id")?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit=20, Window=TimeSpan.FromMinutes(1), QueueLimit=0 }));
    options.AddPolicy("promotions", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst("account_id")?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit=30, Window=TimeSpan.FromMinutes(1), QueueLimit=0 }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    VideoGrabber.Platform.Api.Security.ClientRateLimitPolicies.ConfigureAuthPolicies(options);
});
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("TelegramBotApi")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
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
builder.Services.AddSingleton(sp => new PromotionOptions(
    string.Equals(sp.GetRequiredService<IConfiguration>()["VG_REFERRALS_ENABLED"],"true",StringComparison.OrdinalIgnoreCase),
    sp.GetRequiredService<IConfiguration>()["VG_REFERRALS_PUBLIC_ORIGIN"] ?? "https://videograbber.srv1902378.hstgr.cloud",
    sp.GetRequiredService<IConfiguration>()["VG_TELEGRAM_BOT_USERNAME"] ?? "VideoGra_bot"));
builder.Services.AddSingleton<PromotionStore>();
builder.Services.AddSingleton<BrowserDownloadTicketService>();
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
        catalog,
        sp.GetRequiredService<PromotionStore>());
});
builder.Services.AddSingleton<SubscriptionStore>();
builder.Services.AddSingleton<SubscriptionService>();
builder.Services.AddSingleton<StarsPaymentAdapter>();
builder.Services.AddSingleton<YooKassaPaymentAdapter>();
builder.Services.AddSingleton<StarsUpdateHandler>();
builder.Services.AddHostedService<PaymentReconciliationWorker>();
builder.Services.AddSingleton<EgressProxy>();
builder.Services.AddSingleton<SourceAnalysisService>();
builder.Services.AddHttpClient<DirectMediaResolver>(client => client.Timeout = TimeSpan.FromSeconds(12))
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<EgressProxy>().CreatePinnedHandler());
builder.Services.AddSingleton<DirectDownloadService>();
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
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var adminDsn = configuration.GetConnectionString("PlatformAdmin")
        ?? configuration["VG_PLATFORM_ADMIN_DSN"]
        ?? throw new InvalidOperationException("Platform admin database DSN is not configured.");
    return AdminFeatureOverrideService.CreateOwned(
        sp.GetRequiredService<NpgsqlDataSource>(),
        adminDsn,
        sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var adminDsn = configuration.GetConnectionString("PlatformAdmin")
        ?? configuration["VG_PLATFORM_ADMIN_DSN"]
        ?? throw new InvalidOperationException("Platform admin database DSN is not configured.");
    var encryptionKey = configuration["VG_ADMIN_MFA_ENCRYPTION_KEY"]
        ?? throw new InvalidOperationException("Admin MFA encryption key is not configured.");
    return new AdminMfaService(
        adminDsn,
        sp.GetRequiredService<TimeProvider>(),
        sp.GetRequiredService<SessionJwtOptions>(),
        encryptionKey);
});
builder.Services.AddSingleton<OwnerAdminAccessService>();
builder.Services.AddHostedService<OwnerAdminAccessService>(
    sp => sp.GetRequiredService<OwnerAdminAccessService>());
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
builder.Services.AddSingleton<BotInputLimiter>();
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.SupportBotService>();
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

builder.Services.AddSingleton(sp => new ConsentStore(sp.GetRequiredService<CreditLedger>(), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(new VideoGrabber.Platform.Api.ProductInformation.DocumentCatalog(
    builder.Configuration["VG_DOCUMENTS_PATH"] ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "info", "content.json")));
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.SupportOptions>();
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.SupportDataProtector>();
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.SupportChallengeService>();
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.SupportStore>();
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.ISupportNotificationTransport, VideoGrabber.Platform.Api.Support.SupportTelegramTransport>();
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.ISupportNotificationTransport, VideoGrabber.Platform.Api.Support.SupportMailTransport>();
builder.Services.AddSingleton<VideoGrabber.Platform.Api.Support.SupportNotificationWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<VideoGrabber.Platform.Api.Support.SupportNotificationWorker>());
var app = builder.Build();

app.UseForwardedHeaders();
VideoGrabber.Platform.Api.Security.BrowserSecurityHeaders.UseBrowserSecurityHeaders(app);
app.UseResponseCompression();
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
    await RequestTelemetry.InvokeAsync(context, next, app.Logger);
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
            context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization,Content-Type,X-CSRF-Token,X-VideoGrabber-Api-Version,X-VideoGrabber-Protocol,X-Correlation-Id";
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
    var csrfExempt = context.Request.Path.Equals("/v1/telegram/session", StringComparison.Ordinal)
        || context.Request.Path.Equals("/v1/security/csp-report", StringComparison.Ordinal);
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
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (MediaStoragePolicy.ClientOnly(app.Configuration)
        && MediaStoragePolicy.BlocksMediaPath(context.Request.Path.Value ?? "", context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { code = "media_storage_disabled" });
        return;
    }
    await next();
});
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
app.MapPromotionEndpoints();
VideoGrabber.Platform.Api.Support.SupportEndpoints.MapSupportEndpoints(app);
VideoGrabber.Platform.Api.Security.BrowserSecurityHeaders.MapCspReports(app);
VideoGrabber.Platform.Api.ProductInformation.InformationEndpoints.MapInformationEndpoints(app);
VideoGrabber.Platform.Api.ClientUpdates.ClientReleaseEndpoints.MapClientReleaseEndpoints(app);
app.MapSubscriptionEndpoints();
app.MapYooKassaWebhookEndpoints();
app.MapJobEndpoints();
app.MapJobEventEndpoints();
app.MapAttemptEndpoints();
app.MapSourceEndpoints();
app.MapDirectDownloadEndpoints();
app.MapArtifactUploadEndpoints();
app.MapPlatformHealthEndpoints();
app.MapOperationsEndpoints();
app.MapGet("/download/windows", (IConfiguration configuration) =>
    {
        var path = configuration["VG_WINDOWS_SETUP_PATH"]
            ?? "/var/lib/videograbber/downloads/VideoGrabber-Setup.exe";
        if (!File.Exists(path))
            return Results.NotFound(new { code = "windows_setup_unavailable" });
        return Results.File(
            path,
            "application/vnd.microsoft.portable-executable",
            "VideoGrabber-Setup.exe",
            enableRangeProcessing: true);
    })
    .AllowAnonymous();
app.MapGet("/download/windows/portable", (IConfiguration configuration) =>
    {
        var path = configuration["VG_WINDOWS_DOWNLOAD_PATH"]
            ?? "/var/lib/videograbber/downloads/VideoGrabber-Windows.zip";
        if (!File.Exists(path))
            return Results.NotFound(new { code = "windows_download_unavailable" });
        return Results.File(
            path,
            "application/zip",
            "VideoGrabber-Windows.zip",
            enableRangeProcessing: true);
    })
    .AllowAnonymous();
app.MapGet("/download/windows/checksum", (IConfiguration configuration) =>
    {
        var path = configuration["VG_WINDOWS_CHECKSUM_PATH"]
            ?? "/var/lib/videograbber/downloads/VideoGrabber-Windows.sha256.txt";
        if (!File.Exists(path))
            return Results.NotFound(new { code = "windows_checksum_unavailable" });
        return Results.Text(
            File.ReadAllText(path),
            "text/plain; charset=utf-8");
    })
    .AllowAnonymous();
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
