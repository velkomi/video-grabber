using System.Data;
using System.IdentityModel.Tokens.Jwt;
using Npgsql;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Accounts;

public sealed class LinkChallengeNotFoundException : Exception;
public sealed class LinkChallengeConflictException : Exception;
public sealed class IdentityLinkConflictException : Exception;
public sealed class IdentityNotFoundException : Exception;
public sealed class FinancialMergeRequiresReconciliationException : Exception;

public sealed class IdentityLinkService : IAsyncDisposable
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    private readonly NpgsqlDataSource _apiDataSource;
    private readonly NpgsqlDataSource _identityDataSource;
    private readonly NpgsqlDataSource _adminDataSource;
    private readonly TimeProvider _clock;
    private readonly IBrokerTokenValidator _tokens;
    private readonly IReadOnlyDictionary<string, BrokerPartitionOptions> _partitions;
    private readonly bool _ownsIdentityDataSource;
    private readonly bool _ownsAdminDataSource;

    public IdentityLinkService(
        NpgsqlDataSource apiDataSource,
        IConfiguration configuration,
        TimeProvider clock,
        IBrokerTokenValidator tokens,
        IReadOnlyDictionary<string, BrokerPartitionOptions> partitions)
        : this(apiDataSource, CreateIdentityDataSource(configuration), CreateAdminDataSource(configuration), clock,
            tokens, partitions, ownsIdentityDataSource: true, ownsAdminDataSource: true)
    {
    }

    private IdentityLinkService(
        NpgsqlDataSource apiDataSource,
        NpgsqlDataSource identityDataSource,
        NpgsqlDataSource adminDataSource,
        TimeProvider clock,
        IBrokerTokenValidator tokens,
        IReadOnlyDictionary<string, BrokerPartitionOptions> partitions,
        bool ownsIdentityDataSource,
        bool ownsAdminDataSource)
    {
        _apiDataSource = apiDataSource;
        _identityDataSource = identityDataSource;
        _adminDataSource = adminDataSource;
        _clock = clock;
        _tokens = tokens;
        _partitions = partitions;
        _ownsIdentityDataSource = ownsIdentityDataSource;
        _ownsAdminDataSource = ownsAdminDataSource;
    }

    public static IdentityLinkService CreateForTesting(
        NpgsqlDataSource apiDataSource,
        NpgsqlDataSource identityDataSource,
        NpgsqlDataSource adminDataSource,
        TimeProvider clock,
        IBrokerTokenValidator tokens,
        IReadOnlyDictionary<string, BrokerPartitionOptions> partitions)
        => new(apiDataSource, identityDataSource, adminDataSource, clock, tokens, partitions,
            ownsIdentityDataSource: false, ownsAdminDataSource: false);

    public async Task<LinkChallenge> BeginAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var challenge = new LinkChallenge(Guid.NewGuid(), now.Add(ChallengeLifetime));
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        const string sql = """
            insert into licensing.account_links(
              challenge_id,account_id,created_at,expires_at)
            values(@id,@account,@created,@expires)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", challenge.ChallengeId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("created", now);
        command.Parameters.AddWithValue("expires", challenge.ExpiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return challenge;
    }

    public async Task CompleteAsync(
        Guid accountId,
        LinkProof proof,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proof.FreshProviderAssertion);
        await ClaimChallengeAsync(accountId, proof.ChallengeId, cancellationToken);
        var provider = ReadProviderHint(proof.FreshProviderAssertion);
        if (!_partitions.ContainsKey(provider))
            throw new UnauthorizedAccessException("Unknown link provider.");
        var identity = await _tokens.ValidateAsync(proof.FreshProviderAssertion, provider,
            proof.ChallengeId.ToString("D"), cancellationToken);
        await LinkIdentityAsync(accountId, identity, cancellationToken);
        await AuditAsync(accountId, "identity_linked", identity.Provider, cancellationToken);
    }

    public async Task UnlinkAsync(
        Guid accountId,
        Guid identityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _identityDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        var count = await CountIdentitiesAsync(connection, transaction, accountId, cancellationToken);
        if (count <= 1) throw new IdentityLinkConflictException();

        await using var command = new NpgsqlCommand("""
            delete from licensing.identities
            where identity_id=@identity and account_id=@account
            returning provider
            """, connection, transaction);
        command.Parameters.AddWithValue("identity", identityId);
        command.Parameters.AddWithValue("account", accountId);
        var provider = (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new IdentityNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync(accountId, "identity_unlinked", provider, cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedIdentity>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _identityDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select identity_id,provider,verified_email,linked_at
            from licensing.identities
            where account_id=@account
            order by linked_at,identity_id
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var identities = new List<LinkedIdentity>();
        while (await reader.ReadAsync(cancellationToken))
            identities.Add(new LinkedIdentity(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return identities;
    }

    public async Task EnsureChallengeAvailableAsync(
        Guid accountId,
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select expires_at,consumed_at
            from licensing.account_links
            where challenge_id=@id and account_id=@account and purpose='link'
            """, connection, transaction);
        command.Parameters.AddWithValue("id", challengeId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new LinkChallengeNotFoundException();
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(0);
        var consumed = !reader.IsDBNull(1);
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        if (consumed || expiresAt <= now)
            throw new LinkChallengeConflictException();
    }

    public async Task CompleteVerifiedAsync(
        Guid accountId,
        Guid challengeId,
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        await ClaimChallengeAsync(accountId, challengeId, cancellationToken);
        await LinkIdentityAsync(accountId, identity, cancellationToken);
        await AuditAsync(accountId, "identity_linked", identity.Provider, cancellationToken);
    }
    private async Task ClaimChallengeAsync(
        Guid accountId,
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var select = new NpgsqlCommand("""
            select expires_at,consumed_at
            from licensing.account_links
            where challenge_id=@id and purpose='link'
            for update
            """, connection, transaction);
        select.Parameters.AddWithValue("id", challengeId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new LinkChallengeNotFoundException();
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(0);
        var consumed = !reader.IsDBNull(1);
        await reader.DisposeAsync();
        if (consumed || expiresAt <= now)
            throw new LinkChallengeConflictException();
        await using var consume = new NpgsqlCommand(
            "update licensing.account_links set consumed_at=@now where challenge_id=@id",
            connection, transaction);
        consume.Parameters.AddWithValue("now", now);
        consume.Parameters.AddWithValue("id", challengeId);
        await consume.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task LinkIdentityAsync(
        Guid accountId,
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var connection = await _identityDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        var owner = await FindIdentityOwnerAsync(connection, transaction, identity, cancellationToken);
        if (owner is Guid existing)
        {
            if (existing != accountId) throw new IdentityLinkConflictException();
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        try
        {
            await using var insert = new NpgsqlCommand("""
                insert into licensing.identities(
                  identity_id,account_id,issuer,provider,provider_subject,verified_email)
                values(@id,@account,@issuer,@provider,@subject,@email)
                """, connection, transaction);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("account", accountId);
            insert.Parameters.AddWithValue("issuer", identity.Issuer);
            insert.Parameters.AddWithValue("provider", identity.Provider);
            insert.Parameters.AddWithValue("subject", identity.Subject);
            insert.Parameters.AddWithValue("email", (object?)identity.VerifiedEmail ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            var currentOwner = await FindIdentityOwnerAsync(identity, cancellationToken);
            if (currentOwner != accountId) throw new IdentityLinkConflictException();
        }
    }

    private static async Task<Guid?> FindIdentityOwnerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select account_id from licensing.identities
            where issuer=@issuer and provider=@provider and provider_subject=@subject
            """, connection, transaction);
        command.Parameters.AddWithValue("issuer", identity.Issuer);
        command.Parameters.AddWithValue("provider", identity.Provider);
        command.Parameters.AddWithValue("subject", identity.Subject);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private async Task<Guid?> FindIdentityOwnerAsync(
        VerifiedIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var connection = await _identityDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var owner = await FindIdentityOwnerAsync(connection, transaction, identity, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return owner;
    }

    private static async Task LockAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended(@account,20260917))",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

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

    public async Task MergeAsync(
        Guid adminId,
        MergeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceAccountId == request.TargetAccountId)
            throw new IdentityLinkConflictException();
        await EnsureAdminAsync(adminId, cancellationToken);
        var source = await ValidateMergeProofAsync(request.SourceProof,
            MergeNonce(request, "source"), cancellationToken);
        var target = await ValidateMergeProofAsync(request.TargetProof,
            MergeNonce(request, "target"), cancellationToken);
        if (await FindIdentityOwnerAsync(source, cancellationToken) != request.SourceAccountId
            || await FindIdentityOwnerAsync(target, cancellationToken) != request.TargetAccountId)
            throw new UnauthorizedAccessException("Merge proof does not own the requested account.");

        await MergeAccountsAsync(adminId, request, cancellationToken);
        await RevokeSessionsAsync(request.SourceAccountId, cancellationToken);
        await AuditMergeAsync(adminId, request, cancellationToken);
    }

    private async Task<VerifiedIdentity> ValidateMergeProofAsync(
        string assertion,
        string nonce,
        CancellationToken cancellationToken)
    {
        var provider = ReadProviderHint(assertion);
        if (!_partitions.ContainsKey(provider))
            throw new UnauthorizedAccessException("Unknown merge proof provider.");
        return await _tokens.ValidateAsync(assertion, provider, nonce, cancellationToken);
    }

    private async Task EnsureAdminAsync(Guid adminId, CancellationToken cancellationToken)
    {
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, adminId, cancellationToken);
        await using var command = new NpgsqlCommand(
            "select base_role,blocked_at is not null from licensing.accounts where account_id=@id",
            connection, transaction);
        command.Parameters.AddWithValue("id", adminId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.GetString(0) != "owner_admin" || reader.GetBoolean(1))
            throw new UnauthorizedAccessException("Admin authorization is required.");
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MergeAccountsAsync(
        Guid adminId, MergeRequest request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var connection = await _adminDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var accounts = await LockMergeAccountsAsync(connection, transaction, request, cancellationToken);
        if (accounts.Count != 2 || accounts.Any(account => account.Blocked || account.MergedInto is not null))
            throw new FinancialMergeRequiresReconciliationException();
        if (await HasActiveReservationsAsync(connection, transaction, request, cancellationToken))
            throw new FinancialMergeRequiresReconciliationException();

        await TransferActiveGrantsAsync(connection, transaction, adminId, request, now, cancellationToken);
        var identities = await ReadIdentitiesAsync(
            connection, transaction, request.SourceAccountId, cancellationToken);
        if (identities.Count == 0) throw new IdentityNotFoundException();
        await MoveIdentitiesAsync(connection, transaction, request, identities, cancellationToken);
        await ApplyMergeAccountStateAsync(connection, transaction, adminId, request, accounts, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<List<MergeAccountState>> LockMergeAccountsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, MergeRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select account_id,base_role,blocked_at is not null,first_purchase_at,merged_into
            from licensing.accounts where account_id in (@source,@target)
            order by account_id::text for update
            """, connection, transaction);
        command.Parameters.AddWithValue("source", request.SourceAccountId);
        command.Parameters.AddWithValue("target", request.TargetAccountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<MergeAccountState>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4)));
        return result;
    }

    private static async Task<bool> HasActiveReservationsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, MergeRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select exists(select 1 from licensing.reservations
              where account_id in (@source,@target) and state in ('reserved','review_required'))
            """, connection, transaction);
        command.Parameters.AddWithValue("source", request.SourceAccountId);
        command.Parameters.AddWithValue("target", request.TargetAccountId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task TransferActiveGrantsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid adminId,
        MergeRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select grant_id,kind,valid_until,available,reserved,original_amount
            from licensing.entitlement_grants
            where account_id=@source and revoked_at is null and valid_from<=@now
              and (valid_until is null or valid_until>@now)
            order by created_at,grant_id for update
            """, connection, transaction);
        command.Parameters.AddWithValue("source", request.SourceAccountId);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var grants = new List<MergeGrantState>();
        while (await reader.ReadAsync(cancellationToken))
            grants.Add(new(reader.GetGuid(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5)));
        await reader.DisposeAsync();
        if (grants.Any(grant => grant.Reserved > 0))
            throw new FinancialMergeRequiresReconciliationException();
        foreach (var grant in grants)
            await TransferGrantAsync(connection, transaction, adminId, request, grant, now, cancellationToken);
    }

    private static async Task TransferGrantAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid adminId,
        MergeRequest request, MergeGrantState grant, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var transferable = grant.Kind is "credits" or "hybrid" ? grant.Available : 0L;
        if (grant.Kind is "credits" or "hybrid" && transferable <= 0)
        {
            await RevokeSourceGrantAsync(connection, transaction, grant.Id, now, cancellationToken);
            return;
        }
        var targetGrantId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,valid_from,valid_until,available,reserved,
              original_amount,admin_id,reason,created_at)
            values(@id,@target,@kind,'adjustment',@start,@end,@available,0,@original,@admin,@reason,@now)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("id", targetGrantId);
            insert.Parameters.AddWithValue("target", request.TargetAccountId);
            insert.Parameters.AddWithValue("kind", grant.Kind);
            insert.Parameters.AddWithValue("start", now);
            insert.Parameters.AddWithValue("end", (object?)grant.EndsAt ?? DBNull.Value);
            insert.Parameters.AddWithValue("available", transferable);
            insert.Parameters.AddWithValue("original", grant.Kind is "credits" or "hybrid" ? transferable : grant.OriginalAmount);
            insert.Parameters.AddWithValue("admin", adminId);
            insert.Parameters.AddWithValue("reason", "account merge: " + request.Reason);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await RevokeSourceGrantAsync(connection, transaction, grant.Id, now, cancellationToken);
        if (transferable > 0)
        {
            await InsertMergeLedgerAsync(connection, transaction, request.SourceAccountId, grant.Id,
                -transferable, adminId, request.Reason, cancellationToken);
            await InsertMergeLedgerAsync(connection, transaction, request.TargetAccountId, targetGrantId,
                transferable, adminId, request.Reason, cancellationToken);
        }
    }

    private static async Task RevokeSourceGrantAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid grantId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "update licensing.entitlement_grants set available=0,revoked_at=@now where grant_id=@grant",
            connection, transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("grant", grantId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertMergeLedgerAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId, Guid grantId,
        long availableDelta, Guid adminId, string reason, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.credit_ledger(
              ledger_id,account_id,grant_id,event_kind,available_delta,reserved_delta,
              spent_delta,void_delta,actor_account_id,reason)
            values(@id,@account,@grant,'adjustment',@available,0,0,0,@admin,@reason)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("grant", grantId);
        command.Parameters.AddWithValue("available", availableDelta);
        command.Parameters.AddWithValue("admin", adminId);
        command.Parameters.AddWithValue("reason", "account merge: " + reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MoveIdentitiesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, MergeRequest request,
        IReadOnlyList<StoredIdentity> identities, CancellationToken cancellationToken)
    {
        await using (var remove = new NpgsqlCommand(
            "delete from licensing.identities where account_id=@source", connection, transaction))
        {
            remove.Parameters.AddWithValue("source", request.SourceAccountId);
            await remove.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var identity in identities)
            await InsertStoredIdentityAsync(connection, transaction,
                request.TargetAccountId, identity, cancellationToken);
    }

    private static async Task ApplyMergeAccountStateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid adminId,
        MergeRequest request, IReadOnlyList<MergeAccountState> accounts,
        CancellationToken cancellationToken)
    {
        var source = accounts.Single(account => account.Id == request.SourceAccountId);
        var target = accounts.Single(account => account.Id == request.TargetAccountId);
        var purchaseAt = target.FirstPurchaseAt is null ? source.FirstPurchaseAt
            : source.FirstPurchaseAt is null ? target.FirstPurchaseAt
            : target.FirstPurchaseAt < source.FirstPurchaseAt ? target.FirstPurchaseAt : source.FirstPurchaseAt;
        var targetRole = purchaseAt is not null && target.Role == "guest" ? "user" : target.Role;
        await using (var updateTarget = new NpgsqlCommand(
            "update licensing.accounts set base_role=@role,first_purchase_at=@purchase where account_id=@target",
            connection, transaction))
        {
            updateTarget.Parameters.AddWithValue("role", targetRole);
            updateTarget.Parameters.AddWithValue("purchase", (object?)purchaseAt ?? DBNull.Value);
            updateTarget.Parameters.AddWithValue("target", request.TargetAccountId);
            await updateTarget.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var tombstone = new NpgsqlCommand(
            "update licensing.accounts set merged_into=@target where account_id=@source", connection, transaction))
        {
            tombstone.Parameters.AddWithValue("target", request.TargetAccountId);
            tombstone.Parameters.AddWithValue("source", request.SourceAccountId);
            await tombstone.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var relation = new NpgsqlCommand("""
            insert into licensing.account_merge_relations(
              source_account_id,target_account_id,actor_account_id,reason)
            values(@source,@target,@admin,@reason)
            """, connection, transaction);
        relation.Parameters.AddWithValue("source", request.SourceAccountId);
        relation.Parameters.AddWithValue("target", request.TargetAccountId);
        relation.Parameters.AddWithValue("admin", adminId);
        relation.Parameters.AddWithValue("reason", request.Reason);
        await relation.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<StoredIdentity>> ReadIdentitiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select identity_id,issuer,provider,provider_subject,verified_email,linked_at
            from licensing.identities where account_id=@account order by identity_id
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StoredIdentity>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new StoredIdentity(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        return result;
    }

    private static async Task InsertStoredIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid targetAccountId,
        StoredIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.identities(
              identity_id,account_id,issuer,provider,provider_subject,verified_email,linked_at)
            values(@id,@account,@issuer,@provider,@subject,@email,@linked)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", identity.IdentityId);
        command.Parameters.AddWithValue("account", targetAccountId);
        command.Parameters.AddWithValue("issuer", identity.Issuer);
        command.Parameters.AddWithValue("provider", identity.Provider);
        command.Parameters.AddWithValue("subject", identity.Subject);
        command.Parameters.AddWithValue("email", (object?)identity.VerifiedEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("linked", identity.LinkedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RevokeSessionsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand(
            "update licensing.api_sessions set revoked_at=@now where account_id=@account and revoked_at is null",
            connection, transaction);
        command.Parameters.AddWithValue("now", _clock.GetUtcNow());
        command.Parameters.AddWithValue("account", accountId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task AuditMergeAsync(
        Guid adminId,
        MergeRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, adminId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.audit_events(
              event_id,account_id,actor_account_id,event_type,details)
            values(@id,@target,@admin,'account_merged',
              jsonb_build_object('source',@source,'reason',@reason))
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("target", request.TargetAccountId);
        command.Parameters.AddWithValue("admin", adminId);
        command.Parameters.AddWithValue("source", request.SourceAccountId.ToString("D"));
        command.Parameters.AddWithValue("reason", request.Reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string MergeNonce(MergeRequest request, string side)
        => $"merge:{request.SourceAccountId:D}:{request.TargetAccountId:D}:{side}";

    private sealed record StoredIdentity(Guid IdentityId, string Issuer, string Provider,
        string Subject, string? VerifiedEmail, DateTimeOffset LinkedAt);
    private sealed record MergeAccountState(Guid Id, string Role, bool Blocked,
        DateTimeOffset? FirstPurchaseAt, Guid? MergedInto);
    private sealed record MergeGrantState(Guid Id, string Kind, DateTimeOffset? EndsAt,
        long Available, long Reserved, long OriginalAmount);

    public async Task<LinkChallenge> BeginRecoveryAsync(Guid adminId, Guid accountId, string proofSource, string reason, bool mfaVerified, CancellationToken cancellationToken)
    {
        if (!mfaVerified) throw new UnauthorizedAccessException("Fresh admin MFA is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(proofSource); ArgumentException.ThrowIfNullOrWhiteSpace(reason); await EnsureAdminAsync(adminId,cancellationToken);
        var now=_clock.GetUtcNow(); var challenge=new LinkChallenge(Guid.NewGuid(),now.Add(ChallengeLifetime)); await using var c=await _adminDataSource.OpenConnectionAsync(cancellationToken); await using var tx=await c.BeginTransactionAsync(cancellationToken);
        await using var q=new NpgsqlCommand("insert into licensing.account_links(challenge_id,account_id,created_at,expires_at,purpose,created_by_admin,proof_source,reason) values(@id,@a,@now,@exp,'recovery',@admin,@source,@reason)",c,tx); q.Parameters.AddWithValue("id",challenge.ChallengeId); q.Parameters.AddWithValue("a",accountId); q.Parameters.AddWithValue("now",now); q.Parameters.AddWithValue("exp",challenge.ExpiresAt); q.Parameters.AddWithValue("admin",adminId); q.Parameters.AddWithValue("source",proofSource); q.Parameters.AddWithValue("reason",reason); await q.ExecuteNonQueryAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        await RevokeSessionsAsync(accountId,cancellationToken); await AuditRecoveryAsync(adminId,accountId,"recovery_reviewed",proofSource,reason,cancellationToken); return challenge;
    }

    public async Task CompleteRecoveryAsync(LinkProof proof, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proof.FreshProviderAssertion); var recovery=await ClaimRecoveryAsync(proof.ChallengeId,cancellationToken) ?? throw new UnauthorizedAccessException("Recovery challenge is unavailable.");
        var provider=ReadProviderHint(proof.FreshProviderAssertion); if(!_partitions.ContainsKey(provider)) throw new UnauthorizedAccessException("Unknown recovery provider."); var identity=await _tokens.ValidateAsync(proof.FreshProviderAssertion,provider,proof.ChallengeId.ToString("D"),cancellationToken);
        if (provider.Equals("email", StringComparison.OrdinalIgnoreCase)) { var owner=await FindIdentityOwnerAsync(identity,cancellationToken); if(owner!=recovery.AccountId) throw new UnauthorizedAccessException("Verified email identity is not linked to the recovery account."); } else await LinkIdentityAsync(recovery.AccountId,identity,cancellationToken);
        await AuditRecoveryAsync(recovery.AdminId,recovery.AccountId,"recovery_completed",provider,recovery.Reason,cancellationToken);
    }

    private async Task<RecoveryChallenge?> ClaimRecoveryAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var now=_clock.GetUtcNow(); await using var c=await _adminDataSource.OpenConnectionAsync(cancellationToken); await using var tx=await c.BeginTransactionAsync(cancellationToken); await using var q=new NpgsqlCommand("update licensing.account_links set consumed_at=@now where challenge_id=@id and purpose='recovery' and consumed_at is null and expires_at>@now returning account_id,created_by_admin,reason",c,tx); q.Parameters.AddWithValue("now",now); q.Parameters.AddWithValue("id",challengeId); await using var r=await q.ExecuteReaderAsync(cancellationToken); if(!await r.ReadAsync(cancellationToken)) return null; var value=new RecoveryChallenge(r.GetGuid(0),r.GetGuid(1),r.IsDBNull(2)?"":r.GetString(2)); await r.DisposeAsync(); await tx.CommitAsync(cancellationToken); return value;
    }

    private async Task AuditRecoveryAsync(Guid adminId, Guid accountId, string eventType, string source, string reason, CancellationToken cancellationToken)
    {
        await using var c=await _adminDataSource.OpenConnectionAsync(cancellationToken); await using var q=new NpgsqlCommand("insert into licensing.audit_events(event_id,account_id,actor_account_id,event_type,details) values(@id,@a,@admin,@event,jsonb_build_object('source',@source,'reason',@reason))",c); q.Parameters.AddWithValue("id",Guid.NewGuid()); q.Parameters.AddWithValue("a",accountId); q.Parameters.AddWithValue("admin",adminId); q.Parameters.AddWithValue("event",eventType); q.Parameters.AddWithValue("source",source); q.Parameters.AddWithValue("reason",reason); await q.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record RecoveryChallenge(Guid AccountId, Guid AdminId, string Reason);

    private async Task AuditAsync(
        Guid accountId,
        string eventType,
        string provider,
        CancellationToken cancellationToken)
    {
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.audit_events(
              event_id,account_id,actor_account_id,event_type,details)
            values(@id,@account,@account,@event,jsonb_build_object('provider',@provider))
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("event", eventType);
        command.Parameters.AddWithValue("provider", provider);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string ReadProviderHint(string assertion)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(assertion);
            return jwt.Claims.FirstOrDefault(claim => claim.Type == "provider")?.Value
                ?? throw new UnauthorizedAccessException("Provider claim is missing.");
        }
        catch (ArgumentException ex)
        {
            throw new UnauthorizedAccessException("Provider assertion is malformed.", ex);
        }
    }

    private static async Task SetAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select set_config('vg.account_id',@account,true)", connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlDataSource CreateAdminDataSource(IConfiguration configuration)
    {
        var dsn = configuration.GetConnectionString("PlatformAdmin") ?? configuration["VG_PLATFORM_ADMIN_DSN"] ?? throw new InvalidOperationException("Platform admin database DSN is not configured.");
        return NpgsqlDataSource.Create(dsn);
    }

    private static NpgsqlDataSource CreateIdentityDataSource(IConfiguration configuration)
    {
        var dsn = configuration.GetConnectionString("PlatformIdentity")
            ?? configuration["VG_PLATFORM_IDENTITY_DSN"]
            ?? throw new InvalidOperationException("Platform identity database DSN is not configured.");
        return NpgsqlDataSource.Create(dsn);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsIdentityDataSource) await _identityDataSource.DisposeAsync();
        if (_ownsAdminDataSource) await _adminDataSource.DisposeAsync();
    }
}
