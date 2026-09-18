using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record TelegramWebSession(DateTimeOffset ExpiresAt, string CsrfToken);

public interface ITelegramAccountResolver
{
    Task<Guid> ResolveAsync(long userId, DateTimeOffset authTime, CancellationToken cancellationToken);
}

public sealed class TelegramAccountResolver : ITelegramAccountResolver, IAsyncDisposable
{
    private static readonly string TelegramIssuer = new Uri("https://telegram.webapp.local/").AbsoluteUri;
    private readonly NpgsqlDataSource _identityDataSource;
    private readonly AccountStore _accounts;
    private readonly bool _ownsDataSource;

    public TelegramAccountResolver(IConfiguration configuration)
    {
        var dsn = configuration.GetConnectionString("PlatformIdentity")
            ?? configuration["VG_PLATFORM_IDENTITY_DSN"]
            ?? throw new InvalidOperationException("Platform identity database DSN is not configured.");
        _identityDataSource = NpgsqlDataSource.Create(dsn);
        _accounts = new AccountStore(_identityDataSource);
        _ownsDataSource = true;
    }

    private TelegramAccountResolver(NpgsqlDataSource identityDataSource)
    {
        _identityDataSource = identityDataSource;
        _accounts = new AccountStore(identityDataSource);
        _ownsDataSource = false;
    }

    public static TelegramAccountResolver CreateForTesting(NpgsqlDataSource identityDataSource)
        => new(identityDataSource);

    public async Task<Guid> ResolveAsync(
        long userId,
        DateTimeOffset authTime,
        CancellationToken cancellationToken)
    {
        if (userId <= 0) throw new UnauthorizedAccessException("telegram_user_invalid");
        var subject = userId.ToString(CultureInfo.InvariantCulture);
        await using (var connection = await _identityDataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = new NpgsqlCommand("""
            select distinct account_id
            from licensing.identities
            where provider='telegram' and provider_subject=@subject
            order by account_id
            limit 2
            """, connection))
        {
            command.Parameters.AddWithValue("subject", subject);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var accounts = new List<Guid>(2);
            while (await reader.ReadAsync(cancellationToken)) accounts.Add(reader.GetGuid(0));
            if (accounts.Count == 1) return accounts[0];
            if (accounts.Count > 1) throw new UnauthorizedAccessException("telegram_identity_ambiguous");
        }

        var profile = await _accounts.ResolveAsync(new VerifiedIdentity(
            TelegramIssuer,
            "telegram",
            subject,
            null,
            authTime,
            "telegram_initdata"), cancellationToken);
        return profile.AccountId;
    }

    public ValueTask DisposeAsync()
        => _ownsDataSource ? _identityDataSource.DisposeAsync() : ValueTask.CompletedTask;
}

public sealed class TelegramAssertionStore(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider)
{
    public async Task<bool> TryUseAsync(
        TelegramPrincipal principal,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expires = principal.AuthTime.AddMinutes(5);
        if (expires < now) return false;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.telegram_assertions(
              assertion_hash,user_id,account_id,used_at,expires_at)
            values(@hash,@user,@account,@used,@expires)
            on conflict(assertion_hash) do nothing
            """, connection);
        command.Parameters.AddWithValue("hash", principal.AssertionHash);
        command.Parameters.AddWithValue("user", principal.UserId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("used", now);
        command.Parameters.AddWithValue("expires", expires);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "delete from licensing.telegram_assertions where expires_at < @cutoff", connection);
        command.Parameters.AddWithValue("cutoff", timeProvider.GetUtcNow().AddDays(-1));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public static class TelegramSessionEndpoints
{
    private const int MaxInitDataBytes = 16 * 1024;

    public static IEndpointRouteBuilder MapTelegramSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/telegram/session", CreateSessionAsync)
            .RequireRateLimiting("auth");
        return endpoints;
    }

    private static async Task<IResult> CreateSessionAsync(
        HttpContext http,
        IMiniAppAssertionValidator validator,
        ITelegramAccountResolver accounts,
        TelegramAssertionStore assertions,
        SessionStore sessions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (http.Request.ContentLength is > MaxInitDataBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        string initData;
        try { initData = await ReadLimitedAsync(http.Request.Body, MaxInitDataBytes, cancellationToken); }
        catch (PayloadTooLargeException) { return Results.StatusCode(StatusCodes.Status413PayloadTooLarge); }
        try
        {
            var principal = validator.Validate(initData, clock.GetUtcNow());
            var accountId = await accounts.ResolveAsync(principal.UserId, principal.AuthTime, cancellationToken);
            if (!await assertions.TryUseAsync(principal, accountId, cancellationToken))
                return Results.Unauthorized();
            var session = await sessions.IssueAsync(accountId, cancellationToken);
            var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var cookie = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = session.ExpiresAt
            };
            http.Response.Cookies.Append("vg_session", session.AccessToken, cookie);
            http.Response.Cookies.Append("vg_csrf", csrf, cookie);
            return Results.Ok(new TelegramWebSession(session.ExpiresAt, csrf));
        }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (ArgumentException) { return Results.Unauthorized(); }
    }

    internal static async Task<string> ReadLimitedAsync(
        Stream stream,
        int limit,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream(Math.Min(limit, 4096));
        var buffer = new byte[4096];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > limit) throw new PayloadTooLargeException();
            memory.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private sealed class PayloadTooLargeException : Exception;
}