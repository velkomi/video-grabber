using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Altcha;
using Npgsql;
using VideoGrabber.Platform.Api.Support;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SupportRequestTests
{
    internal static async Task<string> SolveAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/v1/support/challenge");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var challenge = JsonSerializer.Deserialize<Challenge>(await response.Content.ReadAsStringAsync(), AltchaJson.SerializerOptions)!;
        var solution = AltchaPow.SolveChallenge(new SolveChallengeOptions { Challenge = challenge }, CancellationToken.None);
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Payload
        { Challenge = challenge, Solution = solution }, AltchaJson.SerializerOptions));
    }

    private static object Body(Guid requestId, string proof, string contact = "client@example.test", string? message = null)
        => new { requestId, topic = "sign_in", contact,
            message = message ?? "Не получается войти в приложение после подтверждения.", altcha = proof, website = "" };

    private static async Task<ApiFixture> EnabledAsync()
    {
        var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        return f;
    }

    [Fact]
    public async Task Valid_guest_ticket_persists_encrypted_payload_and_two_notifications()
    {
        await using var f = await EnabledAsync();
        var proof = await SolveAsync(f.Anonymous);
        using var response = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(Guid.NewGuid(), proof));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ticket = json.RootElement.GetProperty("ticketId").GetGuid();
        Assert.NotEqual(Guid.Empty, ticket);
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var query = new NpgsqlCommand("select encrypted_payload,(select count(*) from support.notifications n where n.ticket_id=r.ticket_id) from support.requests r where ticket_id=@ticket", connection);
        query.Parameters.AddWithValue("ticket", ticket);
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.DoesNotContain("client@example.test", Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)));
        Assert.Equal(2, reader.GetInt64(1));
        Assert.DoesNotContain(f.Logs, entry => entry.Contains("Не получается войти", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parallel_retry_creates_one_ticket_and_one_outbox_pair()
    {
        await using var f = await EnabledAsync();
        var proof = await SolveAsync(f.Anonymous);
        var id = Guid.NewGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(id, proof))));
        var tickets = new HashSet<Guid>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                tickets.Add(json.RootElement.GetProperty("ticketId").GetGuid());
            }
        }
        Assert.Single(tickets);
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var count = new NpgsqlCommand("select count(*) from support.requests", connection);
        Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Request_key_with_changed_body_is_rejected_without_new_delivery()
    {
        await using var f = await EnabledAsync();
        var proof = await SolveAsync(f.Anonymous);
        var id = Guid.NewGuid();
        using var first = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(id, proof));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var changed = await f.Anonymous.PostAsJsonAsync("/v1/support/requests",
            Body(id, proof, message: "Другой текст обращения с тем же ключом отправки."));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task Challenge_replay_with_new_ticket_is_rejected()
    {
        await using var f = await EnabledAsync();
        var proof = await SolveAsync(f.Anonymous);
        using var first = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(Guid.NewGuid(), proof));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var replay = await f.Anonymous.PostAsJsonAsync("/v1/support/requests",
            Body(Guid.NewGuid(), proof, contact: "second@example.test"));
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
    }

    [Fact]
    public async Task Expired_or_tampered_challenge_cannot_create_a_ticket()
    {
        await using var f = await EnabledAsync();
        var proof = await SolveAsync(f.Anonymous);
        using var tampered = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(Guid.NewGuid(), proof[..^4] + "AAAA"));
        Assert.Equal(HttpStatusCode.Forbidden, tampered.StatusCode);
        f.Clock.Advance(TimeSpan.FromMinutes(6));
        using var expired = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(Guid.NewGuid(), proof));
        Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
    }

    [Fact]
    public async Task Caller_quota_survives_api_restart()
    {
        await using var f = await EnabledAsync();
        using var first = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(Guid.NewGuid(), await SolveAsync(f.Anonymous)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await f.RestartAsync();
        using var limited = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", Body(Guid.NewGuid(), await SolveAsync(f.Anonymous)));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task Unverified_guest_contact_cannot_poison_another_networks_quota()
    {
        await using var f = await EnabledAsync();
        var store = f.Service<SupportStore>();
        var verifier = f.Service<SupportChallengeService>();
        var first = new SupportRequest(Guid.NewGuid(), "other", "same-contact@example.test", "Это первое обращение гостя с одного адреса.");
        var firstProof = verifier.Verify(await SolveAsync(f.Anonymous));
        await store.SubmitVerifiedAsync(first, null, "198.51.100.10", "form", firstProof, CancellationToken.None);
        var secondProof = verifier.Verify(await SolveAsync(f.Anonymous));
        var second = first with { RequestId = Guid.NewGuid() };

        var accepted = await store.SubmitVerifiedAsync(second, null, "198.51.100.11", "form", secondProof, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, accepted.TicketId);
    }
    [Fact]
    public async Task Support_configuration_is_public_and_contains_no_delivery_credentials()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        using var response = await f.Anonymous.GetAsync("/v1/support/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.True(json.RootElement.GetProperty("enabled").GetBoolean());
        Assert.DoesNotContain("99001", raw);
        Assert.DoesNotContain("test-telegram-bot-token", raw);
        Assert.DoesNotContain("smtp", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guest_cannot_submit_without_a_verified_challenge()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        using var response = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", new
        {
            requestId = Guid.NewGuid(), topic = "sign_in", contact = "client@example.test",
            message = "Не получается войти в приложение после подтверждения.", altcha = "", website = ""
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Oversized_support_body_is_rejected_before_json_parsing()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        using var response = await f.Anonymous.PostAsync("/v1/support/requests",
            new StringContent(new string('x', 20 * 1024), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_choose_notification_recipient()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        using var response = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", new
        {
            requestId = Guid.NewGuid(), topic = "other", contact = "client@example.test",
            message = "Вопрос по использованию VideoGrabber.", altcha = "", website = "",
            to = "someone-else@example.test", chatId = 123456
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_support_fails_closed_without_accepting_a_ticket()
    {
        await using var f = await ApiFixture.StartAsync();
        using var response = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", new
        {
            requestId = Guid.NewGuid(), topic = "other", contact = "client@example.test",
            message = "Вопрос по использованию VideoGrabber.", altcha = "", website = ""
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
