using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class LedgerConcurrencyTests
{
    [Theory]
    [InlineData(20)]
    [InlineData(100)]
    public async Task Last_credit_is_reserved_once_across_concurrent_requests(int count)
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "shared-last-credit-" + count);
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "race", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var tasks = Enumerable.Range(0, count).Select(i => user.Client.PostAsJsonAsync(
            "/v1/reservations", new ReservationRequest(Guid.NewGuid(), i.ToString("D64"),
                "download", "server_worker", null)));
        var responses = await Task.WhenAll(tasks);
        Assert.Single(responses, r => r.IsSuccessStatusCode);
        Assert.All(responses.Where(r => !r.IsSuccessStatusCode),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Same_intent_replays_receipt_and_changed_payload_conflicts()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "intent-replay");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 2, null, "replay", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var request = new ReservationRequest(Guid.NewGuid(), new string('a', 64),
            "download", "server_worker", null);
        var first = await user.Client.PostAsJsonAsync("/v1/reservations", request);
        var replay = await user.Client.PostAsJsonAsync("/v1/reservations", request);
        first.EnsureSuccessStatusCode(); replay.EnsureSuccessStatusCode();
        Assert.Equal(await first.Content.ReadFromJsonAsync<ReservationReceipt>(),
            await replay.Content.ReadFromJsonAsync<ReservationReceipt>());
        var changed = await user.Client.PostAsJsonAsync("/v1/reservations",
            request with { RequestHash = new string('b', 64) });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(1, access!.RemainingDownloads);
    }
}
