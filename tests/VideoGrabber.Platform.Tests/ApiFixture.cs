using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
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
        AdjustableTimeProvider clock)
    {
        Database = database;
        _apiDataSource = apiDataSource;
        _identityDataSource = identityDataSource;
        Clock = clock;
        _factory = CreateFactory();
        Anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public NpgsqlDataSource Database { get; }
    public AdjustableTimeProvider Clock { get; }
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
        return new ApiFixture(database, apiDataSource, identityDataSource, new AdjustableTimeProvider());
    }

    public async Task<TestAccount> AccountAsync(
        string provider,
        string subject,
        string? verifiedEmail = null)
    {
        var client = CreateIdentityClient(provider, subject, verifiedEmail);
        var response = await client.GetAsync("/v1/me");
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<AccountProfile>()
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
        await _identityDataSource.DisposeAsync();
        await _apiDataSource.DisposeAsync();
        await Database.DisposeAsync();
    }

    private HttpClient CreateIdentityClient(
        string provider,
        string subject,
        string? verifiedEmail)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-Provider", provider);
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        if (!string.IsNullOrWhiteSpace(verifiedEmail))
            client.DefaultRequestHeaders.Add("X-Test-Email", verifiedEmail);
        return client;
    }

    private PlatformApiFactory CreateFactory()
        => new(_apiDataSource, _identityDataSource, Clock);

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
    AdjustableTimeProvider clock) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.RemoveAll<IAccountStore>();
            services.AddSingleton(apiDataSource);
            services.AddSingleton<IAccountStore>(_ => new AccountStore(apiDataSource));
            services.AddSingleton(new TestIdentityResolver(new AccountStore(identityDataSource), clock));
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestIdentity";
                    options.DefaultChallengeScheme = "TestIdentity";
                })
                .AddScheme<AuthenticationSchemeOptions, TestIdentityAuthenticationHandler>(
                    "TestIdentity", _ => { });
        });
    }
}

file sealed class TestIdentityResolver(
    AccountStore store,
    AdjustableTimeProvider clock)
{
    public Task<AccountProfile> ResolveAsync(
        string provider,
        string subject,
        string? email,
        CancellationToken cancellationToken)
        => store.ResolveAsync(new VerifiedIdentity(
            $"https://{provider}.issuer.test",
            provider,
            subject,
            email,
            clock.GetUtcNow(),
            "test"), cancellationToken);
}

file sealed class TestIdentityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TestIdentityResolver resolver)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var provider = Request.Headers["X-Test-Provider"].ToString();
        var subject = Request.Headers["X-Test-Subject"].ToString();
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(subject))
            return AuthenticateResult.NoResult();

        var profile = await resolver.ResolveAsync(
            provider,
            subject,
            Request.Headers["X-Test-Email"].ToString() is { Length: > 0 } email ? email : null,
            Context.RequestAborted);
        var claims = new[]
        {
            new Claim("account_id", profile.AccountId.ToString("D")),
            new Claim(ClaimTypes.NameIdentifier, profile.AccountId.ToString("D"))
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
