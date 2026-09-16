using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class ProfileIsolationTests
{
    [Fact]
    public async Task Email_equality_never_selects_another_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("google", "subject-a", "same@example.test");
        var b = await f.AccountAsync("apple", "subject-b", "same@example.test");
        Assert.NotEqual(a.Id, b.Id);
        var response = await a.Client.GetAsync($"/v1/accounts/{b.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var profile = await a.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.Equal(a.Id, profile!.AccountId);
    }

    [Fact]
    public async Task No_identity_is_unauthorized()
    {
        await using var f = await ApiFixture.StartAsync();
        var response = await f.Anonymous.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Incompatible_api_version_requires_upgrade()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "version-subject");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        request.Headers.Add("X-VideoGrabber-Api-Version", "999");
        var response = await account.Client.SendAsync(request);
        Assert.Equal((HttpStatusCode)426, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("client_upgrade_required", body, StringComparison.Ordinal);
    }
}

public sealed class ProfilePersistenceTests
{
    [Fact]
    public async Task Repeated_provider_subject_resolves_the_same_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var first = await f.AccountAsync("google", "stable-subject", "one@example.test");
        var second = await f.AccountAsync("google", "stable-subject", "changed@example.test");

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Api_role_with_account_a_context_cannot_read_account_b()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("google", "subject-a");
        var b = await f.AccountAsync("apple", "subject-b");

        await using var connection = await f.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var role = new Npgsql.NpgsqlCommand("set local role vg_api", connection, transaction);
        await role.ExecuteNonQueryAsync();
        await using var tenant = new Npgsql.NpgsqlCommand("select set_config('vg.account_id', @id, true)", connection, transaction);
        tenant.Parameters.AddWithValue("id", a.Id.ToString("D"));
        await tenant.ExecuteNonQueryAsync();
        await using var query = new Npgsql.NpgsqlCommand("select count(*) from licensing.accounts where account_id=@id", connection, transaction);
        query.Parameters.AddWithValue("id", b.Id);
        Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
    }
}
public sealed class MigrationAcceptanceTests
{
    [Fact]
    public async Task Migration_runner_is_idempotent_and_records_sha256()
    {
        await using var f = await ApiFixture.StartAsync();
        await MigrationRunner.ApplyAsync(f.Database, CancellationToken.None);

        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "select count(*), min(length(sha256)) from vg_migrations.applied_migrations", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(64, reader.GetInt32(1));
    }
}