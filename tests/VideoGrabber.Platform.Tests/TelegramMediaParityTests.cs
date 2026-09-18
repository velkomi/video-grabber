using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class TelegramMediaParityTests
{
    [Fact]
    public async Task Capabilities_publish_all_media_operations_with_real_executor_policy()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "parity-capabilities");
        var capabilities = await account.Client.GetFromJsonAsync<PlatformCapabilitiesDto>(
            "/v1/capabilities");
        Assert.NotNull(capabilities);
        var names = capabilities!.Operations.Select(x => x.Operation).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new HashSet<string>(["download","mp3","trim","join","transcribe"], StringComparer.Ordinal),
            names);
        Assert.Contains(capabilities.Operations,
            x => x.Operation == "download" && x.Executors.Contains("server_worker"));
        Assert.Contains(capabilities.Operations,
            x => x.Operation == "transcribe" && x.Executors.Contains("desktop_worker"));
    }

    [Theory]
    [InlineData("download")]
    [InlineData("mp3")]
    [InlineData("trim")]
    [InlineData("join")]
    [InlineData("transcribe")]
    public async Task Supported_media_operation_requires_owned_inputs_and_is_admitted(string operation)
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "parity-" + operation);
        await f.PromoteAdminAsync(account.Id);
        var sourceId = await f.SourceAsync(account.Id, "720p");
        var artifact1 = await SeedArtifactAsync(f, account.Id);
        var artifact2 = await SeedArtifactAsync(f, account.Id);

        Guid[] inputs = operation switch
        {
            "trim" or "transcribe" => [artifact1],
            "join" => [artifact1, artifact2],
            _ => []
        };
        var executor = operation == "transcribe" ? "desktop_worker" : "server_worker";
        Guid? deviceId = null;
        if (executor == "desktop_worker")
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var deviceResponse = await account.Client.PostAsJsonAsync(
                "/v1/devices",
                new DeviceRegistration(
                    "Parity PC", "windows",
                    Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    Guid.NewGuid()));
            deviceResponse.EnsureSuccessStatusCode();
            deviceId = (await deviceResponse.Content.ReadFromJsonAsync<DeviceReceipt>())!.DeviceId;
        }

        var request = NewRequest(
            operation, executor, deviceId, sourceId, inputs,
            operation == "trim" ? 0 : null,
            operation == "trim" ? 1000 : null);

        using var response = await account.Client.PostAsJsonAsync("/v1/jobs", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var job = (await response.Content.ReadFromJsonAsync<JobView>())!;
        Assert.Equal(
            executor == "desktop_worker" ? "waiting_for_worker" : "queued",
            job.State);
    }

    [Fact]
    public async Task Foreign_artifact_is_rejected_without_metadata_leak()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("telegram", "parity-a");
        var b = await f.AccountAsync("telegram", "parity-b");
        await f.PromoteAdminAsync(a.Id);
        var sourceId = await f.SourceAsync(a.Id, "720p");
        var foreign = await SeedArtifactAsync(f, b.Id);
        var request = NewRequest(
            "trim", "server_worker", null, sourceId, [foreign], 0, 1000);

        using var response = await a.Client.PostAsJsonAsync("/v1/jobs", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(foreign.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Queue_reorder_is_versioned_and_rejects_stale_version()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "parity-queue");
        await f.PromoteAdminAsync(account.Id);
        var sourceId = await f.SourceAsync(account.Id, "720p");
        var one = await CreateJobAsync(account.Client,
            NewRequest("download","server_worker",null,sourceId,[],null,null));
        var two = await CreateJobAsync(account.Client,
            NewRequest("mp3","server_worker",null,sourceId,[],null,null));

        using var first = await account.Client.PostAsJsonAsync(
            "/v1/queue/order",
            new QueueOrder([two.JobId, one.JobId], 1));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var order = (await first.Content.ReadFromJsonAsync<QueueOrderView>())!;
        Assert.Equal(2, order.Version);
        Assert.Equal([two.JobId, one.JobId], order.JobIds);

        using var stale = await account.Client.PostAsJsonAsync(
            "/v1/queue/order",
            new QueueOrder([one.JobId, two.JobId], 1));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Job_event_stream_is_account_scoped()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("telegram", "events-a");
        var b = await f.AccountAsync("telegram", "events-b");
        await f.PromoteAdminAsync(a.Id);
        await f.PromoteAdminAsync(b.Id);
        var sourceA = await f.SourceAsync(a.Id, "720p");
        var sourceB = await f.SourceAsync(b.Id, "720p");
        var jobA = await CreateJobAsync(a.Client,
            NewRequest("download","server_worker",null,sourceA,[],null,null));
        var jobB = await CreateJobAsync(b.Client,
            NewRequest("download","server_worker",null,sourceB,[],null,null));

        var streamA = await a.Client.GetStringAsync("/v1/jobs/events?snapshot=true");
        var streamB = await b.Client.GetStringAsync("/v1/jobs/events?snapshot=true");

        Assert.Contains(jobA.JobId.ToString("D"), streamA, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(jobB.JobId.ToString("D"), streamA, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(jobB.JobId.ToString("D"), streamB, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(jobA.JobId.ToString("D"), streamB, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bot_media_commands_are_private_and_expose_real_help()
    {
        await using var f = await ApiFixture.StartAsync();
        _ = await f.AccountAsync("telegram", "8801");
        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9801, new
        {
            update_id = 9801L,
            message = new
            {
                message_id = 1L,
                from = new { id = 8801L },
                chat = new { id = 8801L, type = "private" },
                text = "/media"
            }
        });
        Assert.True(await f.Service<VideoGrabber.Platform.Api.Telegram.TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));
        var request = Assert.Single(f.TelegramApi.Requests,
            x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        using var json = System.Text.Json.JsonDocument.Parse(request.Body);
        var text = json.RootElement.GetProperty("text").GetString() ?? "";
        Assert.Contains("/download", text);
        Assert.Contains("/join", text);

        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9802, new
        {
            update_id = 9802L,
            message = new
            {
                message_id = 2L,
                from = new { id = 8801L },
                chat = new { id = -1008801L, type = "group" },
                text = "/media"
            }
        });
        Assert.True(await f.Service<VideoGrabber.Platform.Api.Telegram.TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));
        var group = Assert.Single(f.TelegramApi.Requests,
            x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        using var groupJson = System.Text.Json.JsonDocument.Parse(group.Body);
        Assert.Contains("личн",
            groupJson.RootElement.GetProperty("text").GetString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task PostWebhookAsync(ApiFixture f, long updateId, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/telegram/webhook")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(
            "X-Telegram-Bot-Api-Secret-Token",
            TelegramWebhookTests.WebhookSecret);
        using var response = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static CreateJob NewRequest(
        string kind,
        string executor,
        Guid? deviceId,
        string sourceId,
        Guid[] inputs,
        long? start,
        long? duration)
    {
        var request = new CreateJob(
            Guid.NewGuid(), string.Empty, kind, executor, deviceId,
            sourceId, "720p", inputs, start, duration);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }

    private static async Task<JobView> CreateJobAsync(
        HttpClient client,
        CreateJob request)
    {
        using var response = await client.PostAsJsonAsync("/v1/jobs", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobView>())!;
    }

    private static async Task<Guid> SeedArtifactAsync(ApiFixture f, Guid accountId)
    {
        var jobId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var now = f.Clock.GetUtcNow();
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var job = new NpgsqlCommand("""
            insert into licensing.jobs(
              job_id,account_id,intent_id,request_hash,kind,executor,
              source_id,quality,input_artifact_ids,state,fence,reason,created_at,updated_at)
            values(@job,@account,@intent,@hash,'download','server_worker',
              null,'720p',array[]::uuid[],'completed',1,'fixture',@now,@now)
            """, connection, transaction))
        {
            job.Parameters.AddWithValue("job", jobId);
            job.Parameters.AddWithValue("account", accountId);
            job.Parameters.AddWithValue("intent", Guid.NewGuid());
            job.Parameters.AddWithValue("hash", new string('a',64));
            job.Parameters.AddWithValue("now", now);
            await job.ExecuteNonQueryAsync();
        }
        await using (var attempt = new NpgsqlCommand("""
            insert into licensing.job_attempts(
              attempt_id,job_id,worker_id,fence,capability_hash,lease_until,
              runtime_deadline,state,started_at,heartbeat_at,completed_at,outcome,evidence_id)
            values(@attempt,@job,@worker,1,@cap,@until,@until,'completed',
              @now,@now,@now,'success','fixture')
            """, connection, transaction))
        {
            attempt.Parameters.AddWithValue("attempt", attemptId);
            attempt.Parameters.AddWithValue("job", jobId);
            attempt.Parameters.AddWithValue("worker", Guid.NewGuid());
            attempt.Parameters.AddWithValue("cap", new string('b',64));
            attempt.Parameters.AddWithValue("until", now.AddHours(1));
            attempt.Parameters.AddWithValue("now", now);
            await attempt.ExecuteNonQueryAsync();
        }
        await using (var artifact = new NpgsqlCommand("""
            insert into licensing.artifacts(
              artifact_id,account_id,job_id,attempt_id,fence,sha256,bytes,
              media_type,verification_evidence_id,created_at)
            values(@artifact,@account,@job,@attempt,1,@sha,1000,'video/mp4','fixture',@now)
            """, connection, transaction))
        {
            artifact.Parameters.AddWithValue("artifact", artifactId);
            artifact.Parameters.AddWithValue("account", accountId);
            artifact.Parameters.AddWithValue("job", jobId);
            artifact.Parameters.AddWithValue("attempt", attemptId);
            artifact.Parameters.AddWithValue("sha", new string('c',64));
            artifact.Parameters.AddWithValue("now", now);
            await artifact.ExecuteNonQueryAsync();
        }
        await using (var update = new NpgsqlCommand(
            "update licensing.jobs set artifact_id=@artifact where job_id=@job",
            connection, transaction))
        {
            update.Parameters.AddWithValue("artifact", artifactId);
            update.Parameters.AddWithValue("job", jobId);
            await update.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        return artifactId;
    }

    private sealed record PlatformCapabilitiesDto(
        bool MediaAvailable,
        bool DesktopWorkerAvailable,
        string Reason,
        MediaCapability[] Operations);
}
