using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VideoGrabber.Platform.Api.Accounts;
using VideoGrabber.Platform.Api.Admin;
using VideoGrabber.Platform.Api.Access;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Tests;

public sealed record TestAccount(Guid Id, HttpClient Client);

public sealed class ApiFixture : IAsyncDisposable
{
    private static readonly SemaphoreSlim MigrationGate = new(1, 1);
    private readonly NpgsqlDataSource _apiDataSource;
    private readonly NpgsqlDataSource _identityDataSource;
    private readonly NpgsqlDataSource _adminDataSource;
    private readonly NpgsqlDataSource _ledgerDataSource;
    private readonly NpgsqlDataSource _deviceDataSource;
    private readonly string _clusterConnectionString;
    private readonly string _databaseName;
    private PlatformApiFactory _factory;

    private ApiFixture(
        NpgsqlDataSource database,
        NpgsqlDataSource apiDataSource,
        NpgsqlDataSource identityDataSource,
        NpgsqlDataSource adminDataSource,
        NpgsqlDataSource ledgerDataSource,
        NpgsqlDataSource deviceDataSource,
        string clusterConnectionString,
        string databaseName,
        AdjustableTimeProvider clock,
        BrokerEmulator broker)
    {
        Database = database;
        _apiDataSource = apiDataSource;
        _identityDataSource = identityDataSource;
        _adminDataSource = adminDataSource;
        _ledgerDataSource = ledgerDataSource;
        _deviceDataSource = deviceDataSource;
        _clusterConnectionString = clusterConnectionString;
        _databaseName = databaseName;
        Clock = clock;
        Broker = broker;
        _factory = CreateFactory();
        Anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public NpgsqlDataSource Database { get; }
    public AdjustableTimeProvider Clock { get; }
    public BrokerEmulator Broker { get; }
    public TelegramApiEmulator TelegramApi { get; } = new();
    public System.Collections.Concurrent.ConcurrentQueue<string> Logs { get; } = new();
    public HttpClient Anonymous { get; private set; }

    public static async Task<ApiFixture> StartAsync()
    {
        var dsn = Environment.GetEnvironmentVariable("VG_TEST_POSTGRES_DSN");
        if (string.IsNullOrWhiteSpace(dsn))
            throw new InvalidOperationException("VG_TEST_POSTGRES_DSN is required for Platform tests.");
        var baseBuilder = new NpgsqlConnectionStringBuilder(dsn);
        ValidateTestTarget(baseBuilder);

        var clusterConnectionString = baseBuilder.ConnectionString;
        var databaseName = "vg_test_" + Guid.NewGuid().ToString("N");
        await using (var adminSource = NpgsqlDataSource.Create(baseBuilder.ConnectionString))
        await using (var connection = await adminSource.OpenConnectionAsync())
        await using (var create = new NpgsqlCommand(
            $"create database \"{databaseName}\"", connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        baseBuilder.Database = databaseName;
        var database = NpgsqlDataSource.Create(baseBuilder.ConnectionString);
        await MigrationGate.WaitAsync();
        try { await MigrationRunner.ApplyAsync(database, CancellationToken.None); }
        finally { MigrationGate.Release(); }

        var apiDataSource = NpgsqlDataSource.Create(RoleDsn(baseBuilder, "vg_api"));
        var identityDataSource = NpgsqlDataSource.Create(RoleDsn(baseBuilder, "vg_identity"));
        var adminDataSource = NpgsqlDataSource.Create(RoleDsn(baseBuilder, "vg_admin"));
        var ledgerDataSource = NpgsqlDataSource.Create(RoleDsn(baseBuilder, "vg_ledger"));
        var deviceDataSource = NpgsqlDataSource.Create(RoleDsn(baseBuilder, "vg_device"));
        return new ApiFixture(database, apiDataSource, identityDataSource, adminDataSource, ledgerDataSource, deviceDataSource,
            clusterConnectionString, databaseName, new AdjustableTimeProvider(), new BrokerEmulator());
    }

    public async Task<TestAccount> AccountAsync(
        string provider,
        string subject,
        string? verifiedEmail = null)
    {
        const string verifier = "fixture-account-verifier-0123456789";
        var begin = await Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn(provider, new Uri("https://client.example.test/auth/complete"), Pkce(verifier)));
        begin.EnsureSuccessStatusCode();
        var started = await begin.Content.ReadFromJsonAsync<SignInStart>()
            ?? throw new InvalidDataException("Sign-in start response was empty.");
        var state = QueryValue(started.AuthorizationUri, "state");
        var nonce = QueryValue(started.AuthorizationUri, "nonce");
        var code = Broker.RegisterCode(Partition(provider), subject, nonce,
            Clock.GetUtcNow().AddMinutes(5), verifiedEmail);
        var completed = await Anonymous.PostAsJsonAsync("/v1/auth/complete",
            new CompleteSignIn(started.FlowId, code, state, verifier));
        completed.EnsureSuccessStatusCode();
        var session = await completed.Content.ReadFromJsonAsync<ApiSession>()
            ?? throw new InvalidDataException("API session response was empty.");
        var client = SessionClient(session);
        var profile = await client.GetFromJsonAsync<AccountProfile>("/v1/me")
            ?? throw new InvalidDataException("Profile response was empty.");
        return new TestAccount(profile.AccountId, client);
    }

    public async Task PromoteAdminAsync(Guid accountId)
    {
        await using var connection = await Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "update licensing.accounts set base_role='owner_admin' where account_id=@id", connection);
        command.Parameters.AddWithValue("id", accountId);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("Test admin account was not found.");
    }

    public HttpClient FreshMfaClient(Guid accountId)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        ApplyFreshMfa(client, accountId);
        return client;
    }
    public async Task<HttpClient> AdminAsync(bool freshMfa = true)
    {
        var account = await AccountAsync("email", "admin-" + Guid.NewGuid().ToString("N"));
        await using var connection = await Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "update licensing.accounts set base_role='owner_admin' where account_id=@id", connection);
        command.Parameters.AddWithValue("id", account.Id);
        await command.ExecuteNonQueryAsync();
        if (freshMfa) ApplyFreshMfa(account.Client, account.Id);
        return account.Client;
    }

    private void ApplyFreshMfa(HttpClient client, Guid accountId)
    {
        var now = Clock.GetUtcNow();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PlatformApiFactory.TestSessionKey));
        var token = new JwtSecurityToken("videograbber-platform", "videograbber-api",
            [new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString("D")),
             new Claim("account_id", accountId.ToString("D")),
             new Claim("sid", Guid.NewGuid().ToString("D")),
             new Claim("mfa_at", now.ToUnixTimeSeconds().ToString())],
            now.UtcDateTime, now.AddMinutes(5).UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    public Task RestartAsync()
    {
        Anonymous.Dispose();
        _factory.Dispose();
        _factory = CreateFactory();
        Anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Anonymous.Dispose();
        _factory.Dispose();
        Broker.Dispose();
        await _deviceDataSource.DisposeAsync();
        await _ledgerDataSource.DisposeAsync();
        await _adminDataSource.DisposeAsync();
        await _identityDataSource.DisposeAsync();
        await _apiDataSource.DisposeAsync();
        await Database.DisposeAsync();
        await DropDatabaseAsync(_clusterConnectionString, _databaseName);
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using var source = NpgsqlDataSource.Create(builder.ConnectionString);
        await using var connection = await source.OpenConnectionAsync();
        var quoted = "\"" + databaseName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        await using var command = new NpgsqlCommand($"drop database if exists {quoted} with (force)", connection);
        await command.ExecuteNonQueryAsync();
    }

    public HttpClient TelegramWebClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    public async Task<HttpClient> MiniAppAsync(long userId)
    {
        var client = TelegramWebClient();

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = Clock.GetUtcNow().ToUnixTimeSeconds().ToString(),
            ["query_id"] = "AA-miniapp-" + userId + "-" + Guid.NewGuid().ToString("N"),
            ["user"] = System.Text.Json.JsonSerializer.Serialize(new { id = userId, first_name = "MiniApp" })
        };
        var initData = TelegramAuthTests.SignInitData(fields, TelegramAuthTests.BotToken);
        using var response = await client.PostAsync("/v1/telegram/session",
            new StringContent(initData, Encoding.UTF8, "text/plain"));
        response.EnsureSuccessStatusCode();
        var webSession = await response.Content.ReadFromJsonAsync<TelegramWebSession>()
            ?? throw new InvalidDataException("Telegram web session response was empty.");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", webSession.CsrfToken);
        return client;
    }
    public HttpClient SessionClient(ApiSession session)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    public BrokerPartitionOptions Partition(string provider)
        => PlatformApiFactory.TestPartitions()[provider];

    public T Service<T>() where T : notnull
        => _factory.Services.GetRequiredService<T>();

    private static string Pkce(string verifier)
        => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string QueryValue(Uri uri, string name)
    {
        foreach (var item in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = item.Split('=', 2);
            if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]) == name)
                return Uri.UnescapeDataString(pair[1]);
        }
        throw new InvalidDataException($"Missing query value {name}.");
    }

    private PlatformApiFactory CreateFactory()
        => new(_apiDataSource, _identityDataSource, _adminDataSource, _ledgerDataSource, _deviceDataSource, Clock, Broker, TelegramApi, Logs);

    private static void ValidateTestTarget(NpgsqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Database)
            || !builder.Database.StartsWith("vg_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Platform test database must start with vg_test_.");
        if (builder.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Platform tests require a loopback PostgreSQL host.");
    }

    private static string RoleDsn(NpgsqlConnectionStringBuilder source, string role)
    {
        var builder = new NpgsqlConnectionStringBuilder(source.ConnectionString)
        {
            Options = "-c role=" + role
        };
        if (role == "vg_ledger") builder.MaxPoolSize = 32;
        return builder.ConnectionString;
    }
}

internal sealed class PlatformApiFactory(
    NpgsqlDataSource apiDataSource,
    NpgsqlDataSource identityDataSource,
    NpgsqlDataSource adminDataSource,
    NpgsqlDataSource ledgerDataSource,
    NpgsqlDataSource deviceDataSource,
    AdjustableTimeProvider clock,
    BrokerEmulator broker,
    TelegramApiEmulator telegramApi,
    ConcurrentQueue<string> logs) : WebApplicationFactory<Program>
{
    internal const string TestSessionKey = "test-only-videograbber-session-signing-key-2026";
    private static readonly string TestLeasePrivateKey = CreateTestLeasePrivateKey();

    internal static IReadOnlyDictionary<string, BrokerPartitionOptions> TestPartitions()
        => new[] { "google", "apple", "yandex", "telegram", "email" }
            .ToDictionary(
                provider => provider,
                provider => new BrokerPartitionOptions(
                    provider,
                    new Uri($"https://{provider}.broker.test/"),
                    "videograbber-test",
                    new Uri("https://api.example.test/auth/callback"),
                    provider),
                StringComparer.OrdinalIgnoreCase);
    private static string CreateTestLeasePrivateKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Convert.ToBase64String(key.ExportPkcs8PrivateKey());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Security:AllowedOrigins:0", "https://miniapp.example.test");
        builder.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddProvider(new CapturingLoggerProvider(logs)); });
        builder.UseSetting("VG_PLATFORM_SESSION_SIGNING_KEY", TestSessionKey);
        builder.UseSetting("VG_PLATFORM_LEASE_KEY_ID", "test-lease-key-1");
        builder.UseSetting("VG_PLATFORM_LEASE_SIGNING_KEY_PKCS8", TestLeasePrivateKey);
        builder.UseSetting("VG_TELEGRAM_BOT_TOKEN", "123456789:test-telegram-bot-token-for-local-tests");
        builder.UseSetting("VG_TELEGRAM_WEBHOOK_SECRET", "test-webhook-secret-2026");
        builder.UseSetting("VG_TELEGRAM_INBOX_KEY", Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray()));
        builder.UseSetting("VG_TELEGRAM_MINIAPP_URL", "https://miniapp.example.test/");
        builder.UseSetting("VG_PLATFORM_PUBLIC_URL", "https://platform.example.test/");
        builder.UseSetting("VG_TELEGRAM_BOT_USERNAME", "VideoGrabberTestBot");
        builder.UseSetting("VG_TELEGRAM_WORKER_ENABLED", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<IReadOnlyDictionary<string, BrokerPartitionOptions>>();
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IReadOnlyDictionary<string, BrokerPartitionOptions>>(
                TestPartitions());
            services.RemoveAll<IAccountStore>();
            services.RemoveAll<IIdentityAccountResolver>();
            services.RemoveAll<IBrokerCodeExchange>();
            services.RemoveAll<IBrokerSigningKeySource>();
            services.RemoveAll<IBrokerUserInfoSource>();
            services.AddSingleton(apiDataSource);
            services.AddSingleton<IAccountStore>(_ => new AccountStore(apiDataSource));
            services.AddSingleton<IIdentityAccountResolver>(
                _ => new TestIdentityResolver(new AccountStore(identityDataSource)));
            services.AddSingleton<IBrokerCodeExchange>(broker);
            services.AddSingleton<IBrokerSigningKeySource>(broker);
            services.AddSingleton<IBrokerUserInfoSource>(broker);
            services.RemoveAll<IdentityLinkService>();
            services.AddSingleton(sp => IdentityLinkService.CreateForTesting(
                apiDataSource, identityDataSource, adminDataSource, clock,
                sp.GetRequiredService<IBrokerTokenValidator>(), TestPartitions()));
            services.RemoveAll<GrantStore>();
            services.AddSingleton(sp => GrantStore.CreateForTesting(
                apiDataSource, adminDataSource, sp.GetRequiredService<IAccountStore>(), clock));
            services.RemoveAll<CreditLedger>();
            services.AddSingleton(CreditLedger.CreateForTesting(ledgerDataSource, clock));
            services.RemoveAll<DeviceStore>();
            services.AddSingleton(DeviceStore.CreateForTesting(deviceDataSource, clock));
            services.RemoveAll<AdminService>();
            services.AddSingleton(AdminService.CreateForTesting(adminDataSource, clock));
            services.RemoveAll<ITelegramAccountResolver>();
            services.AddSingleton<ITelegramAccountResolver>(_ => TelegramAccountResolver.CreateForTesting(identityDataSource));
            services.AddHttpClient("TelegramBotApi")
                .ConfigurePrimaryHttpMessageHandler(_ => telegramApi);
        });
    }
}

file sealed class TestIdentityResolver(AccountStore store) : IIdentityAccountResolver
{
    public Task<AccountProfile> ResolveAsync(
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
        => store.ResolveAsync(identity, cancellationToken);
}

internal sealed class CapturingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(logs);
    public void Dispose() { }
}

internal sealed class CapturingLogger(ConcurrentQueue<string> logs) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => logs.Enqueue(formatter(state, exception));
}
