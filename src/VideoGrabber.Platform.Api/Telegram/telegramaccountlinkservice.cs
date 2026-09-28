using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Core.Access;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed class TelegramAccountLinkUnavailableException : Exception;
public sealed class TelegramAccountLinkConflictException : Exception;
public sealed class TelegramAccountLinkReconciliationRequiredException : Exception;

public sealed record TelegramAccountLinkTicket(
    Uri LinkUri,
    DateTimeOffset ExpiresAt);

public sealed class TelegramAccountLinkService : IAsyncDisposable
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);
    private readonly NpgsqlDataSource _adminDataSource;
    private readonly TimeProvider _clock;
    private readonly Uri _publicBase;

    public TelegramAccountLinkService(
        IConfiguration configuration,
        TimeProvider clock)
    {
        var dsn = configuration.GetConnectionString("PlatformAdmin")
            ?? configuration["VG_PLATFORM_ADMIN_DSN"]
            ?? throw new InvalidOperationException(
                "Platform admin database DSN is not configured.");
        _adminDataSource = NpgsqlDataSource.Create(dsn);
        _clock = clock;
        _publicBase = ReadPublicBase(configuration);
    }

    public async Task<TelegramAccountLinkTicket> BeginAsync(
        Guid sourceAccountId,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        if (sourceAccountId == Guid.Empty || telegramUserId <= 0)
            throw new ArgumentException("Telegram link source is invalid.");

        var now = _clock.GetUtcNow();
        var expiresAt = now.Add(TicketLifetime);
        var rawToken = RandomToken();
        var tokenHash = Hash(rawToken);

        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var source = await ReadAccountForUpdateAsync(
            connection, transaction, sourceAccountId, cancellationToken);
        if (source is null
            || source.Blocked
            || source.PrimaryAuthProvider != "telegram")
            throw new TelegramAccountLinkConflictException();

        var subject = telegramUserId.ToString(CultureInfo.InvariantCulture);
        var identities = await CountIdentitiesAsync(
            connection, transaction, sourceAccountId, cancellationToken);
        var telegramIdentity = await CountTelegramIdentityAsync(
            connection, transaction, sourceAccountId, subject, cancellationToken);
        if (identities != 1 || telegramIdentity != 1)
            throw new TelegramAccountLinkReconciliationRequiredException();

        await using (var cleanup = new NpgsqlCommand("""
            delete from licensing.telegram_account_links
            where source_account_id=@source
              and consumed_at is null
            """, connection, transaction))
        {
            cleanup.Parameters.AddWithValue("source", sourceAccountId);
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = new NpgsqlCommand("""
            insert into licensing.telegram_account_links(
              link_id,token_hash,source_account_id,telegram_user_id,
              created_at,expires_at)
            values(@id,@hash,@source,@telegram,@created,@expires)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("hash", tokenHash);
            insert.Parameters.AddWithValue("source", sourceAccountId);
            insert.Parameters.AddWithValue("telegram", telegramUserId);
            insert.Parameters.AddWithValue("created", now);
            insert.Parameters.AddWithValue("expires", expiresAt);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var link = new UriBuilder(new Uri(_publicBase, "/web/"))
        {
            Query = "telegram_link=" + Uri.EscapeDataString(rawToken)
        }.Uri;
        return new TelegramAccountLinkTicket(link, expiresAt);
    }

    public async Task CompleteAsync(
        Guid targetAccountId,
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (targetAccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(rawToken)
            || rawToken.Length > 256
            || rawToken.Any(char.IsWhiteSpace))
            throw new TelegramAccountLinkUnavailableException();

        var now = _clock.GetUtcNow();
        var tokenHash = Hash(rawToken);

        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        Guid sourceAccountId;
        long telegramUserId;
        Guid linkId;
        await using (var ticket = new NpgsqlCommand("""
            select link_id,source_account_id,telegram_user_id
            from licensing.telegram_account_links
            where token_hash=@hash
              and consumed_at is null
              and expires_at>@now
            for update
            """, connection, transaction))
        {
            ticket.Parameters.AddWithValue("hash", tokenHash);
            ticket.Parameters.AddWithValue("now", now);
            await using var reader =
                await ticket.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new TelegramAccountLinkUnavailableException();
            linkId = reader.GetGuid(0);
            sourceAccountId = reader.GetGuid(1);
            telegramUserId = reader.GetInt64(2);
        }

        if (sourceAccountId == targetAccountId)
            throw new TelegramAccountLinkConflictException();

        await LockAccountsAsync(
            connection, transaction, sourceAccountId, targetAccountId,
            cancellationToken);

        var source = await ReadAccountAsync(
            connection, transaction, sourceAccountId, cancellationToken)
            ?? throw new TelegramAccountLinkUnavailableException();
        var target = await ReadAccountAsync(
            connection, transaction, targetAccountId, cancellationToken)
            ?? throw new TelegramAccountLinkUnavailableException();

        if (source.Blocked
            || source.PrimaryAuthProvider != "telegram"
            || target.Blocked
            || !(target.Role == "owner_admin"
                 || target.PrimaryAuthProvider is "google" or "email"))
            throw new TelegramAccountLinkConflictException();

        var subject = telegramUserId.ToString(CultureInfo.InvariantCulture);
        if (await CountIdentitiesAsync(
                connection, transaction, sourceAccountId, cancellationToken) != 1
            || await CountTelegramIdentityAsync(
                connection, transaction, sourceAccountId, subject,
                cancellationToken) != 1)
            throw new TelegramAccountLinkReconciliationRequiredException();

        if (await HasOtherTelegramIdentityAsync(
                connection, transaction, targetAccountId, subject,
                cancellationToken))
            throw new TelegramAccountLinkConflictException();

        await EnsureSourceIsTemporaryAsync(
            connection, transaction, sourceAccountId, cancellationToken);

        var starter = await ReadUntouchedStarterAsync(
            connection, transaction, sourceAccountId, cancellationToken)
            ?? throw new TelegramAccountLinkReconciliationRequiredException();

        await using (var move = new NpgsqlCommand("""
            update licensing.identities
            set account_id=@target
            where account_id=@source
              and provider='telegram'
              and provider_subject=@subject
            """, connection, transaction))
        {
            move.Parameters.AddWithValue("target", targetAccountId);
            move.Parameters.AddWithValue("source", sourceAccountId);
            move.Parameters.AddWithValue("subject", subject);
            if (await move.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new TelegramAccountLinkConflictException();
        }

        await using (var revokeStarter = new NpgsqlCommand("""
            update licensing.entitlement_grants
            set available=0,revoked_at=@now
            where grant_id=@grant
              and available=10
              and reserved=0
              and revoked_at is null
            """, connection, transaction))
        {
            revokeStarter.Parameters.AddWithValue("now", now);
            revokeStarter.Parameters.AddWithValue("grant", starter);
            if (await revokeStarter.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new TelegramAccountLinkReconciliationRequiredException();
        }

        await using (var ledger = new NpgsqlCommand("""
            insert into licensing.credit_ledger(
              ledger_id,account_id,grant_id,event_kind,
              available_delta,reserved_delta,spent_delta,void_delta,
              actor_account_id,reason,created_at)
            values(@id,@source,@grant,'adjustment',
              -10,0,0,10,@target,'telegram temporary account linked',@now)
            """, connection, transaction))
        {
            ledger.Parameters.AddWithValue("id", Guid.NewGuid());
            ledger.Parameters.AddWithValue("source", sourceAccountId);
            ledger.Parameters.AddWithValue("grant", starter);
            ledger.Parameters.AddWithValue("target", targetAccountId);
            ledger.Parameters.AddWithValue("now", now);
            await ledger.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var revokeSessions = new NpgsqlCommand("""
            update licensing.api_sessions
            set revoked_at=coalesce(revoked_at,@now)
            where account_id=@source
              and revoked_at is null
            """, connection, transaction))
        {
            revokeSessions.Parameters.AddWithValue("now", now);
            revokeSessions.Parameters.AddWithValue("source", sourceAccountId);
            await revokeSessions.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var blockSource = new NpgsqlCommand("""
            update licensing.accounts
            set blocked_at=coalesce(blocked_at,@now)
            where account_id=@source
            """, connection, transaction))
        {
            blockSource.Parameters.AddWithValue("now", now);
            blockSource.Parameters.AddWithValue("source", sourceAccountId);
            await blockSource.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var consume = new NpgsqlCommand("""
            update licensing.telegram_account_links
            set consumed_at=@now,target_account_id=@target
            where link_id=@link and consumed_at is null
            """, connection, transaction))
        {
            consume.Parameters.AddWithValue("now", now);
            consume.Parameters.AddWithValue("target", targetAccountId);
            consume.Parameters.AddWithValue("link", linkId);
            if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new TelegramAccountLinkConflictException();
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureSourceIsTemporaryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sourceAccountId,
        CancellationToken cancellationToken)
    {
        var checks = new[]
        {
            "select exists(select 1 from licensing.payments where account_id=@source)",
            "select exists(select 1 from licensing.subscriptions where account_id=@source)",
            "select exists(select 1 from licensing.devices where account_id=@source and revoked_at is null)",
            "select exists(select 1 from licensing.delivery_destinations where account_id=@source and revoked_at is null)",
            "select exists(select 1 from licensing.jobs where account_id=@source and state not in ('completed','cancelled','failed'))",
            """
            select exists(
              select 1
              from licensing.entitlement_grants
              where account_id=@source
                and revoked_at is null
                and not (source='system_starter' and plan_id='free'))
            """
        };

        foreach (var sql in checks)
        {
            await using var command =
                new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("source", sourceAccountId);
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
                throw new TelegramAccountLinkReconciliationRequiredException();
        }
    }

    private static async Task<Guid?> ReadUntouchedStarterAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sourceAccountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select grant_id
            from licensing.entitlement_grants
            where account_id=@source
              and source='system_starter'
              and plan_id='free'
              and revoked_at is null
              and original_amount=10
              and available=10
              and reserved=0
            for update
            """, connection, transaction);
        command.Parameters.AddWithValue("source", sourceAccountId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static async Task LockAccountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid source,
        Guid target,
        CancellationToken cancellationToken)
    {
        foreach (var account in new[] { source, target }.OrderBy(x => x))
        {
            await using var command = new NpgsqlCommand(
                "select pg_advisory_xact_lock(hashtextextended(@account,20260922))",
                connection, transaction);
            command.Parameters.AddWithValue("account", account.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<AccountRow?> ReadAccountForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select base_role,blocked_at is not null,primary_auth_provider
            from licensing.accounts
            where account_id=@account
            for update
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new AccountRow(
            reader.GetString(0),
            reader.GetBoolean(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static Task<AccountRow?> ReadAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
        => ReadAccountForUpdateAsync(
            connection, transaction, accountId, cancellationToken);

    private static async Task<long> CountIdentitiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.identities where account_id=@account",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<long> CountTelegramIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string subject,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select count(*)
            from licensing.identities
            where account_id=@account
              and provider='telegram'
              and provider_subject=@subject
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("subject", subject);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<bool> HasOtherTelegramIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string subject,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select exists(
              select 1
              from licensing.identities
              where account_id=@account
                and provider='telegram'
                and provider_subject<>@subject)
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("subject", subject);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
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

    private sealed record AccountRow(
        string Role,
        bool Blocked,
        string? PrimaryAuthProvider);

    public ValueTask DisposeAsync()
        => _adminDataSource.DisposeAsync();
}
