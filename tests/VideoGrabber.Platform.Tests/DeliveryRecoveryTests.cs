using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Npgsql;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DeliveryRecoveryTests
{
    [Fact]
    public void Unknown_delivery_requires_explicit_retry()
    {
        Assert.False(ArtifactDeliveryService.MayAutomaticallyRetry("delivery_unknown"));
        Assert.True(ArtifactDeliveryService.MayAutomaticallyRetry("failed_before_send"));
        Assert.False(ArtifactDeliveryService.MayAutomaticallyRetry("delivered"));
    }

    [Fact]
    public async Task Lost_ack_does_not_automatically_send_twice_and_manual_retry_is_explicit()
    {
        await using var f = await ApiFixture.StartAsync();
        const long userId = 9101;
        var account = await f.AccountAsync("telegram", userId.ToString());
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 1, null, "delivery", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var fixture = await CompletedArtifactAsync(f, account.Id);
        var destination = await LinkPrivateDestinationAsync(f, account.Client, userId);
        var request = new DeliveryRequest(
            fixture.Job.JobId, fixture.Artifact.ArtifactId,
            destination.DestinationId, Guid.NewGuid());

        f.TelegramApi.Clear();
        f.TelegramApi.LoseNextDocumentAck = true;
        using var first = await account.Client.PostAsJsonAsync("/v1/deliveries", request);
        first.EnsureSuccessStatusCode();
        var unknown = (await first.Content.ReadFromJsonAsync<DeliveryView>())!;
        Assert.Equal("delivery_unknown", unknown.State);
        Assert.Equal(1, SendDocumentCount(f));
        Assert.Equal(1L, await CommitCountAsync(f, account.Id));

        using var replay = await account.Client.PostAsJsonAsync("/v1/deliveries", request);
        replay.EnsureSuccessStatusCode();
        var same = (await replay.Content.ReadFromJsonAsync<DeliveryView>())!;
        Assert.Equal(unknown.DeliveryId, same.DeliveryId);
        Assert.Equal("delivery_unknown", same.State);
        Assert.Equal(1, SendDocumentCount(f));
        Assert.Equal(1L, await CommitCountAsync(f, account.Id));

        using var missingWarning = await account.Client.PostAsJsonAsync(
            $"/v1/deliveries/{unknown.DeliveryId:D}/retry-unknown",
            new DeliveryRetry("retry please"));
        Assert.Equal(HttpStatusCode.BadRequest, missingWarning.StatusCode);
        Assert.Equal(1, SendDocumentCount(f));

        using var retry = await account.Client.PostAsJsonAsync(
            $"/v1/deliveries/{unknown.DeliveryId:D}/retry-unknown",
            new DeliveryRetry("I understand this may send a duplicate"));
        retry.EnsureSuccessStatusCode();
        var delivered = (await retry.Content.ReadFromJsonAsync<DeliveryView>())!;
        Assert.Equal("delivered", delivered.State);
        Assert.Equal(77L, delivered.MessageId);
        Assert.Equal("file-77", delivered.TelegramFileId);
        Assert.Equal(2, SendDocumentCount(f));
        Assert.Equal(1L, await CommitCountAsync(f, account.Id));

        Cleanup(fixture.Path);
    }

    [Fact]
    public async Task Permission_loss_before_send_is_safe_to_retry_automatically()
    {
        await using var f = await ApiFixture.StartAsync();
        const long userId = 9102;
        var account = await f.AccountAsync("telegram", userId.ToString());
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 1, null, "delivery", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var fixture = await CompletedArtifactAsync(f, account.Id);
        var destination = await LinkPrivateDestinationAsync(f, account.Client, userId);
        var request = new DeliveryRequest(
            fixture.Job.JobId, fixture.Artifact.ArtifactId,
            destination.DestinationId, Guid.NewGuid());

        f.TelegramApi.SetRights(
            userId, userId, userCanPublish: true, botCanPublish: false, kind: "private");
        f.TelegramApi.Clear();
        using var denied = await account.Client.PostAsJsonAsync("/v1/deliveries", request);
        denied.EnsureSuccessStatusCode();
        var failed = (await denied.Content.ReadFromJsonAsync<DeliveryView>())!;
        Assert.Equal("failed_before_send", failed.State);
        Assert.Equal(0, SendDocumentCount(f));

        f.TelegramApi.SetRights(
            userId, userId, userCanPublish: true, botCanPublish: true, kind: "private");
        using var retry = await account.Client.PostAsJsonAsync("/v1/deliveries", request);
        retry.EnsureSuccessStatusCode();
        var delivered = (await retry.Content.ReadFromJsonAsync<DeliveryView>())!;
        Assert.Equal(failed.DeliveryId, delivered.DeliveryId);
        Assert.Equal("delivered", delivered.State);
        Assert.Equal(1, SendDocumentCount(f));
        Assert.Equal(1L, await CommitCountAsync(f, account.Id));

        Cleanup(fixture.Path);
    }

    [Fact]
    public async Task Foreign_account_cannot_read_or_retry_delivery()
    {
        await using var f = await ApiFixture.StartAsync();
        const long userId = 9103;
        var owner = await f.AccountAsync("telegram", userId.ToString());
        var foreign = await f.AccountAsync("telegram", "9104");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(owner.Id, "credits", 0, 1, null, "delivery", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var fixture = await CompletedArtifactAsync(f, owner.Id);
        var destination = await LinkPrivateDestinationAsync(f, owner.Client, userId);
        f.TelegramApi.LoseNextDocumentAck = true;
        using var created = await owner.Client.PostAsJsonAsync(
            "/v1/deliveries",
            new DeliveryRequest(
                fixture.Job.JobId, fixture.Artifact.ArtifactId,
                destination.DestinationId, Guid.NewGuid()));
        var delivery = (await created.Content.ReadFromJsonAsync<DeliveryView>())!;

        using var read = await foreign.Client.GetAsync(
            $"/v1/deliveries/{delivery.DeliveryId:D}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var retry = await foreign.Client.PostAsJsonAsync(
            $"/v1/deliveries/{delivery.DeliveryId:D}/retry-unknown",
            new DeliveryRetry("I understand this may send a duplicate"));
        Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);

        Cleanup(fixture.Path);
    }

    [Fact]
    public async Task Altered_body_with_same_idempotency_key_conflicts()
    {
        await using var f = await ApiFixture.StartAsync();
        const long userId = 9105;
        var account = await f.AccountAsync("telegram", userId.ToString());
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 1, null, "delivery", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var fixture = await CompletedArtifactAsync(f, account.Id);
        var destination = await LinkPrivateDestinationAsync(f, account.Client, userId);
        var key = Guid.NewGuid();
        var firstRequest = new DeliveryRequest(
            fixture.Job.JobId, fixture.Artifact.ArtifactId,
            destination.DestinationId, key);
        using var first = await account.Client.PostAsJsonAsync("/v1/deliveries", firstRequest);
        first.EnsureSuccessStatusCode();

        using var altered = await account.Client.PostAsJsonAsync(
            "/v1/deliveries",
            firstRequest with { ArtifactId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, altered.StatusCode);

        Cleanup(fixture.Path);
    }

    private static int SendDocumentCount(ApiFixture f)
        => f.TelegramApi.Requests.Count(
            x => x.Path.EndsWith("/sendDocument", StringComparison.Ordinal));

    private static async Task<DeliveryDestination> LinkPrivateDestinationAsync(
        ApiFixture f,
        HttpClient client,
        long userId)
    {
        f.TelegramApi.SetRights(
            userId, userId, userCanPublish: true, botCanPublish: true, kind: "private");
        using var challengeResponse = await client.PostAsJsonAsync(
            "/v1/destinations/challenges",
            new DestinationChallengeRequest(userId));
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content
            .ReadFromJsonAsync<DestinationChallenge>())!;
        using var link = await client.PostAsJsonAsync(
            "/v1/destinations",
            new DestinationRequest(userId, "Private delivery", challenge.Proof));
        link.EnsureSuccessStatusCode();
        return (await link.Content.ReadFromJsonAsync<DeliveryDestination>())!;
    }

    private static async Task<CompletedFixture> CompletedArtifactAsync(
        ApiFixture f,
        Guid accountId)
    {
        var sourceId = await f.SourceAsync(accountId, "720p");
        var request = new CreateJob(
            Guid.NewGuid(), string.Empty, "download", "server_worker",
            null, sourceId, "720p", [], null, null);
        request = request with { RequestHash = JobRequestHasher.Hash(request) };
        var jobs = f.Service<JobStore>();
        var job = await jobs.CreateAsync(accountId, request, CancellationToken.None);
        var lease = (await jobs.ClaimAsync(
            Guid.NewGuid(), null, null, CancellationToken.None))!;

        var path = Path.Combine(
            Path.GetTempPath(),
            "vg-delivery-" + Guid.NewGuid().ToString("N") + ".bin");
        var bytes = RandomNumberGenerator.GetBytes(64 * 1024);
        await File.WriteAllBytesAsync(path, bytes);
        var artifact = new ArtifactReceipt(
            Guid.NewGuid(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            bytes.LongLength,
            "application/octet-stream",
            "delivery-verified",
            path);
        var completed = await jobs.CompleteAsync(
            new AttemptCompletion(
                lease.JobId, lease.AttemptId, lease.Fence,
                "success", artifact, artifact.VerificationEvidenceId),
            CancellationToken.None);
        Assert.Equal("completed", completed.State);
        return new CompletedFixture(completed, artifact, path);
    }

    private static async Task<long> CommitCountAsync(ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.credit_ledger where account_id=@id and event_kind='commit'",
            connection);
        command.Parameters.AddWithValue("id", accountId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static void Cleanup(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed record CompletedFixture(
        JobView Job,
        ArtifactReceipt Artifact,
        string Path);
}
