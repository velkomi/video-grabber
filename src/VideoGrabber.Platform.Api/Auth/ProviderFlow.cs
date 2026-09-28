using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Auth;

public interface IBrokerCodeExchange
{
    Task<string> ExchangeAsync(
        BrokerPartitionOptions partition,
        string code,
        CancellationToken cancellationToken);
}

public sealed class UnavailableBrokerCodeExchange : IBrokerCodeExchange
{
    public Task<string> ExchangeAsync(
        BrokerPartitionOptions partition,
        string code,
        CancellationToken cancellationToken)
        => throw new UnauthorizedAccessException("Broker code exchange is not configured.");
}

public interface IIdentityAccountResolver
{
    Task<AccountProfile> ResolveAsync(VerifiedIdentity identity, CancellationToken cancellationToken);
}

public sealed class IdentityAccountResolver : IIdentityAccountResolver, IAsyncDisposable
{
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly AccountStore _store;

    public IdentityAccountResolver(
        IConfiguration configuration,
        TimeProvider clock)
    {
        var dsn = configuration.GetConnectionString("PlatformIdentity")
            ?? configuration["VG_PLATFORM_IDENTITY_DSN"]
            ?? throw new InvalidOperationException("Platform identity database DSN is not configured.");
        _dataSource = Npgsql.NpgsqlDataSource.Create(dsn);
        _store = new AccountStore(_dataSource, clock);
    }

    public Task<AccountProfile> ResolveAsync(VerifiedIdentity identity, CancellationToken cancellationToken)
        => _store.ResolveAsync(identity, cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}

public sealed class ProviderFlow(
    IReadOnlyDictionary<string, BrokerPartitionOptions> partitions,
    SessionStore sessions,
    TimeProvider timeProvider,
    IBrokerCodeExchange codeExchange,
    IBrokerTokenValidator tokenValidator,
    IIdentityAccountResolver identities,
    IConfiguration configuration)
{
    private static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(5);
    private readonly Uri[] _allowedWebReturnUris = ReadAllowedWebReturnUris(configuration);

    public Task<SignInStart> BeginAsync(
        BeginSignIn request,
        CancellationToken cancellationToken)
        => BeginCoreAsync(request, null, cancellationToken);

    internal Task<SignInStart> BeginBoundAsync(
        BeginSignIn request,
        string boundNonce,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundNonce);
        return BeginCoreAsync(request, boundNonce, cancellationToken);
    }

    private async Task<SignInStart> BeginCoreAsync(
        BeginSignIn request,
        string? boundNonce,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientChallenge);
        if (!IsAllowedConfiguredClientReturnUri(request.ReturnUri, _allowedWebReturnUris))
            throw new ArgumentException(
                "Return URI must be an explicitly allowed HTTPS web callback or the exact desktop loopback callback.",
                nameof(request));
        if (!partitions.TryGetValue(request.Provider, out var partition))
            throw new KeyNotFoundException("Unknown identity provider.");

        var state = RandomToken();
        var nonce = boundNonce ?? RandomToken();
        var expiresAt = timeProvider.GetUtcNow().Add(FlowLifetime);
        var flowId = await sessions.CreateFlowAsync(
            partition.Provider,
            request.ReturnUri,
            Hash(state),
            nonce,
            request.ClientChallenge,
            expiresAt,
            cancellationToken);

        var authorizationUri = BuildAuthorizationUri(
            partition, request.ReturnUri, state, nonce, request.ClientChallenge);
        return new SignInStart(flowId, authorizationUri, expiresAt);
    }
    public async Task<VerifiedIdentity> ValidateCompletionAsync(
        CompleteSignIn request,
        CancellationToken cancellationToken)
    {
        var claimed = await sessions.ClaimFlowAsync(request.FlowId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Authentication flow is unavailable.");
        if (!FixedHashEquals(claimed.StateHash, request.State))
            throw new UnauthorizedAccessException("Authentication state is invalid.");
        if (!FixedTextEquals(claimed.ClientChallenge, Pkce(request.ClientVerifier)))
            throw new UnauthorizedAccessException("PKCE verification failed.");
        if (!partitions.TryGetValue(claimed.Provider, out var partition))
            throw new UnauthorizedAccessException("Authentication provider is unavailable.");

        var token = await codeExchange.ExchangeAsync(
            partition, request.Code, cancellationToken);
        return await tokenValidator.ValidateAsync(
            token, claimed.Provider, claimed.Nonce, cancellationToken);
    }

    public async Task<ApiSession> CompleteAsync(
        CompleteSignIn request,
        CancellationToken cancellationToken)
    {
        var identity = await ValidateCompletionAsync(request, cancellationToken).ConfigureAwait(false);
        var profile = await identities.ResolveAsync(identity, cancellationToken).ConfigureAwait(false);
        return await sessions.IssueAsync(profile.AccountId, cancellationToken).ConfigureAwait(false);
    }
    internal static bool IsAllowedClientReturnUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return IsDesktopLoopbackReturnUri(uri)
            || (uri.Scheme == Uri.UriSchemeHttps
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment));
    }

    private static bool IsAllowedConfiguredClientReturnUri(
        Uri uri,
        IReadOnlyList<Uri> allowedWebReturnUris)
    {
        if (IsDesktopLoopbackReturnUri(uri)) return true;
        if (uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            return false;
        return allowedWebReturnUris.Any(allowed =>
            string.Equals(
                allowed.GetComponents(
                    UriComponents.SchemeAndServer | UriComponents.Path,
                    UriFormat.UriEscaped),
                uri.GetComponents(
                    UriComponents.SchemeAndServer | UriComponents.Path,
                    UriFormat.UriEscaped),
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDesktopLoopbackReturnUri(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttp
            && string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)
            && !uri.IsDefaultPort
            && string.Equals(uri.AbsolutePath, "/videograbber-auth/callback", StringComparison.Ordinal)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);

    private static Uri[] ReadAllowedWebReturnUris(IConfiguration configuration)
    {
        var result = new List<Uri>();
        AddConfigured(configuration["VG_WEB_AUTH_RETURN_URI"]);
        foreach (var raw in (configuration["VG_WEB_AUTH_RETURN_URIS"] ?? string.Empty)
                     .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            AddConfigured(raw);

        if (Uri.TryCreate(
                configuration["VG_PLATFORM_PUBLIC_URL"],
                UriKind.Absolute,
                out var publicUri)
            && publicUri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(publicUri.UserInfo))
            AddConfigured(new Uri(publicUri, "/web/").AbsoluteUri);

        return result.Distinct().ToArray();

        void AddConfigured(string? raw)
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
                return;
            result.Add(uri);
        }
    }
    private static Uri BuildAuthorizationUri(
        BrokerPartitionOptions partition,
        Uri returnUri,
        string state,
        string nonce,
        string clientChallenge)
    {
        var baseUri = new Uri(partition.Issuer, "authorize");
        var query = string.Join("&", new[]
        {
            "provider=" + Uri.EscapeDataString(partition.ProviderKey),
            "redirect_uri=" + Uri.EscapeDataString(partition.CallbackUri.AbsoluteUri),
            "state=" + Uri.EscapeDataString(state),
            "nonce=" + Uri.EscapeDataString(nonce),
            "code_challenge=" + Uri.EscapeDataString(clientChallenge),
            "code_challenge_method=S256"
        });
        return new UriBuilder(baseUri) { Query = query }.Uri;
    }

    private static string RandomToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string Pkce(string verifier)
        => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool FixedHashEquals(string expectedHash, string value)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHash),
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTextEquals(string expected, string actual)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length
            && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
