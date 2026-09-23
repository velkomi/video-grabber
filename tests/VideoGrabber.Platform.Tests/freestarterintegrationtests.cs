using System.Net.Http.Json;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class FreeStarterIntegrationTests
{
    [Fact]
    public async Task First_registration_issues_exactly_one_lifetime_free_allowance()
    {
        await using var f = await ApiFixture.StartAsync();

        var first = await f.AccountAsync("email", "free-starter-once", "free@example.test", includeStarter: true);

        await using (var connection = await f.Database.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            select count(*),coalesce(sum(available),0),coalesce(sum(original_amount),0)
            from licensing.entitlement_grants
            where account_id=@account
              and source='system_starter'
              and plan_id='free'
            """, connection))
        {
            command.Parameters.AddWithValue("account", first.Id);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(10L, reader.GetInt64(1));
            Assert.Equal(10L, reader.GetInt64(2));
        }

        var access = await first.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.NotNull(access);
        Assert.Equal("free", access!.PlanId);
        Assert.Equal(10, access.RemainingDownloads);
        Assert.True(access.CanDownload);
        Assert.False(access.CanDownloadCourse);

        var second = await f.AccountAsync("email", "free-starter-once", "free@example.test", includeStarter: true);
        Assert.Equal(first.Id, second.Id);

        await using (var connection = await f.Database.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            select count(*)
            from licensing.entitlement_grants
            where account_id=@account
              and source='system_starter'
              and plan_id='free'
            """, connection))
        {
            command.Parameters.AddWithValue("account", first.Id);
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }
    }
}
