using Npgsql;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class RestoreConsistencyTests
{
    [Fact]
    public async Task New_platform_database_has_no_accounting_violations()
    {
        await using var f = await ApiFixture.StartAsync();

        var report = await ConsistencyReport.RunAsync(
            f.Database, CancellationToken.None);

        Assert.NotEmpty(report);
        Assert.All(report, pair => Assert.Equal(0, pair.Value));
    }

    [Fact]
    public async Task Broken_credit_projection_is_detected()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "restore-broken");
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,valid_from,valid_until,
              available,reserved,original_amount,reason,created_at)
            values(@grant,@account,'credits','adjustment',@now,null,
              7,0,7,'deliberate broken restore fixture',@now)
            """, connection);
        command.Parameters.AddWithValue("grant", Guid.NewGuid());
        command.Parameters.AddWithValue("account", account.Id);
        command.Parameters.AddWithValue("now", f.Clock.GetUtcNow());
        await command.ExecuteNonQueryAsync();

        var report = await ConsistencyReport.RunAsync(
            f.Database, CancellationToken.None);

        Assert.Equal(1, report["credit_grant_ledger_mismatch"]);
    }

    [Fact]
    public async Task Completed_job_without_artifact_is_detected()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "restore-job-broken");
        var sourceId = await f.SourceAsync(account.Id, "720p");
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            insert into licensing.jobs(
              job_id,account_id,intent_id,request_hash,kind,executor,source_id,
              quality,input_artifact_ids,state,fence,reason,created_at,updated_at)
            values(@job,@account,@intent,@hash,'download','server_worker',@source,
              '720p',array[]::uuid[],'completed',1,'deliberate missing artifact',@now,@now)
            """, connection);
        command.Parameters.AddWithValue("job", Guid.NewGuid());
        command.Parameters.AddWithValue("account", account.Id);
        command.Parameters.AddWithValue("intent", Guid.NewGuid());
        command.Parameters.AddWithValue("hash", new string('a',64));
        command.Parameters.AddWithValue("source", sourceId);
        command.Parameters.AddWithValue("now", f.Clock.GetUtcNow());
        await command.ExecuteNonQueryAsync();

        var report = await ConsistencyReport.RunAsync(
            f.Database, CancellationToken.None);

        Assert.Equal(1, report["completed_job_missing_artifact"]);
    }
}
