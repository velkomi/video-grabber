using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Persistence;

public sealed class AccountStore(NpgsqlDataSource dataSource) : IAccountStore
{
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
                    var profile = await ReadProfileAsync(
                        connection, transaction, accountId, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return profile ?? throw new InvalidDataException("Identity points to a missing account.");
                }

                accountId = Guid.NewGuid();
                await InsertAccountAsync(connection, transaction, accountId, cancellationToken);
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
        const string sql = """
            select account_id
            from licensing.identities
            where issuer=@issuer and provider=@provider and provider_subject=@subject
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("issuer", identity.Issuer);
        command.Parameters.AddWithValue("provider", identity.Provider);
        command.Parameters.AddWithValue("subject", identity.Subject);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static async Task InsertAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "insert into licensing.accounts(account_id,base_role) values(@id,'guest')",
            connection, transaction);
        command.Parameters.AddWithValue("id", accountId);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
                     filter (where i.identity_id is not null), array[]::text[]) as providers
            from licensing.accounts a
            left join licensing.identities i on i.account_id = a.account_id
            where a.account_id=@id
            group by a.account_id,a.base_role,a.blocked_at,a.first_purchase_at
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
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3));
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
