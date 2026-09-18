using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class ApiCompatibilityTests
{
    [Theory]
    [InlineData(PlatformProtocol.MinimumSupported)]
    [InlineData(PlatformProtocol.Current)]
    public async Task Current_and_previous_protocols_are_accepted(int protocol)
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "compat-" + protocol);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/v1/access");
        request.Headers.Add(
            "X-VideoGrabber-Protocol",
            protocol.ToString());

        using var response = await account.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            PlatformProtocol.Current.ToString(),
            response.Headers.GetValues(
                "X-VideoGrabber-Protocol-Current").Single());
        Assert.Equal(
            PlatformProtocol.MinimumSupported.ToString(),
            response.Headers.GetValues(
                "X-VideoGrabber-Protocol-Minimum").Single());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("future")]
    public async Task Unsupported_client_gets_upgrade_response(
        string protocol)
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "google", "old-client-" + Guid.NewGuid().ToString("N"));

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/v1/access");
        request.Headers.Add(
            "X-VideoGrabber-Protocol",
            protocol);

        using var response = await account.Client.SendAsync(request);

        Assert.Equal(
            (HttpStatusCode)426,
            response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "client_upgrade_required",
            body,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"currentProtocol\":2",
            body,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"minimumProtocol\":1",
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_api_version_one_remains_accepted()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "google", "legacy-api-one");
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/v1/access");
        request.Headers.Add(
            "X-VideoGrabber-Api-Version",
            PlatformProtocol.LegacyApiVersion.ToString());

        using var response = await account.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_additive_json_field_is_ignored_predictably()
    {
        await using var f = await ApiFixture.StartAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/v1/auth/start")
        {
            Content = new StringContent(
                """
                {
                  "provider":"google",
                  "returnUri":"https://client.example.test/auth/complete",
                  "clientChallenge":"client-challenge",
                  "futureOptionalField":{"version":99}
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(
            "X-VideoGrabber-Protocol",
            PlatformProtocol.MinimumSupported.ToString());

        using var response = await f.Anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_worker_cannot_claim_new_operation()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "google", "legacy-worker-account");
        using var admin = await f.AdminAsync();
        using (var grant = await admin.PostAsJsonAsync(
                   "/v1/admin/grants",
                   new GrantRequest(
                       account.Id,
                       "credits",
                       0,
                       5,
                       null,
                       "compatibility worker credits",
                       Guid.NewGuid())))
        {
            grant.EnsureSuccessStatusCode();
        }

        var source = await f.SourceAsync(
            account.Id, "720p");
        var mp3 = NewJob(
            "mp3", source);
        var download = NewJob(
            "download", source);
        using (var response = await account.Client.PostAsJsonAsync(
                   "/v1/jobs", mp3))
            response.EnsureSuccessStatusCode();
        using (var response = await account.Client.PostAsJsonAsync(
                   "/v1/jobs", download))
            response.EnsureSuccessStatusCode();

        using var claim = new HttpRequestMessage(
            HttpMethod.Post,
            "/v1/worker/jobs/claim")
        {
            Content = JsonContent.Create(
                new WorkerClaim(
                    Guid.NewGuid(),
                    PlatformProtocol.MinimumSupported,
                    SupportedOperations: null))
        };
        claim.Headers.Add(
            "X-VideoGrabber-Worker-Token",
            "test-server-worker-token");
        claim.Headers.Add(
            "X-VideoGrabber-Protocol",
            PlatformProtocol.MinimumSupported.ToString());

        using var claimedResponse =
            await f.Anonymous.SendAsync(claim);
        claimedResponse.EnsureSuccessStatusCode();
        var lease = await claimedResponse.Content
            .ReadFromJsonAsync<AttemptLease>();

        Assert.NotNull(lease);
        Assert.Equal("download", lease!.Work.Kind);

        var jobs = await account.Client
            .GetFromJsonAsync<JobView[]>("/v1/jobs");
        Assert.Contains(
            jobs!,
            x => x.IntentId == mp3.IntentId
                 && x.State == "queued");
    }

    [Fact]
    public async Task Unsupported_worker_protocol_gets_426_before_claim()
    {
        await using var f = await ApiFixture.StartAsync();
        using var claim = new HttpRequestMessage(
            HttpMethod.Post,
            "/v1/worker/jobs/claim")
        {
            Content = JsonContent.Create(
                new WorkerClaim(
                    Guid.NewGuid(),
                    ProtocolVersion: 0,
                    SupportedOperations: ["download"]))
        };
        claim.Headers.Add(
            "X-VideoGrabber-Worker-Token",
            "test-server-worker-token");

        using var response =
            await f.Anonymous.SendAsync(claim);

        Assert.Equal(
            HttpStatusCode.UpgradeRequired,
            response.StatusCode);
    }

    private static CreateJob NewJob(
        string kind,
        string sourceId)
    {
        var request = new CreateJob(
            Guid.NewGuid(),
            string.Empty,
            kind,
            "server_worker",
            null,
            sourceId,
            "720p",
            [],
            null,
            null);
        return request with
        {
            RequestHash =
                JobRequestHasher.Hash(request)
        };
    }
}
