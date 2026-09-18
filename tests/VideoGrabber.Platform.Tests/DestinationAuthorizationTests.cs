using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DestinationAuthorizationTests
{
    private const long ChannelId = -1001234567890L;

    [Fact]
    public async Task An_account_cannot_revoke_another_accounts_destination()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("telegram", "7001");
        var b = await f.AccountAsync("telegram", "7002");
        f.TelegramApi.SetRights(ChannelId, 7001, userCanPublish: true, botCanPublish: true, kind: "channel");

        var challengeResponse = await a.Client.PostAsJsonAsync("/v1/destinations/challenges",
            new DestinationChallengeRequest(ChannelId));
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<DestinationChallenge>())!;
        var linked = await a.Client.PostAsJsonAsync("/v1/destinations",
            new DestinationRequest(ChannelId, "Synthetic private channel", challenge.Proof));
        linked.EnsureSuccessStatusCode();
        var destination = (await linked.Content.ReadFromJsonAsync<DeliveryDestination>())!;

        var response = await b.Client.PostAsJsonAsync(
            $"/v1/destinations/{destination.DestinationId:D}/revoke", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Arbitrary_chat_without_verified_rights_cannot_be_linked()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "7101");
        const long arbitrary = -1009876543210L;
        var challengeResponse = await account.Client.PostAsJsonAsync("/v1/destinations/challenges",
            new DestinationChallengeRequest(arbitrary));
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<DestinationChallenge>())!;

        var linked = await account.Client.PostAsJsonAsync("/v1/destinations",
            new DestinationRequest(arbitrary, "Not mine", challenge.Proof));

        Assert.Equal(HttpStatusCode.Forbidden, linked.StatusCode);
    }

    [Fact]
    public async Task Rights_are_rechecked_before_each_send_and_loss_denies_delivery()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "7201");
        f.TelegramApi.SetRights(ChannelId, 7201, userCanPublish: true, botCanPublish: true, kind: "channel");
        var challengeResponse = await account.Client.PostAsJsonAsync("/v1/destinations/challenges",
            new DestinationChallengeRequest(ChannelId));
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<DestinationChallenge>())!;
        var linked = await account.Client.PostAsJsonAsync("/v1/destinations",
            new DestinationRequest(ChannelId, "Rights change", challenge.Proof));
        var destination = (await linked.Content.ReadFromJsonAsync<DeliveryDestination>())!;

        f.TelegramApi.SetRights(ChannelId, 7201, userCanPublish: true, botCanPublish: false, kind: "channel");
        var service = f.Service<DestinationService>();
        var denied = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AuthorizeSendAsync(account.Id, destination.DestinationId, CancellationToken.None));
        Assert.Equal("destination_permission_required", denied.Message);
    }

    [Fact]
    public async Task Large_64_bit_chat_id_roundtrips_without_truncation_and_proof_is_single_use()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "7301");
        const long largeChat = -1009223372036854000L;
        f.TelegramApi.SetRights(largeChat, 7301, userCanPublish: true, botCanPublish: true, kind: "supergroup");
        var challengeResponse = await account.Client.PostAsJsonAsync("/v1/destinations/challenges",
            new DestinationChallengeRequest(largeChat));
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<DestinationChallenge>())!;
        var request = new DestinationRequest(largeChat, "Large ID", challenge.Proof);
        var first = await account.Client.PostAsJsonAsync("/v1/destinations", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var destination = (await first.Content.ReadFromJsonAsync<DeliveryDestination>())!;
        Assert.Equal(largeChat, destination.ChatId);

        var replay = await account.Client.PostAsJsonAsync("/v1/destinations", request);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var rows = (await account.Client.GetFromJsonAsync<DeliveryDestination[]>("/v1/destinations"))!;
        Assert.Single(rows);
        Assert.Equal(largeChat, rows[0].ChatId);
    }

    [Fact]
    public async Task Private_destination_requires_started_bot_rights_for_same_verified_user()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "7401");
        f.TelegramApi.SetRights(7401, 7401, userCanPublish: true, botCanPublish: false, kind: "private");
        var challengeResponse = await account.Client.PostAsJsonAsync("/v1/destinations/challenges",
            new DestinationChallengeRequest(7401));
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<DestinationChallenge>())!;
        var denied = await account.Client.PostAsJsonAsync("/v1/destinations",
            new DestinationRequest(7401, "My private chat", challenge.Proof));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        f.TelegramApi.SetRights(7401, 7401, userCanPublish: true, botCanPublish: true, kind: "private");
        var freshChallenge = (await (await account.Client.PostAsJsonAsync("/v1/destinations/challenges",
            new DestinationChallengeRequest(7401))).Content.ReadFromJsonAsync<DestinationChallenge>())!;
        var linked = await account.Client.PostAsJsonAsync("/v1/destinations",
            new DestinationRequest(7401, "My private chat", freshChallenge.Proof));
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
    }
}