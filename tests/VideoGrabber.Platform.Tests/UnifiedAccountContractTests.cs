using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class UnifiedAccountContractTests
{
    [Theory]
    [InlineData("email")]
    [InlineData("google")]
    public async Task Explicit_telegram_link_and_windows_handoff_share_profile_balance_and_jobs(string provider)
    {
        await using var fixture = await ApiFixture.StartAsync();
        fixture.Service<IConfiguration>()["VG_MEDIA_STORAGE_MODE"] = "client_only";
        const long telegramUserId = 924681357;
        const string primarySubject = "unified-contract-owner";
        var website = await fixture.AccountAsync(provider, primarySubject, includeStarter: true);
        var temporary = await fixture.AccountAsync("telegram",
            telegramUserId.ToString(CultureInfo.InvariantCulture), includeStarter: true, telegramOnly: true);
        Assert.NotEqual(website.Id, temporary.Id);

        var ticket = await fixture.Service<TelegramAccountLinkService>().BeginAsync(
            temporary.Id, telegramUserId, CancellationToken.None);
        var linkRequest = new CompleteTelegramAccountLinkRequest(Query(ticket.LinkUri, "telegram_link"));
        using var linked = await website.Client.PostAsJsonAsync("/v1/telegram/account-link/complete", linkRequest);
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);
        using var linkReplay = await website.Client.PostAsJsonAsync("/v1/telegram/account-link/complete", linkRequest);
        Assert.Equal(HttpStatusCode.NotFound, linkReplay.StatusCode);

        // A fresh Telegram assertion must resolve the moved identity, rather than the temporary account.
        using var telegram = fixture.TelegramWebClient();
        var initData = TelegramAuthTests.SignInitData(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = fixture.Clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["query_id"] = "AA-unified-" + Guid.NewGuid().ToString("N"),
            ["user"] = JsonSerializer.Serialize(new { id = telegramUserId, first_name = "Synthetic" })
        }, TelegramAuthTests.BotToken);
        using var telegramResponse = await telegram.PostAsync("/v1/telegram/session",
            new StringContent(initData, Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, telegramResponse.StatusCode);
        var telegramSession = await ReadAsync<TelegramWebSession>(telegramResponse);
        telegram.DefaultRequestHeaders.Add("X-CSRF-Token", telegramSession.CsrfToken);
        using var assertionReplay = await telegram.PostAsync("/v1/telegram/session",
            new StringContent(initData, Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.Unauthorized, assertionReplay.StatusCode);

        // Re-entering the website exercises identity resolution again; no second starter grant is allowed.
        var freshWebsite = await fixture.AccountAsync(provider, primarySubject, includeStarter: true);
        Assert.Equal(website.Id, freshWebsite.Id);
        using var startResponse = await fixture.Anonymous.PostAsJsonAsync("/v1/auth/desktop/start",
            new DesktopSignInStartRequest(new Uri("http://127.0.0.1:54323/videograbber-auth/callback")));
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var started = await ReadAsync<DesktopSignInStart>(startResponse);
        using var approveResponse = await freshWebsite.Client.PostAsJsonAsync("/v1/auth/desktop/approve",
            new DesktopSignInApprovalRequest(started.FlowId, started.State));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await ReadAsync<DesktopSignInApproval>(approveResponse);
        var consumeRequest = new DesktopSignInConsumeRequest(started.FlowId, approved.Code, approved.State);
        using var consumeResponse = await fixture.Anonymous.PostAsJsonAsync("/v1/auth/desktop/consume", consumeRequest);
        Assert.Equal(HttpStatusCode.OK, consumeResponse.StatusCode);
        using var windows = fixture.SessionClient(await ReadAsync<ApiSession>(consumeResponse));
        using var handoffReplay = await fixture.Anonymous.PostAsJsonAsync("/v1/auth/desktop/consume", consumeRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, handoffReplay.StatusCode);

        var clients = new[] { website.Client, freshWebsite.Client, telegram, windows };
        foreach (var client in clients)
        {
            var profile = await GetAsync<AccountProfile>(client, "/v1/me");
            Assert.Equal(website.Id, profile.AccountId);
            Assert.Equal(provider, profile.PrimaryAuthProvider);
            Assert.False(profile.Blocked);
            Assert.Equal(new[] { provider, "telegram" }.OrderBy(value => value),
                profile.LinkedProviders.OrderBy(value => value));
            var access = await GetAsync<AccessSnapshot>(client, "/v1/access");
            Assert.Equal("free", access.PlanId);
            Assert.Equal(10, access.RemainingDownloads);
            Assert.True(access.CanDownload);
            Assert.False(access.CanEdit);
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var deviceResponse = await windows.PostAsJsonAsync("/v1/devices", new DeviceRegistration(
            "Synthetic Windows", "windows", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), Guid.NewGuid()));
        deviceResponse.EnsureSuccessStatusCode();
        var device = await ReadAsync<DeviceReceipt>(deviceResponse);
        using var sourceResponse = await freshWebsite.Client.PostAsJsonAsync("/v1/sources/register-desktop",
            new RegisterDesktopSourceRequest(new Uri("https://93.184.216.34/synthetic-lesson.mp4"), "720p"));
        sourceResponse.EnsureSuccessStatusCode();
        var source = await ReadAsync<AnalyzedMedia>(sourceResponse);

        var websiteRequest = JobRequest(source.SourceId, device.DeviceId);
        var websiteJob = await CreateJobAsync(freshWebsite.Client, websiteRequest);
        var telegramJob = await CreateJobAsync(telegram, JobRequest(source.SourceId, device.DeviceId));
        // The intent is account scoped: replaying a website request in Telegram must not charge again.
        var crossSurfaceReplay = await CreateJobAsync(telegram, websiteRequest);
        Assert.Equal(websiteJob.JobId, crossSurfaceReplay.JobId);
        Assert.NotEqual(websiteJob.JobId, telegramJob.JobId);
        var expectedJobs = new[] { websiteJob.JobId, telegramJob.JobId }.OrderBy(value => value).ToArray();
        Assert.Equal(10, (await GetAsync<AccessSnapshot>(windows, "/v1/access")).RemainingDownloads);

        // Desktop jobs reserve at claim, not at enqueue. Exercise the real device proof and
        // client acknowledgement contract without claiming that any media file was downloaded.
        using var challengeResponse = await windows.PostAsJsonAsync(
            $"/v1/devices/{device.DeviceId:D}/challenge", new { });
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await ReadAsync<DeviceChallenge>(challengeResponse);
        var signature = key.SignData(Encoding.UTF8.GetBytes(challenge.Nonce), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        using var claimResponse = await windows.PostAsJsonAsync(
            $"/v1/desktop-worker/devices/{device.DeviceId:D}/claim",
            new DeviceLeaseProof(challenge.Nonce, Convert.ToBase64String(signature)));
        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        var lease = await ReadAsync<AttemptLease>(claimResponse);
        Assert.Contains(lease.JobId, expectedJobs);
        Assert.Equal(device.DeviceId, lease.Work.DeviceId);
        var completion = new DesktopLocalCompletionRequest(lease, "success", "synthetic-local-contract-ack");
        using var completedResponse = await windows.PostAsJsonAsync(
            $"/v1/desktop-worker/devices/{device.DeviceId:D}/complete-local", completion);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        var completed = await ReadAsync<JobView>(completedResponse);
        Assert.Equal("completed", completed.State);
        Assert.Null(completed.ArtifactId);
        // The existing endpoint rejects reuse of a terminal attempt; it must not debit again.
        using var completionReplay = await windows.PostAsJsonAsync(
            $"/v1/desktop-worker/devices/{device.DeviceId:D}/complete-local", completion);
        Assert.Equal(HttpStatusCode.NotFound, completionReplay.StatusCode);
        var expectedHistory = await GetAsync<JobView[]>(freshWebsite.Client, "/v1/jobs");
        Assert.Single(expectedHistory, job => job.JobId == lease.JobId && job.State == "completed");
        var expectedAccess = await GetAsync<AccessSnapshot>(freshWebsite.Client, "/v1/access");
        Assert.Equal(9, expectedAccess.RemainingDownloads);
        foreach (var client in clients)
        {
            Assert.Equivalent(expectedAccess, await GetAsync<AccessSnapshot>(client, "/v1/access"));
            var history = await GetAsync<JobView[]>(client, "/v1/jobs");
            Assert.Equal(expectedJobs, history.Select(job => job.JobId).OrderBy(value => value));
            Assert.All(history, job =>
            {
                Assert.Equal(website.Id, job.AccountId);
                Assert.Equal("desktop_worker", job.Executor);
            });
            foreach (var job in expectedHistory)
                Assert.Equivalent(job, await GetAsync<JobView>(client, $"/v1/jobs/{job.JobId:D}"));
            var devices = await GetAsync<DeviceReceipt[]>(client, "/v1/devices");
            Assert.Contains(devices, item => item.DeviceId == device.DeviceId && !item.Revoked);
        }

        // An unrelated identity must not inherit the linked account's history or reserved credits.
        var outsider = await fixture.AccountAsync(provider, "unified-contract-outsider", includeStarter: true);
        Assert.NotEqual(website.Id, outsider.Id);
        Assert.Empty(await GetAsync<JobView[]>(outsider.Client, "/v1/jobs"));
        Assert.Equal(10, (await GetAsync<AccessSnapshot>(outsider.Client, "/v1/access")).RemainingDownloads);
        using var foreignJob = await outsider.Client.GetAsync($"/v1/jobs/{websiteJob.JobId:D}");
        Assert.Equal(HttpStatusCode.NotFound, foreignJob.StatusCode);
    }

    private static CreateJob JobRequest(string sourceId, Guid deviceId)
    {
        var request = new CreateJob(Guid.NewGuid(), string.Empty, "download", "desktop_worker",
            deviceId, sourceId, "720p", [], null, null);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }

    private static async Task<JobView> CreateJobAsync(HttpClient client, CreateJob request)
    {
        using var response = await client.PostAsJsonAsync("/v1/jobs", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<JobView>(response);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidDataException("Synthetic contract response was empty.");

    private static string Query(Uri uri, string name)
    {
        foreach (var value in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = value.Split('=', 2);
            if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]) == name)
                return Uri.UnescapeDataString(pair[1]);
        }
        throw new InvalidDataException("Synthetic link token was missing.");
    }
}
