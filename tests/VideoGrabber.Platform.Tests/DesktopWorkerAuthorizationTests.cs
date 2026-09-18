using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DesktopWorkerAuthorizationTests
{
    [Fact]
    public async Task Desktop_job_waits_without_credit_until_registered_device_claims()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "desktop-wait");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 1, null, "desktop", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await RegisterAsync(account.Client, key, "PC-WAIT");
        var sourceId = await f.SourceAsync(account.Id, "720p");
        var request = NewDesktopJob(sourceId, device.DeviceId);

        using var created = await account.Client.PostAsJsonAsync("/v1/jobs", request);
        created.EnsureSuccessStatusCode();
        var job = (await created.Content.ReadFromJsonAsync<JobView>())!;
        Assert.Equal("waiting_for_worker", job.State);
        Assert.Equal((1L, 0L), await CreditBucketsAsync(f, account.Id));

        var lease = await ClaimAsync(account.Client, device.DeviceId, key);
        Assert.NotNull(lease);
        Assert.Equal(device.DeviceId, lease!.Work.DeviceId);
        Assert.Equal((0L, 1L), await CreditBucketsAsync(f, account.Id));
    }

    [Fact]
    public async Task Foreign_account_and_device_cannot_use_upload_ticket()
    {
        await using var f = await ApiFixture.StartAsync();
        var owner = await f.AccountAsync("telegram", "desktop-owner");
        var foreign = await f.AccountAsync("telegram", "desktop-foreign");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(owner.Id, "credits", 0, 1, null, "desktop", Guid.NewGuid())))
            .EnsureSuccessStatusCode();

        using var ownerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var foreignKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ownerDevice = await RegisterAsync(owner.Client, ownerKey, "OWNER-PC");
        var foreignDevice = await RegisterAsync(foreign.Client, foreignKey, "FOREIGN-PC");
        var sourceId = await f.SourceAsync(owner.Id, "720p");
        var request = NewDesktopJob(sourceId, ownerDevice.DeviceId);
        (await owner.Client.PostAsJsonAsync("/v1/jobs", request)).EnsureSuccessStatusCode();
        var lease = (await ClaimAsync(owner.Client, ownerDevice.DeviceId, ownerKey))!;

        var ticketRequest = new UploadTicketRequest(
            lease, 4, new string('a', 64), "video/mp4");
        using var ticketResponse = await owner.Client.PostAsJsonAsync(
            $"/v1/desktop-worker/devices/{ownerDevice.DeviceId:D}/uploads", ticketRequest);
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = (await ticketResponse.Content.ReadFromJsonAsync<UploadTicket>())!;

        using var content = new ByteArrayContent([1,2,3,4]);
        content.Headers.ContentLength = 4;
        using var foreignUpload = await foreign.Client.PutAsync(
            $"/v1/desktop-worker/devices/{foreignDevice.DeviceId:D}/uploads/{ticket.UploadId:D}",
            content);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpload.StatusCode);
    }

    [Fact]
    public async Task Old_attempt_capability_is_rejected_after_lease_refence()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "desktop-stale");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 1, null, "desktop", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await RegisterAsync(account.Client, key, "STALE-PC");
        var sourceId = await f.SourceAsync(account.Id, "720p");
        (await account.Client.PostAsJsonAsync(
            "/v1/jobs", NewDesktopJob(sourceId, device.DeviceId))).EnsureSuccessStatusCode();
        var stale = (await ClaimAsync(account.Client, device.DeviceId, key))!;

        f.Clock.Advance(TimeSpan.FromSeconds(61));
        var fresh = (await ClaimAsync(account.Client, device.DeviceId, key))!;
        Assert.True(fresh.Fence > stale.Fence);

        using var response = await account.Client.PostAsJsonAsync(
            $"/v1/desktop-worker/devices/{device.DeviceId:D}/uploads",
            new UploadTicketRequest(stale, 4, new string('a',64), "video/mp4"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Verified_upload_commits_one_shared_reservation()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "desktop-upload");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 1, null, "desktop", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await RegisterAsync(account.Client, key, "UPLOAD-PC");
        var sourceId = await f.SourceAsync(account.Id, "180p");
        (await account.Client.PostAsJsonAsync(
            "/v1/jobs", NewDesktopJob(sourceId, device.DeviceId, "180p")))
            .EnsureSuccessStatusCode();
        var lease = (await ClaimAsync(account.Client, device.DeviceId, key))!;

        var file = await MakeVideoAsync();
        try
        {
            var bytes = await File.ReadAllBytesAsync(file);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            using var ticketResponse = await account.Client.PostAsJsonAsync(
                $"/v1/desktop-worker/devices/{device.DeviceId:D}/uploads",
                new UploadTicketRequest(lease, bytes.LongLength, sha, "video/mp4"));
            ticketResponse.EnsureSuccessStatusCode();
            var ticket = (await ticketResponse.Content.ReadFromJsonAsync<UploadTicket>())!;

            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentLength = bytes.LongLength;
            using var upload = await account.Client.PutAsync(
                $"/v1/desktop-worker/devices/{device.DeviceId:D}/uploads/{ticket.UploadId:D}",
                content);
            Assert.True(upload.IsSuccessStatusCode, await upload.Content.ReadAsStringAsync());
            var artifact = (await upload.Content.ReadFromJsonAsync<ArtifactReceipt>())!;
            Assert.Equal(sha, artifact.Sha256);

            using var complete = await account.Client.PostAsJsonAsync(
                $"/v1/desktop-worker/devices/{device.DeviceId:D}/complete",
                new DesktopCompletionRequest(lease, artifact));
            complete.EnsureSuccessStatusCode();
            var job = (await complete.Content.ReadFromJsonAsync<JobView>())!;
            Assert.Equal("completed", job.State);
            Assert.Equal((0L, 0L), await CreditBucketsAsync(f, account.Id));
            Assert.Equal(1L, await LedgerCommitCountAsync(f, account.Id));
        }
        finally
        {
            try { File.Delete(file); } catch { }
        }
    }

    private static CreateJob NewDesktopJob(
        string sourceId, Guid deviceId, string quality = "720p")
    {
        var request = new CreateJob(
            Guid.NewGuid(), string.Empty, "download", "desktop_worker",
            deviceId, sourceId, quality, [], null, null);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }

    private static async Task<DeviceReceipt> RegisterAsync(
        HttpClient client, ECDsa key, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/devices",
            new DeviceRegistration(
                name, "windows",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceReceipt>())!;
    }

    private static async Task<AttemptLease?> ClaimAsync(
        HttpClient client, Guid deviceId, ECDsa key)
    {
        using var challengeResponse = await client.PostAsync(
            $"/v1/devices/{deviceId:D}/challenge", null);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<DeviceChallenge>())!;
        var signature = key.SignData(
            Encoding.UTF8.GetBytes(challenge.Nonce),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        using var response = await client.PostAsJsonAsync(
            $"/v1/desktop-worker/devices/{deviceId:D}/claim",
            new DeviceLeaseProof(challenge.Nonce, Convert.ToBase64String(signature)));
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AttemptLease>();
    }

    private static async Task<(long Available,long Reserved)> CreditBucketsAsync(
        ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select coalesce(sum(available),0),coalesce(sum(reserved),0) from licensing.entitlement_grants where account_id=@id",
            connection);
        command.Parameters.AddWithValue("id", accountId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task<long> LedgerCommitCountAsync(ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.credit_ledger where account_id=@id and event_kind='commit'",
            connection);
        command.Parameters.AddWithValue("id", accountId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> MakeVideoAsync()
    {
        var path = Path.Combine(
            Path.GetTempPath(), "vg-desktop-upload-" + Guid.NewGuid().ToString("N") + ".mp4");
        var ffmpeg = Environment.GetEnvironmentVariable("VG_WORKER_FFMPEG") ?? "ffmpeg";
        var start = new System.Diagnostics.ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=25",
            "-t","1","-c:v","libx264","-threads","1","-pix_fmt","yuv420p","-y",path
        }) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)!;
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        Assert.True(File.Exists(path), error);
        return path;
    }
}
