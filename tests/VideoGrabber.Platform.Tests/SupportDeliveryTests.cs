using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Support;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SupportDeliveryTests
{
    private static async Task<ApiFixture> DeliveryFixtureAsync()
    {
        var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        f.SupportNotificationsEnabled = true;
        await f.RestartAsync();
        return f;
    }

    private static async Task<Guid> SubmitAsync(ApiFixture f)
    {
        using var response = await f.Anonymous.PostAsJsonAsync("/v1/support/requests", new
        {
            requestId = Guid.NewGuid(), topic = "download", contact = "synthetic-client@example.test",
            message = "Тест обращения: не получается сохранить видео <script>test</script> https://example.test/",
            altcha = await SupportRequestTests.SolveAsync(f.Anonymous), website = ""
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("ticketId").GetGuid();
    }

    private static async Task WaitAsync(Func<Task<bool>> predicate)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await predicate()) return;
            await Task.Delay(100);
        }
        Assert.Fail("Notification did not reach its expected durable state.");
    }

    private static async Task<string?> StateAsync(ApiFixture f, Guid ticket, string channel)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var query = new NpgsqlCommand("select state from support.notifications where ticket_id=@ticket and channel=@channel", connection);
        query.Parameters.AddWithValue("ticket", ticket); query.Parameters.AddWithValue("channel", channel);
        return (string?)await query.ExecuteScalarAsync();
    }

    [Fact]
    public async Task Telegram_delivers_only_to_owner_when_email_is_unconfigured()
    {
        await using var f = await DeliveryFixtureAsync();
        var ticket = await SubmitAsync(f);
        await WaitAsync(async () => await StateAsync(f, ticket, "telegram") == "delivered");
        var sent = Assert.Single(f.TelegramApi.Requests, request => request.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(sent.Body);
        Assert.Equal(99001L, body.RootElement.GetProperty("chat_id").GetInt64());
        Assert.False(body.RootElement.TryGetProperty("parse_mode", out _));
        Assert.True(body.RootElement.GetProperty("link_preview_options").GetProperty("is_disabled").GetBoolean());
        Assert.Contains("<script>test</script>", body.RootElement.GetProperty("text").GetString());
        Assert.Equal("pending", await StateAsync(f, ticket, "email"));
    }

    [Fact]
    public async Task Unknown_telegram_ack_is_retained_for_review_without_blind_retry()
    {
        await using var f = await DeliveryFixtureAsync();
        f.TelegramApi.LoseNextMessageAck = true;
        var ticket = await SubmitAsync(f);
        await WaitAsync(async () => await StateAsync(f, ticket, "telegram") == "review_required");
        f.Clock.Advance(TimeSpan.FromHours(1));
        await Task.Delay(1200);
        Assert.Single(f.TelegramApi.Requests, request => request.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(60, 0)]
    [InlineData(200, -2)]
    public async Task Notification_budget_is_durable_and_blocks_dispatch(int reservations, int hoursOffset)
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        var ticket = await SubmitAsync(f);
        await using (var connection = await f.Database.OpenConnectionAsync())
        await using (var reserve = new NpgsqlCommand("""
            insert into support.notification_attempts(claim_id,ticket_id,channel,reserved_at)
            select gen_random_uuid(),@ticket,'telegram',@at from generate_series(1,@count)
            """, connection))
        {
            reserve.Parameters.AddWithValue("ticket", ticket); reserve.Parameters.AddWithValue("at", f.Clock.GetUtcNow().AddHours(hoursOffset));
            reserve.Parameters.AddWithValue("count", reservations); await reserve.ExecuteNonQueryAsync();
        }
        await f.RestartAsync();
        await f.Service<SupportNotificationWorker>().RunOnceAsync(CancellationToken.None);
        Assert.DoesNotContain(f.TelegramApi.Requests, request => request.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        Assert.Equal("pending", await StateAsync(f, ticket, "telegram"));
    }

    [Fact]
    public async Task Expired_sending_lease_becomes_unknown_instead_of_resending()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        var ticket = await SubmitAsync(f);
        await using (var connection = await f.Database.OpenConnectionAsync())
        await using (var interrupted = new NpgsqlCommand("""
            update support.notifications set state='sending',attempts=1,claim_id=gen_random_uuid(),claim_expires_at=@past
            where ticket_id=@ticket and channel='telegram'
            """, connection))
        {
            interrupted.Parameters.AddWithValue("past", f.Clock.GetUtcNow().AddMinutes(-1)); interrupted.Parameters.AddWithValue("ticket", ticket);
            await interrupted.ExecuteNonQueryAsync();
        }
        await f.Service<SupportNotificationWorker>().RunOnceAsync(CancellationToken.None);
        Assert.Equal("review_required", await StateAsync(f, ticket, "telegram"));
        Assert.DoesNotContain(f.TelegramApi.Requests, request => request.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_unicode_message_keeps_telegram_caption_bounded_without_html_parse()
    {
        var payload = new SupportPayload("client@example.test", string.Concat(Enumerable.Repeat("😀", 2000)));
        var notification = new SupportNotification(Guid.NewGuid(), Guid.NewGuid(), 1, "other", "form", DateTimeOffset.UtcNow, payload);
        var compact = SupportNotificationText.Format(notification, true);
        Assert.True(compact.Length <= 4096);
        Assert.False(char.IsHighSurrogate(compact[^1]));
        Assert.Contains("😀", compact);
    }

    [Theory]
    [InlineData(429, 3, "failed")]
    [InlineData(403, 1, "failed")]
    [InlineData(502, 1, "review_required")]
    public async Task Telegram_failure_attempts_are_bounded(int status, int expectedAttempts, string expectedState)
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        f.TelegramApi.MessageStatusCode = (HttpStatusCode)status;
        var ticket = await SubmitAsync(f);
        var worker = f.Service<SupportNotificationWorker>();
        for (var i = 0; i < 5; i++)
        {
            await worker.RunOnceAsync(CancellationToken.None);
            f.Clock.Advance(TimeSpan.FromMinutes(3));
        }
        Assert.Equal(expectedAttempts, f.TelegramApi.Requests.Count(request => request.Path.EndsWith("/sendMessage", StringComparison.Ordinal)));
        Assert.Equal(expectedState, await StateAsync(f, ticket, "telegram"));
    }

    [Fact]
    public async Task Telegram_retry_after_preserves_attempt_until_server_delay_passes()
    {
        await using var f = await ApiFixture.StartAsync(); f.SupportEnabled=true; await f.RestartAsync();
        f.TelegramApi.MessageStatusCode=HttpStatusCode.TooManyRequests; f.TelegramApi.MessageRetryAfter=120;
        var ticket=await SubmitAsync(f); var worker=f.Service<SupportNotificationWorker>();
        await worker.RunOnceAsync(CancellationToken.None);
        f.Clock.Advance(TimeSpan.FromSeconds(60)); await worker.RunOnceAsync(CancellationToken.None);
        Assert.Single(f.TelegramApi.Requests, x=>x.Path.EndsWith("/sendMessage"));
        f.TelegramApi.MessageStatusCode=null; f.Clock.Advance(TimeSpan.FromSeconds(61));
        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal("delivered",await StateAsync(f,ticket,"telegram"));
    }
}
