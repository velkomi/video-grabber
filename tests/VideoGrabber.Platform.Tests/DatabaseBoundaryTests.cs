using Npgsql;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DatabaseBoundaryTests
{
    [Fact]
    public async Task Missing_tenant_context_reads_no_accounts()
    {
        await using var f = await ApiFixture.StartAsync();
        await f.AccountAsync("google", "boundary-one");
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var role = new NpgsqlCommand("set local role vg_api", connection, transaction);
        await role.ExecuteNonQueryAsync();
        await using var query = new NpgsqlCommand("select count(*) from licensing.accounts", connection, transaction);
        Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Api_role_cannot_bypass_rls_or_assume_admin_role()
    {
        await using var f = await ApiFixture.StartAsync();
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var check = new NpgsqlCommand("""
            select rolbypassrls, pg_has_role('vg_api','vg_admin','MEMBER')
            from pg_roles where rolname='vg_api'
            """, connection);
        await using var reader = await check.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
    }

    [Fact]
    public async Task Api_role_cannot_reassign_identity_owner()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("google", "owner-a");
        var b = await f.AccountAsync("apple", "owner-b");
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var role = new NpgsqlCommand("set local role vg_api", connection, transaction);
        await role.ExecuteNonQueryAsync();
        await using var update = new NpgsqlCommand(
            "update licensing.identities set account_id=@b where account_id=@a", connection, transaction);
        update.Parameters.AddWithValue("a", a.Id); update.Parameters.AddWithValue("b", b.Id);
        await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Public_has_no_licensing_schema_or_account_table_access()
    {
        await using var f = await ApiFixture.StartAsync();
        await using var c = await f.Database.OpenConnectionAsync();        await using var q = new NpgsqlCommand("""
            select has_schema_privilege('public','licensing','USAGE'),
                   has_table_privilege('public','licensing.accounts','SELECT')
            """, c);
        await using var r = await q.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        Assert.False(r.GetBoolean(0));
        Assert.False(r.GetBoolean(1));
    }

    [Fact]
    public async Task Set_local_tenant_context_does_not_leak_after_commit()
    {
        await using var f = await ApiFixture.StartAsync();
        await using var c = await f.Database.OpenConnectionAsync();
        await using (var tx = await c.BeginTransactionAsync())
        {
            await using var q = new NpgsqlCommand("select set_config('vg.account_id',@id,true)", c, tx);
            q.Parameters.AddWithValue("id", Guid.NewGuid().ToString("D"));
            await q.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        await using var check = new NpgsqlCommand("select current_setting('vg.account_id',true)", c);
        var value = (string?)await check.ExecuteScalarAsync();
        Assert.True(string.IsNullOrEmpty(value));
    }

    [Fact]
    public async Task Api_role_cannot_run_schema_migration_or_create_objects()
    {
        await using var f = await ApiFixture.StartAsync();
        await using var c = await f.Database.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using var role = new NpgsqlCommand("set local role vg_api", c, tx);
        await role.ExecuteNonQueryAsync();
        await using var create = new NpgsqlCommand(
            "create table licensing.forbidden_runtime_ddl(id int)", c, tx);
        await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync());
    }
}
