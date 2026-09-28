using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class PrimaryAccountGateTests
{
    [Fact]
    public async Task Telegram_origin_account_cannot_create_protected_download_job()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "telegram",
            "telegram-only-download",
            includeStarter: true,
            telegramOnly: true);
        var source = await f.SourceAsync(account.Id, "best");

        var request = CreateServerDownload(source);
        var response = await account.Client.PostAsJsonAsync("/v1/jobs", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("primary_account_link_required", body!["code"]);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("email")]
    public async Task Primary_google_or_email_account_can_create_protected_download_job(
        string provider)
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            provider,
            "primary-download-" + provider,
            includeStarter: true);
        var source = await f.SourceAsync(account.Id, "best");

        var response = await account.Client.PostAsJsonAsync(
            "/v1/jobs", CreateServerDownload(source));

        response.EnsureSuccessStatusCode();
        var job = await response.Content.ReadFromJsonAsync<JobView>();
        Assert.NotNull(job);
        Assert.Equal("download", job!.Kind);
        Assert.Equal("queued", job.State);
    }

    private static CreateJob CreateServerDownload(string sourceId)
    {
        var request = new CreateJob(
            Guid.NewGuid(),
            string.Empty,
            "download",
            "server_worker",
            null,
            sourceId,
            "best",
            [],
            null,
            null);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }
}
