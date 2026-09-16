using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace VideoGrabber.Platform.Persistence;

public static class MigrationRunner
{
    private const long AdvisoryLockKey = 0x5647504C41544631;

    public static async Task ApplyAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var acquire = new NpgsqlCommand(
            "select pg_advisory_lock(@key)", connection);
        acquire.Parameters.AddWithValue("key", AdvisoryLockKey);
        await acquire.ExecuteNonQueryAsync(cancellationToken);
        try
        {
            await EnsureHistoryAsync(connection, cancellationToken);
            foreach (var migration in ReadMigrations())
                await ApplyOneAsync(connection, migration, cancellationToken);
        }
        finally
        {
            await using var release = new NpgsqlCommand(
                "select pg_advisory_unlock(@key)", connection);
            release.Parameters.AddWithValue("key", AdvisoryLockKey);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static async Task EnsureHistoryAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            create schema if not exists vg_migrations;
            create table if not exists vg_migrations.applied_migrations (
              name text primary key,
              sha256 text not null,
              applied_at timestamptz not null default now());
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyOneAsync(
        NpgsqlConnection connection,
        Migration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var check = new NpgsqlCommand(
            "select sha256 from vg_migrations.applied_migrations where name=@name",
            connection, transaction);
        check.Parameters.AddWithValue("name", migration.Name);
        var existing = (string?)await check.ExecuteScalarAsync(cancellationToken);
        if (existing is not null)
        {
            if (!existing.Equals(migration.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Migration hash mismatch for {migration.Name}.");
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await using var apply = new NpgsqlCommand(migration.Sql, connection, transaction);
        await apply.ExecuteNonQueryAsync(cancellationToken);
        await using var record = new NpgsqlCommand(
            "insert into vg_migrations.applied_migrations(name,sha256) values(@name,@sha)",
            connection, transaction);
        record.Parameters.AddWithValue("name", migration.Name);
        record.Parameters.AddWithValue("sha", migration.Sha256);
        await record.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static IReadOnlyList<Migration> ReadMigrations()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => ReadMigration(assembly, name))
            .ToArray();
    }

    private static Migration ReadMigration(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing migration resource {resourceName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        var sql = Encoding.UTF8.GetString(bytes);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var marker = ".Migrations.";
        var index = resourceName.IndexOf(marker, StringComparison.Ordinal);
        var name = index >= 0 ? resourceName[(index + marker.Length)..] : resourceName;
        return new Migration(name, sha256, sql);
    }

    private sealed record Migration(string Name, string Sha256, string Sql);
}
