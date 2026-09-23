using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Persistence;

public sealed class AccountStore(
    NpgsqlDataSource dataSource,
    TimeProvider? clock = null) : IAccountStore
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<AccountProfile> ResolveAsync(
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(identity);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            try
            {
                var existing = await FindIdentityAccountAsync(
                    connection, transaction, identity, cancellationToken);
                if (existing is Guid accountId)
                {
                    await EnsureIdentityAsync(
                        connection, transaction, accountId, identity, cancellationToken);
                    var profile = await ReadProfileAsync(
                        connection, transaction, accountId, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return profile ?? throw new InvalidDataException("Identity points to a missing account.");
                }

                accountId = Guid.NewGuid();
                await InsertAccountAsync(
                    connection,
                    transaction,
                    accountId,
                    identity.Provider,
                    _clock.GetUtcNow(),
                    cancellationToken);
                await InsertIdentityAsync(
                    connection, transaction, accountId, identity, cancellationToken);
                var created = await ReadProfileAsync(
                    connection, transaction, accountId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return created ?? throw new InvalidDataException("Created account could not be read.");
            }
            catch (PostgresException ex) when (
                attempt < 3 && ex.SqlState is PostgresErrorCodes.SerializationFailure
                    or PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
        }
        throw new InvalidOperationException("Account identity resolution exhausted retries.");
    }

    public async Task<AccountProfile?> ReadAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand(
            "select set_config('vg.account_id', @account, true)", connection, transaction))
        {
            tenant.Parameters.AddWithValue("account", accountId.ToString("D"));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }
        var profile = await ReadProfileAsync(
            connection, transaction, accountId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return profile;
    }

    private static async Task<Guid?> FindIdentityAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        await using (var exact = new NpgsqlCommand("""
            select account_id
            from licensing.identities
            where issuer=@issuer and provider=@provider and provider_subject=@subject
            """, connection, transaction))
        {
            exact.Parameters.AddWithValue("issuer", identity.Issuer);
            exact.Parameters.AddWithValue("provider", identity.Provider);
            exact.Parameters.AddWithValue("subject", identity.Subject);
            var value = await exact.ExecuteScalarAsync(cancellationToken);
            if (value is Guid exactId) return exactId;
        }

        if (!identity.AllowProviderCoalescing
            || identity.Provider is not ("google" or "email"))
            return null;

        await using var stable = new NpgsqlCommand("""
            select distinct account_id
            from licensing.identities
            where issuer=@issuer
              and provider_subject=@subject
              and provider in ('google','email')
            order by account_id
            limit 2
            """, connection, transaction);
        stable.Parameters.AddWithValue("issuer", identity.Issuer);
        stable.Parameters.AddWithValue("subject", identity.Subject);
        await using var reader = await stable.ExecuteReaderAsync(cancellationToken);
        var accounts = new List<Guid>(2);
        while (await reader.ReadAsync(cancellationToken))
            accounts.Add(reader.GetGuid(0));
        return accounts.Count switch
        {
            0 => null,
            1 => accounts[0],
            _ => throw new UnauthorizedAccessException(
                "Supabase identity is linked to multiple VideoGrabber accounts.")
        };
    }

    private static async Task EnsureIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.identities(
              identity_id,account_id,issuer,provider,provider_subject,verified_email)
            values(@identity,@account,@issuer,@provider,@subject,@email)
            on conflict(issuer,provider,provider_subject) do nothing
            """, connection, transaction);
        command.Parameters.AddWithValue("identity", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("issuer", identity.Issuer);
        command.Parameters.AddWithValue("provider", identity.Provider);
        command.Parameters.AddWithValue("subject", identity.Subject);
        command.Parameters.AddWithValue(
            "email", (object?)identity.VerifiedEmail ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var verify = new NpgsqlCommand("""
            select account_id
            from licensing.identities
            where issuer=@issuer and provider=@provider and provider_subject=@subject
            """, connection, transaction);
        verify.Parameters.AddWithValue("issuer", identity.Issuer);
        verify.Parameters.AddWithValue("provider", identity.Provider);
        verify.Parameters.AddWithValue("subject", identity.Subject);
        var owner = await verify.ExecuteScalarAsync(cancellationToken);
        if (owner is not Guid mapped || mapped != accountId)
            throw new UnauthorizedAccessException(
                "Identity is already linked to another VideoGrabber account.");
    }

    private static async Task InsertAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string primaryAuthProvider,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using (var command = new NpgsqlCommand(
            """
            insert into licensing.accounts(
              account_id,base_role,primary_auth_provider)
            values(@id,'guest',@provider)
            """,
            connection, transaction))
        {
            command.Parameters.AddWithValue("id", accountId);
            command.Parameters.AddWithValue("provider", primaryAuthProvider);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var grantId = Guid.NewGuid();
        await using (var grant = new NpgsqlCommand("""
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,plan_id,
              valid_from,valid_until,available,reserved,original_amount,
              reason,created_at)
            values(@grant,@account,'credits','system_starter','free',
              @now,null,10,0,10,'free starter allowance',@now)
            """, connection, transaction))
        {
            grant.Parameters.AddWithValue("grant", grantId);
            grant.Parameters.AddWithValue("account", accountId);
            grant.Parameters.AddWithValue("now", now);
            await grant.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var issue = new NpgsqlCommand("""
            insert into licensing.credit_ledger(
              ledger_id,account_id,grant_id,event_kind,
              available_delta,reserved_delta,spent_delta,void_delta,
              reason,created_at)
            values(@ledger,@account,@grant,'issue',
              10,0,0,0,'free starter allowance issued',@now)
            """, connection, transaction);
        issue.Parameters.AddWithValue("ledger", Guid.NewGuid());
        issue.Parameters.AddWithValue("account", accountId);
        issue.Parameters.AddWithValue("grant", grantId);
        issue.Parameters.AddWithValue("now", now);
        await issue.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into licensing.identities(
              identity_id,account_id,issuer,provider,provider_subject,verified_email)
            values(@identity,@account,@issuer,@provider,@subject,@email)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("identity", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("issuer", identity.Issuer);
        command.Parameters.AddWithValue("provider", identity.Provider);
        command.Parameters.AddWithValue("subject", identity.Subject);
        command.Parameters.AddWithValue("email", (object?)identity.VerifiedEmail ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AccountProfile?> ReadProfileAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select a.account_id,
                   a.base_role,
                   a.blocked_at is not null as blocked,
                   a.first_purchase_at,
                   coalesce(array_agg(i.provider order by i.provider)
                     filter (where i.identity_id is not null), array[]::text[]) as providers,
                   a.primary_auth_provider
            from licensing.accounts a
            left join licensing.identities i on i.account_id = a.account_id
            where a.account_id=@id
            group by a.account_id,a.base_role,a.blocked_at,
                     a.first_purchase_at,a.primary_auth_provider
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new AccountProfile(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetBoolean(2),
            reader.GetFieldValue<string[]>(4),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3))
        {
            PrimaryAuthProvider = reader.IsDBNull(5) ? null : reader.GetString(5)
        };
    }

    private static void ValidateIdentity(VerifiedIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Subject);
        if (!Uri.TryCreate(identity.Issuer, UriKind.Absolute, out var issuer)
            || issuer.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Identity issuer must be an absolute HTTPS URI.", nameof(identity));
    }
}
