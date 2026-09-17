using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Tests;

public sealed record TestAccount(Guid Id, HttpClient Client);

public sealed class ApiFixture : IAsyncDisposable
{
    private readonly NpgsqlDataSource _apiDataSource;
    private readonly NpgsqlDataSource _identityDataSource;
    private PlatformApiFactory _factory;

    private ApiFixture(
        NpgsqlDataSource database,
        NpgsqlDataSource apiDataSource,
        NpgsqlDataSource identityDataSource,
        AdjustableTimeProvider clock,
        BrokerEmulator broker)
    {
        Database = database;
        _apiDataSource = apiDataSource;
        _identityDataSource = identityDataSource;
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
    public System.Collections.Concurrent.ConcurrentQueue<string> Logs { get; } = new();
    public HttpClient Anonymous { get; private set; }

    public static async Task<ApiFixture> StartAsync()
    {
        var dsn = Environment.GetEnvironmentVariable("VG_TEST_POSTGRES_DSN");
        if (string.IsNullOrWhiteSpace(dsn))
            throw new InvalidOperationException("VG_TEST_POSTGRES_DSN is required for Platform tests.");
        var baseBuilder = new NpgsqlConnectionStringBuilder(dsn);
        ValidateTestTarget(baseBuilder);

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
        await MigrationRunner.ApplyAsync(database, CancellationToken.None);

        var apiDataSource = NpgsqlDataSource.Create(RoleDsn(baseBuilder, "vg_api"));
        var identityDataSource = NpgsqlDataSource.Create(RoleDsn(baseBuilder, "vg_identity"));
        return new ApiFixture(database, apiDataSource, identityDataSource,
            new AdjustableTimeProvider(), new BrokerEmulator());
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

    public async Task<HttpClient> AdminAsync()
    {
        var account = await AccountAsync("email", "admin-" + Guid.NewGuid().ToString("N"));
        await using var connection = await Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "update licensing.accounts set base_role='owner_admin' where account_id=@id", connection);
        command.Parameters.AddWithValue("id", account.Id);
        await command.ExecuteNonQueryAsync();
        account.Client.DefaultRequestHeaders.Add("X-Test-Mfa", "fresh");
        return account.Client;
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
        await _identityDataSource.DisposeAsync();
        await _apiDataSource.DisposeAsync();
        await Database.DisposeAsync();
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
        => new(_apiDataSource, _identityDataSource, Clock, Broker, Logs);

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
        return builder.ConnectionString;
    }
}

internal sealed class PlatformApiFactory(
    NpgsqlDataSource apiDataSource,
    NpgsqlDataSource identityDataSource,
    AdjustableTimeProvider clock,
    BrokerEmulator broker,
    ConcurrentQueue<string> logs) : WebApplicationFactory<Program>
{
    internal const string TestSessionKey = "test-only-videograbber-session-signing-key-2026";

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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddProvider(new CapturingLoggerProvider(logs)); });
        builder.UseSetting("VG_PLATFORM_SESSION_SIGNING_KEY", TestSessionKey);
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
