using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Admin;

public sealed class OwnerAdminAccessService : IHostedService, IAsyncDisposable
{
    private readonly NpgsqlDataSource _adminDataSource;
    private readonly ILogger<OwnerAdminAccessService> _logger;
    private readonly string? _ownerSubject;

    public OwnerAdminAccessService(
        IConfiguration configuration,
        ILogger<OwnerAdminAccessService> logger)
    {
        var adminDsn = configuration.GetConnectionString("PlatformAdmin")
            ?? configuration["VG_PLATFORM_ADMIN_DSN"]
            ?? throw new InvalidOperationException(
                "Platform admin database DSN is not configured.");
        _adminDataSource = NpgsqlDataSource.Create(adminDsn);
        _logger = logger;

        var configured = configuration["VG_OWNER_ADMIN_SUBJECT"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Guid.TryParse(configured, out var parsed))
                throw new InvalidOperationException(
                    "VG_OWNER_ADMIN_SUBJECT must be a valid UUID.");
            _ownerSubject = parsed.ToString("D");
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_ownerSubject is null)
        {
            _logger.LogInformation(
                "Exclusive owner-admin subject is not configured.");
            return;
        }

        await ReconcileConfiguredOwnerAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task<AccountProfile> ReconcileIdentityAsync(
        VerifiedIdentity identity,
        AccountProfile profile,
        CancellationToken cancellationToken)
    {
        if (_ownerSubject is null || profile.Blocked)
            return profile;

        if (string.Equals(
                identity.Subject,
                _ownerSubject,
                StringComparison.OrdinalIgnoreCase))
        {
            var active = await SetExclusiveOwnerAsync(
                    profile.AccountId,
                    cancellationToken)
                .ConfigureAwait(false);
            return active
                ? profile with { Role = "owner_admin" }
                : profile;
        }

        if (!string.Equals(
                profile.Role,
                "owner_admin",
                StringComparison.Ordinal))
            return profile;

        var demotedRole = profile.FirstPurchaseAt is null
            ? "guest"
            : "user";
        await DemoteAccountAsync(
                profile.AccountId,
                demotedRole,
                cancellationToken)
            .ConfigureAwait(false);
        return profile with { Role = demotedRole };
    }

    private async Task ReconcileConfiguredOwnerAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            select distinct a.account_id
            from licensing.accounts a
            join licensing.identities i
              on i.account_id=a.account_id
            where i.provider_subject=@subject
              and a.merged_into is null
              and a.blocked_at is null
            order by a.account_id
            limit 2
            """, connection);
        command.Parameters.AddWithValue("subject", _ownerSubject!);

        var matches = new List<Guid>(2);
        await using (var reader =
                     await command.ExecuteReaderAsync(cancellationToken)
                         .ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken)
                       .ConfigureAwait(false))
                matches.Add(reader.GetGuid(0));
        }

        if (matches.Count == 0)
        {
            _logger.LogInformation(
                "Configured owner-admin identity has no active VideoGrabber account yet.");
            return;
        }

        if (matches.Count != 1)
            throw new InvalidOperationException(
                "Configured owner-admin identity maps to multiple accounts.");

        await SetExclusiveOwnerAsync(matches[0], cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> SetExclusiveOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

        var exists = false;
        await using (var verify = new NpgsqlCommand("""
            select exists(
              select 1
              from licensing.accounts
              where account_id=@owner
                and merged_into is null
                and blocked_at is null)
            """, connection, transaction))
        {
            verify.Parameters.AddWithValue("owner", ownerAccountId);
            exists = (bool)(await verify.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false) ?? false);
        }

        if (!exists)
        {
            await transaction.RollbackAsync(CancellationToken.None)
                .ConfigureAwait(false);
            return false;
        }

        var changed = 0;
        await using (var promote = new NpgsqlCommand("""
            update licensing.accounts
            set base_role='owner_admin'
            where account_id=@owner
              and base_role<>'owner_admin'
            """, connection, transaction))
        {
            promote.Parameters.AddWithValue("owner", ownerAccountId);
            changed += await promote.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await using (var demote = new NpgsqlCommand("""
            update licensing.accounts
            set base_role=case
              when first_purchase_at is null then 'guest'
              else 'user'
            end
            where account_id<>@owner
              and base_role='owner_admin'
            """, connection, transaction))
        {
            demote.Parameters.AddWithValue("owner", ownerAccountId);
            changed += await demote.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (changed > 0)
        {
            await using var audit = new NpgsqlCommand("""
                insert into licensing.audit_events(
                  event_id,account_id,actor_account_id,event_type,details)
                values(
                  gen_random_uuid(),@owner,null,'owner_admin_reconciled',
                  jsonb_build_object(
                    'exclusive',true,
                    'changed_accounts',@changed))
                """, connection, transaction);
            audit.Parameters.AddWithValue("owner", ownerAccountId);
            audit.Parameters.AddWithValue("changed", changed);
            await audit.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private async Task DemoteAccountAsync(
        Guid accountId,
        string targetRole,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

        var changed = 0;
        await using (var demote = new NpgsqlCommand("""
            update licensing.accounts
            set base_role=@role
            where account_id=@account
              and base_role='owner_admin'
            """, connection, transaction))
        {
            demote.Parameters.AddWithValue("role", targetRole);
            demote.Parameters.AddWithValue("account", accountId);
            changed = await demote.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (changed > 0)
        {
            await using var audit = new NpgsqlCommand("""
                insert into licensing.audit_events(
                  event_id,account_id,actor_account_id,event_type,details)
                values(
                  gen_random_uuid(),@account,null,'owner_admin_revoked',
                  jsonb_build_object('exclusive',true))
                """, connection, transaction);
            audit.Parameters.AddWithValue("account", accountId);
            await audit.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
        => _adminDataSource.DisposeAsync();
}
