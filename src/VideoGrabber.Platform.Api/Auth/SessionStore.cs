using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Auth;

public sealed record ClaimedAuthFlow(
    string Provider,
    Uri ReturnUri,
    string StateHash,
    string Nonce,
    string ClientChallenge,
    DateTimeOffset ExpiresAt);

public sealed record SessionJwtOptions(
    string Issuer,
    string Audience,
    byte[] SigningKey)
{
    public static SessionJwtOptions FromConfiguration(IConfiguration configuration)
    {
        var text = configuration["VG_PLATFORM_SESSION_SIGNING_KEY"]
            ?? throw new InvalidOperationException("Platform session signing key is required.");
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Platform session signing key must be at least 32 bytes.");
        return new("videograbber-platform", "videograbber-api", bytes);
    }
}

public sealed class SessionStore(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    SessionJwtOptions jwt) : IBrokerBindingStore
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task<Guid> CreateFlowAsync(
        string provider,
        Uri returnUri,
        string stateHash,
        string nonce,
        string clientChallenge,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var flowId = Guid.NewGuid();
        const string sql = """
            insert into licensing.auth_flows(
              flow_id,provider,return_uri,state_hash,nonce_value,
              client_challenge,created_at,expires_at)
            values(@id,@provider,@return,@state,@nonce,@challenge,@created,@expires)
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", flowId);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("return", returnUri.AbsoluteUri);
        command.Parameters.AddWithValue("state", stateHash);
        command.Parameters.AddWithValue("nonce", nonce);
        command.Parameters.AddWithValue("challenge", clientChallenge);
        command.Parameters.AddWithValue("created", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("expires", expiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return flowId;
    }

    public async Task<ClaimedAuthFlow?> ClaimFlowAsync(
        Guid flowId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update licensing.auth_flows
               set consumed_at=@now
             where flow_id=@id
               and consumed_at is null
               and expires_at>@now
            returning provider,return_uri,state_hash,nonce_value,
                      client_challenge,expires_at
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", flowId);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ClaimedAuthFlow(
            reader.GetString(0),
            new Uri(reader.GetString(1), UriKind.Absolute),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    public async Task<ApiSession> IssueAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var accessExpires = now.Add(AccessLifetime);
        var refreshExpires = now.Add(RefreshLifetime);
        var refresh = CreateRefreshToken(accountId);
        var sessionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        const string sql = """
            insert into licensing.api_sessions(
              session_id,account_id,family_id,refresh_hash,created_at,refresh_expires_at)
            values(@session,@account,@family,@hash,@created,@expires)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("session", sessionId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("family", familyId);
        command.Parameters.AddWithValue("hash", Hash(refresh));
        command.Parameters.AddWithValue("created", now);
        command.Parameters.AddWithValue("expires", refreshExpires);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ApiSession(CreateAccessToken(accountId, sessionId, now, accessExpires), refresh, accessExpires);
    }

    public async Task FreezeAsync(string provider, string brokerSubject, string providerSubject,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string insertSql = """
            insert into licensing.broker_bindings(provider,broker_subject,provider_subject)
            values(@provider,@broker,@subject) on conflict (provider,broker_subject) do nothing
            """;
        try
        {
            await using var insert = new NpgsqlCommand(insertSql, connection);
            insert.Parameters.AddWithValue("provider", provider);
            insert.Parameters.AddWithValue("broker", brokerSubject);
            insert.Parameters.AddWithValue("subject", providerSubject);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new UnauthorizedAccessException("Provider subject is already bound to another broker user.", ex);
        }
        await using var query = new NpgsqlCommand(
            "select provider_subject from licensing.broker_bindings where provider=@provider and broker_subject=@broker", connection);
        query.Parameters.AddWithValue("provider", provider);
        query.Parameters.AddWithValue("broker", brokerSubject);
        var frozen = (string?)await query.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(frozen, providerSubject, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Broker provider subject changed after binding.");
    }

    public async Task<ApiSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var accountId = ParseRefreshAccount(refreshToken);
        var now = timeProvider.GetUtcNow();
        var hash = Hash(refreshToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        const string selectSql = """
            select session_id,family_id,refresh_expires_at,revoked_at
            from licensing.api_sessions
            where account_id=@account and refresh_hash=@hash for update
            """;
        await using var select = new NpgsqlCommand(selectSql, connection, transaction);
        select.Parameters.AddWithValue("account", accountId);
        select.Parameters.AddWithValue("hash", hash);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new UnauthorizedAccessException("Refresh session is unknown.");
        var familyId = reader.GetGuid(1);
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
        var revoked = !reader.IsDBNull(3);
        await reader.DisposeAsync();
        if (revoked || expiresAt <= now)
        {
            await RevokeFamilyAsync(connection, transaction, accountId, familyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh session is no longer valid.");
        }
        await RevokeSessionAsync(connection, transaction, accountId, hash, now, cancellationToken);
        var next = await InsertSessionAsync(connection, transaction, accountId, familyId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return next;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var accountId = ParseRefreshAccount(refreshToken);
        var hash = Hash(refreshToken);
        var now = timeProvider.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var query = new NpgsqlCommand(
            "select family_id from licensing.api_sessions where account_id=@account and refresh_hash=@hash", connection, transaction);
        query.Parameters.AddWithValue("account", accountId);
        query.Parameters.AddWithValue("hash", hash);
        var family = await query.ExecuteScalarAsync(cancellationToken);
        if (family is not Guid familyId)
            throw new UnauthorizedAccessException("Refresh session is unknown.");
        await RevokeFamilyAsync(connection, transaction, accountId, familyId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<ApiSession> InsertSessionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var refresh = CreateRefreshToken(accountId);
        var accessExpires = now.Add(AccessLifetime);
        var refreshExpires = now.Add(RefreshLifetime);
        await using var command = new NpgsqlCommand("""
            insert into licensing.api_sessions(session_id,account_id,family_id,refresh_hash,created_at,refresh_expires_at)
            values(@session,@account,@family,@hash,@created,@expires)
            """, connection, transaction);
        command.Parameters.AddWithValue("session", sessionId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("family", familyId);
        command.Parameters.AddWithValue("hash", Hash(refresh));
        command.Parameters.AddWithValue("created", now);
        command.Parameters.AddWithValue("expires", refreshExpires);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new ApiSession(CreateAccessToken(accountId, sessionId, now, accessExpires), refresh, accessExpires);
    }

    private static async Task RevokeSessionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, string hash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "update licensing.api_sessions set revoked_at=@now where account_id=@account and refresh_hash=@hash and revoked_at is null",
            connection, transaction);
        command.Parameters.AddWithValue("now", now); command.Parameters.AddWithValue("account", accountId); command.Parameters.AddWithValue("hash", hash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RevokeFamilyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "update licensing.api_sessions set revoked_at=coalesce(revoked_at,@now) where account_id=@account and family_id=@family",
            connection, transaction);
        command.Parameters.AddWithValue("now", now); command.Parameters.AddWithValue("account", accountId); command.Parameters.AddWithValue("family", familyId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CreateRefreshToken(Guid accountId) => accountId.ToString("N") + "." + RandomToken();

    private static Guid ParseRefreshAccount(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("Refresh token is missing.");
        var dot = token.IndexOf('.');
        if (dot != 32 || !Guid.TryParseExact(token[..dot], "N", out var accountId))
            throw new UnauthorizedAccessException("Refresh token is malformed.");
        return accountId;
    }

    private string CreateAccessToken(Guid accountId, Guid sessionId,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(jwt.SigningKey);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString("D")),
            new Claim("account_id", accountId.ToString("D")),
            new Claim("sid", sessionId.ToString("D"))
        };
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            issuedAt.UtcDateTime, expiresAt.UtcDateTime, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task SetAccountAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select set_config('vg.account_id',@account,true)", connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string RandomToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
