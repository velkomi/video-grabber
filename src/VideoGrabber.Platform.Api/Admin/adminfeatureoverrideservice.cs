using System.Text.Json;
using Npgsql;

namespace VideoGrabber.Platform.Api.Admin;

public sealed record AdminFeatureOverrideView(
    string Feature,
    bool Enabled,
    DateTimeOffset? ValidUntil,
    string Reason,
    Guid AdminId,
    DateTimeOffset UpdatedAt);

public sealed record AdminFeatureOverrideRequest(
    bool Enabled,
    DateTimeOffset? ValidUntil,
    string Reason);

public sealed class AdminFeatureOverrideService : IAsyncDisposable
{
    public static readonly IReadOnlySet<string> SupportedFeatures =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "download",
            "mp3",
            "trim",
            "join",
            "transcribe",
            "course_download",
            "browser_download",
            "telegram_delivery"
        };

    private readonly NpgsqlDataSource _apiDataSource;
    private readonly NpgsqlDataSource _adminDataSource;
    private readonly TimeProvider _clock;
    private readonly bool _ownsAdminDataSource;

    private AdminFeatureOverrideService(
        NpgsqlDataSource apiDataSource,
        NpgsqlDataSource adminDataSource,
        TimeProvider clock,
        bool ownsAdminDataSource)
    {
        _apiDataSource = apiDataSource;
        _adminDataSource = adminDataSource;
        _clock = clock;
        _ownsAdminDataSource = ownsAdminDataSource;
    }

    public static AdminFeatureOverrideService CreateOwned(
        NpgsqlDataSource apiDataSource,
        string adminDsn,
        TimeProvider clock)
        => new(
            apiDataSource,
            NpgsqlDataSource.Create(adminDsn),
            clock,
            true);

    public static AdminFeatureOverrideService CreateForTesting(
        NpgsqlDataSource apiDataSource,
        NpgsqlDataSource adminDataSource,
        TimeProvider clock)
        => new(apiDataSource, adminDataSource, clock, false);

    public async Task<IReadOnlyDictionary<string, bool>> ReadEffectiveAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var connection =
            await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(
            connection, transaction, accountId, cancellationToken);

        await using var command = new NpgsqlCommand("""
            select feature,enabled
            from licensing.admin_feature_overrides
            where account_id=@account
              and (valid_until is null or valid_until>@now)
            order by feature
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("now", now);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetString(0)] = reader.GetBoolean(1);
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool?> ReadEffectiveAsync(
        Guid accountId,
        string feature,
        CancellationToken cancellationToken)
    {
        ValidateFeature(feature);
        var values = await ReadEffectiveAsync(accountId, cancellationToken);
        return values.TryGetValue(feature, out var enabled)
            ? enabled
            : null;
    }

    public async Task<IReadOnlyList<AdminFeatureOverrideView>> ListAdminAsync(
        Guid actorId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(
            connection, transaction, actorId, cancellationToken);

        await using var command = new NpgsqlCommand("""
            select feature,enabled,valid_until,reason,admin_id,updated_at
            from licensing.admin_feature_overrides
            where account_id=@account
            order by feature
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AdminFeatureOverrideView>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(
                reader.GetString(0),
                reader.GetBoolean(1),
                reader.IsDBNull(2)
                    ? null
                    : reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetString(3),
                reader.GetGuid(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task SetAsync(
        Guid actorId,
        Guid accountId,
        string feature,
        AdminFeatureOverrideRequest request,
        CancellationToken cancellationToken)
    {
        ValidateFeature(feature);
        ValidateReason(request.Reason);
        var now = _clock.GetUtcNow();
        if (request.ValidUntil is { } expiry && expiry <= now)
            throw new ArgumentException(
                "Feature override expiry must be in the future.");

        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(
            connection, transaction, actorId, cancellationToken);
        await EnsureAccountAsync(
            connection, transaction, accountId, cancellationToken);

        await using (var command = new NpgsqlCommand("""
            insert into licensing.admin_feature_overrides(
              account_id,feature,enabled,valid_until,reason,admin_id,
              created_at,updated_at)
            values(@account,@feature,@enabled,@until,@reason,@admin,@now,@now)
            on conflict(account_id,feature) do update set
              enabled=excluded.enabled,
              valid_until=excluded.valid_until,
              reason=excluded.reason,
              admin_id=excluded.admin_id,
              updated_at=excluded.updated_at
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("account", accountId);
            command.Parameters.AddWithValue("feature", feature);
            command.Parameters.AddWithValue("enabled", request.Enabled);
            command.Parameters.AddWithValue(
                "until",
                (object?)request.ValidUntil ?? DBNull.Value);
            command.Parameters.AddWithValue("reason", request.Reason);
            command.Parameters.AddWithValue("admin", actorId);
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await AuditAsync(
            connection,
            transaction,
            actorId,
            accountId,
            "feature_override_set",
            new
            {
                feature,
                enabled = request.Enabled,
                validUntil = request.ValidUntil,
                reason = request.Reason
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid actorId,
        Guid accountId,
        string feature,
        string reason,
        CancellationToken cancellationToken)
    {
        ValidateFeature(feature);
        ValidateReason(reason);
        await using var connection =
            await _adminDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(
            connection, transaction, actorId, cancellationToken);

        await using var command = new NpgsqlCommand("""
            delete from licensing.admin_feature_overrides
            where account_id=@account and feature=@feature
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("feature", feature);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AuditAsync(
            connection,
            transaction,
            actorId,
            accountId,
            "feature_override_removed",
            new { feature, reason },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateFeature(string feature)
    {
        if (!SupportedFeatures.Contains(feature))
            throw new ArgumentException("Unknown feature override.");
    }

    private static void ValidateReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 500)
            throw new ArgumentException("Reason is too long.");
    }

    private static async Task EnsureAdminAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select base_role,blocked_at is not null
            from licensing.accounts
            where account_id=@actor
            """, connection, transaction);
        command.Parameters.AddWithValue("actor", actorId);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.GetString(0) != "owner_admin"
            || reader.GetBoolean(1))
            throw new UnauthorizedAccessException(
                "Owner admin authorization is required.");
    }

    private static async Task EnsureAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select exists(
              select 1 from licensing.accounts where account_id=@account)
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
            throw new KeyNotFoundException("Account was not found.");
    }

    private static async Task SetAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select set_config('vg.account_id',@account,true)",
            connection,
            transaction);
        command.Parameters.AddWithValue(
            "account",
            accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorId,
        Guid accountId,
        string eventType,
        object details,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.audit_events(
              event_id,account_id,actor_account_id,event_type,details,created_at)
            values(@event,@account,@actor,@event_type,@details::jsonb,now())
            """, connection, transaction);
        command.Parameters.AddWithValue("event", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("actor", actorId);
        command.Parameters.AddWithValue("event_type", eventType);
        command.Parameters.AddWithValue(
            "details",
            JsonSerializer.Serialize(details));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsAdminDataSource)
            await _adminDataSource.DisposeAsync();
    }
}
