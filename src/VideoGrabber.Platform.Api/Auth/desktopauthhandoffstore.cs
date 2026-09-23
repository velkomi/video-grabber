using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Auth;

public sealed class DesktopAuthHandoffStore : IAsyncDisposable
{
    private static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(5);
    private const string CallbackPath = "/videograbber-auth/callback";

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly Uri _publicBase;
    private readonly bool _ownsDataSource;

    public DesktopAuthHandoffStore(
        IConfiguration configuration,
        TimeProvider clock)
    {
        var dsn = configuration.GetConnectionString("PlatformIdentity")
            ?? configuration["VG_PLATFORM_IDENTITY_DSN"]
            ?? throw new InvalidOperationException(
                "Platform identity database DSN is not configured.");
        _dataSource = NpgsqlDataSource.Create(dsn);
        _clock = clock;
        _publicBase = ReadPublicBase(configuration);
        _ownsDataSource = true;
    }

    private DesktopAuthHandoffStore(
        NpgsqlDataSource dataSource,
        TimeProvider clock,
        Uri publicBase)
    {
        _dataSource = dataSource;
        _clock = clock;
        _publicBase = publicBase;
        _ownsDataSource = false;
    }

    public static DesktopAuthHandoffStore CreateForTesting(
        NpgsqlDataSource dataSource,
        TimeProvider clock,
        Uri publicBase)
        => new(dataSource, clock, publicBase);

    public async Task<DesktopSignInStart> StartAsync(
        Uri returnUri,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedLoopback(returnUri))
            throw new ArgumentException(
                "Desktop return URI must be the exact IPv4 loopback callback.",
                nameof(returnUri));

        var now = _clock.GetUtcNow();
        var expires = now.Add(FlowLifetime);
        var flowId = Guid.NewGuid();
        var state = RandomToken();

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.desktop_auth_handoffs(
              flow_id,return_uri,state_hash,created_at,expires_at)
            values(@flow,@return,@state,@created,@expires)
            """, connection);
        command.Parameters.AddWithValue("flow", flowId);
        command.Parameters.AddWithValue("return", returnUri.AbsoluteUri);
        command.Parameters.AddWithValue("state", Hash(state));
        command.Parameters.AddWithValue("created", now);
        command.Parameters.AddWithValue("expires", expires);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var verification = new UriBuilder(new Uri(_publicBase, "/web/"))
        {
            Query =
                "desktop_flow=" + Uri.EscapeDataString(flowId.ToString("D")) +
                "&desktop_state=" + Uri.EscapeDataString(state)
        }.Uri;
        return new DesktopSignInStart(flowId, verification, state, expires);
    }

    public async Task<DesktopSignInApproval> ApproveAsync(
        Guid accountId,
        DesktopSignInApprovalRequest request,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty
            || request.FlowId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.State))
            throw new ArgumentException("Desktop approval is incomplete.");

        var now = _clock.GetUtcNow();
        var code = RandomToken();
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.desktop_auth_handoffs
               set account_id=@account,
                   code_hash=@code,
                   approved_at=@now
             where flow_id=@flow
               and state_hash=@state
               and approved_at is null
               and consumed_at is null
               and expires_at>@now
            returning return_uri
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("code", Hash(code));
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("flow", request.FlowId);
        command.Parameters.AddWithValue("state", Hash(request.State));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string returnText
            || !Uri.TryCreate(returnText, UriKind.Absolute, out var returnUri)
            || !IsAllowedLoopback(returnUri))
            throw new UnauthorizedAccessException("desktop_handoff_unavailable");

        return new DesktopSignInApproval(returnUri, code, request.State);
    }

    public async Task<Guid> ConsumeAsync(
        DesktopSignInConsumeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FlowId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.State))
            throw new UnauthorizedAccessException("desktop_handoff_invalid");

        var now = _clock.GetUtcNow();
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.desktop_auth_handoffs
               set consumed_at=@now
             where flow_id=@flow
               and state_hash=@state
               and code_hash=@code
               and account_id is not null
               and approved_at is not null
               and consumed_at is null
               and expires_at>@now
            returning account_id
            """, connection);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("flow", request.FlowId);
        command.Parameters.AddWithValue("state", Hash(request.State));
        command.Parameters.AddWithValue("code", Hash(request.Code));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid accountId
            ? accountId
            : throw new UnauthorizedAccessException(
                "desktop_handoff_unavailable");
    }

    public static bool IsAllowedLoopback(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.Scheme == Uri.UriSchemeHttp
            && string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)
            && !uri.IsDefaultPort
            && string.Equals(uri.AbsolutePath, CallbackPath, StringComparison.Ordinal)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }

    private static Uri ReadPublicBase(IConfiguration configuration)
    {
        var raw = configuration["VG_PLATFORM_PUBLIC_URL"];
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException(
                "VG_PLATFORM_PUBLIC_URL must be an absolute HTTPS URL.");
        return uri;
    }

    private static string RandomToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string value)
        => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    public ValueTask DisposeAsync()
        => _ownsDataSource
            ? _dataSource.DisposeAsync()
            : ValueTask.CompletedTask;
}
