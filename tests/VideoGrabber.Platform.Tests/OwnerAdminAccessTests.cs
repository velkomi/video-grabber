using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using VideoGrabber.Platform.Api.Admin;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class OwnerAdminAccessTests
{
    [Fact]
    public async Task Configured_owner_subject_is_the_only_owner_admin()
    {
        await using var fixture = await ApiFixture.StartAsync();
        var ownerSubject = Guid.NewGuid().ToString("D");
        var otherSubject = Guid.NewGuid().ToString("D");

        var owner = await fixture.AccountAsync("google", ownerSubject, "owner@example.test");
        var other = await fixture.AccountAsync("google", otherSubject, "other@example.test");
        await fixture.PromoteAdminAsync(owner.Id);
        await fixture.PromoteAdminAsync(other.Id);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PlatformAdmin"] = fixture.TestDatabaseConnectionString,
                ["VG_OWNER_ADMIN_SUBJECT"] = ownerSubject
            })
            .Build();

        await using var policy = new OwnerAdminAccessService(
            configuration,
            NullLogger<OwnerAdminAccessService>.Instance);
        await policy.StartAsync(CancellationToken.None);

        await using var connection = await fixture.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            select
              count(*) filter (where base_role='owner_admin') as owner_count,
              bool_and(case when account_id=@owner
                then base_role='owner_admin'
                else base_role<>'owner_admin'
              end) as exclusive
            from licensing.accounts
            where account_id in (@owner,@other)
            """, connection);
        command.Parameters.AddWithValue("owner", owner.Id);
        command.Parameters.AddWithValue("other", other.Id);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.True(reader.GetBoolean(1));
    }
}
