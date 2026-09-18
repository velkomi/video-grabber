using System.Net;
using System.Net.Http.Json;
using Npgsql;
using VideoGrabber.Platform.Core.Jobs;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class JobRecoveryTests
{
    [Fact]
    public void Canonical_hash_excludes_RequestHash_and_preserves_input_order()
    {
        var intent = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var a = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var b = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var request = new CreateJob(intent, "ignored", "download", "server_worker",
            null, "source-1", "720p", [a, b], null, null);
        var first = JobRequestHasher.Hash(request);
        var second = JobRequestHasher.Hash(request with { RequestHash = new string('f', 64) });
        var reordered = JobRequestHasher.Hash(request with { InputArtifactIds = [b, a] });
        Assert.Equal(64, first.Length);
        Assert.Equal(first, second);
        Assert.NotEqual(first, reordered);
        Assert.Equal(first, first.ToLowerInvariant());
    }

    [Fact]
    public async Task Admission_replay_returns_one_logical_job()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "job-owner");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "job", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var sourceId = await f.SourceAsync(user.Id, "720p");
        var request = NewJob(sourceId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => user.Client.PostAsJsonAsync("/v1/jobs", request)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var jobs = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<JobView>()));
        Assert.Single(jobs.Select(j => j!.JobId).Distinct());
        Assert.Equal(1L, await CountJobsAsync(f, user.Id));
        var buckets = await CreditBucketsAsync(f, user.Id);
        Assert.Equal((0L, 1L), buckets);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Hash_mismatch_is_rejected_before_credit_reservation()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "job-bad-hash");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "job", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var sourceId = await f.SourceAsync(user.Id, "720p");
        var request = NewJob(sourceId) with { RequestHash = new string('0', 64) };

        using var response = await user.Client.PostAsJsonAsync("/v1/jobs", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0L, await CountJobsAsync(f, user.Id));
        Assert.Equal((1L, 0L), await CreditBucketsAsync(f, user.Id));
    }

    [Fact]
    public async Task Crash_after_reservation_rolls_back_credit_and_job_atomically()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "job-crash");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "job", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var sourceId = await f.SourceAsync(user.Id, "720p");
        var ledger = f.Service<CreditLedger>();
        var faulted = JobStore.CreateForTesting(ledger, f.Clock,
            point => { if (point == "after_reservation") throw new InvalidOperationException("synthetic crash"); });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            faulted.CreateAsync(user.Id, NewJob(sourceId), CancellationToken.None));

        Assert.Equal(0L, await CountJobsAsync(f, user.Id));
        Assert.Equal((1L, 0L), await CreditBucketsAsync(f, user.Id));
        Assert.Equal(0L, await CountReservationsAsync(f, user.Id));
    }

    [Fact]
    public async Task Expired_attempt_is_refenced_and_stale_completion_cannot_commit()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "job-fence");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "job", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var sourceId = await f.SourceAsync(user.Id, "720p");
        var store = f.Service<JobStore>();
        var job = await store.CreateAsync(user.Id, NewJob(sourceId), CancellationToken.None);
        var oldLease = (await store.ClaimAsync(Guid.NewGuid(), null, null, CancellationToken.None))!;
        Assert.True(await store.HeartbeatAsync(oldLease, CancellationToken.None));
        f.Clock.Advance(TimeSpan.FromSeconds(61));
        var fresh = (await store.ClaimAsync(Guid.NewGuid(), null, null, CancellationToken.None))!;
        Assert.Equal(job.JobId, fresh.JobId);
        Assert.True(fresh.Fence > oldLease.Fence);

        var stale = Completion(oldLease, "success");
        await Assert.ThrowsAsync<JobFenceConflictException>(() => store.CompleteAsync(stale, CancellationToken.None));
        var completed = await store.CompleteAsync(Completion(fresh, "success"), CancellationToken.None);
        Assert.Equal("completed", completed.State);
        var duplicate = await store.CompleteAsync(Completion(fresh, "success"), CancellationToken.None);
        Assert.Equal(completed, duplicate);
        Assert.Equal(1L, await CountArtifactsAsync(f, job.JobId));
        Assert.Equal(1L, await CountOutboxAsync(f, job.JobId, "job_completed"));
        Assert.Equal((0L, 0L), await CreditBucketsAsync(f, user.Id));
    }

    [Fact]
    public async Task Foreign_account_cannot_read_or_cancel_existing_job()
    {
        await using var f = await ApiFixture.StartAsync();
        var owner = await f.AccountAsync("telegram", "job-private-owner");
        var foreign = await f.AccountAsync("telegram", "job-private-foreign");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(owner.Id, "credits", 0, 1, null, "job", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var sourceId = await f.SourceAsync(owner.Id, "720p");
        using var created = await owner.Client.PostAsJsonAsync("/v1/jobs", NewJob(sourceId));
        created.EnsureSuccessStatusCode();
        var job = (await created.Content.ReadFromJsonAsync<JobView>())!;

        using var read = await foreign.Client.GetAsync($"/v1/jobs/{job.JobId:D}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var cancel = await foreign.Client.PostAsync($"/v1/jobs/{job.JobId:D}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    [Fact]
    public async Task Worker_claim_requires_private_capability_and_completion_can_win_cancel_race_once()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "job-worker-api");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "job", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var sourceId = await f.SourceAsync(user.Id, "720p");
        using var created = await user.Client.PostAsJsonAsync("/v1/jobs", NewJob(sourceId));
        created.EnsureSuccessStatusCode();
        var job = (await created.Content.ReadFromJsonAsync<JobView>())!;
        var workerId = Guid.NewGuid();

        using var denied = await f.Anonymous.PostAsJsonAsync("/v1/worker/jobs/claim", new WorkerClaim(workerId));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var claimRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/worker/jobs/claim")
        { Content = JsonContent.Create(new WorkerClaim(workerId)) };
        claimRequest.Headers.Add("X-VideoGrabber-Worker-Token", "test-server-worker-token");
        using var claimed = await f.Anonymous.SendAsync(claimRequest);
        claimed.EnsureSuccessStatusCode();
        var lease = (await claimed.Content.ReadFromJsonAsync<AttemptLease>())!;
        Assert.Equal(job.JobId, lease.JobId);

        using var cancel = await user.Client.PostAsync($"/v1/jobs/{job.JobId:D}/cancel", null);
        cancel.EnsureSuccessStatusCode();
        var completion = Completion(lease, "success");
        using var completeRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/worker/jobs/complete")
        { Content = JsonContent.Create(completion) };
        completeRequest.Headers.Add("X-VideoGrabber-Worker-Token", "test-server-worker-token");
        using var completed = await f.Anonymous.SendAsync(completeRequest);
        completed.EnsureSuccessStatusCode();
        var final = (await completed.Content.ReadFromJsonAsync<JobView>())!;
        Assert.Equal("completed", final.State);
        Assert.Equal(1L, await CountArtifactsAsync(f, job.JobId));
        Assert.Equal(1L, await CountOutboxAsync(f, job.JobId, "job_completed"));
        Assert.Equal((0L, 0L), await CreditBucketsAsync(f, user.Id));
    }
    private static CreateJob NewJob(string sourceId)
    {
        var request = new CreateJob(Guid.NewGuid(), string.Empty, "download", "server_worker",
            null, sourceId, "720p", [], null, null);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }

    private static AttemptCompletion Completion(AttemptLease lease, string outcome)
    {
        var artifact = new ArtifactReceipt(Guid.NewGuid(), new string('a', 64), 1234,
            "video/mp4", "verify-" + lease.AttemptId.ToString("N"));
        return new AttemptCompletion(lease.JobId, lease.AttemptId, lease.Fence,
            outcome, artifact, artifact.VerificationEvidenceId);
    }

    private static async Task<long> CountJobsAsync(ApiFixture f, Guid accountId)
        => await ScalarAsync(f, "select count(*) from licensing.jobs where account_id=@id", accountId);
    private static async Task<long> CountReservationsAsync(ApiFixture f, Guid accountId)
        => await ScalarAsync(f, "select count(*) from licensing.reservations where account_id=@id", accountId);
    private static async Task<long> CountArtifactsAsync(ApiFixture f, Guid jobId)
        => await ScalarAsync(f, "select count(*) from licensing.artifacts where job_id=@id", jobId);
    private static async Task<long> CountOutboxAsync(ApiFixture f, Guid jobId, string kind)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new NpgsqlCommand(
            "select count(*) from licensing.job_outbox where job_id=@id and event_type=@kind", c);
        q.Parameters.AddWithValue("id", jobId); q.Parameters.AddWithValue("kind", kind);
        return (long)(await q.ExecuteScalarAsync())!;
    }
    private static async Task<long> ScalarAsync(ApiFixture f, string sql, Guid id)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new NpgsqlCommand(sql, c); q.Parameters.AddWithValue("id", id);
        return (long)(await q.ExecuteScalarAsync())!;
    }
    private static async Task<(long Available,long Reserved)> CreditBucketsAsync(ApiFixture f, Guid accountId)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new NpgsqlCommand(
            "select coalesce(sum(available),0),coalesce(sum(reserved),0) from licensing.entitlement_grants where account_id=@id", c);
        q.Parameters.AddWithValue("id", accountId);
        await using var r = await q.ExecuteReaderAsync(); await r.ReadAsync();
        return (r.GetInt64(0), r.GetInt64(1));
    }
}