using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Telegram;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class TelegramWebhookTests
{
    internal const string WebhookSecret = "test-webhook-secret-2026";
    internal static readonly byte[] InboxKey = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();

    [Fact]
    public async Task Duplicate_update_is_only_inserted_once_under_concurrency()
    {
        await using var f = await ApiFixture.StartAsync();
        var inbox = new TelegramUpdateInbox(f.Database, InboxKey, f.Clock);
        var update = new TelegramUpdate(100, JsonSerializer.SerializeToElement(new { update_id = 100, message = new { text = "hello" } }));

        var accepted = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => inbox.AcceptAsync(update, CancellationToken.None)));

        Assert.Single(accepted, value => value);
        var restarted = new TelegramUpdateInbox(f.Database, InboxKey, f.Clock);
        Assert.False(await restarted.AcceptAsync(update, CancellationToken.None));
        Assert.Equal(1L, await CountAsync(f, 100));
    }

    [Fact]
    public async Task Inbox_encrypts_payload_strips_secret_fields_and_claims_once()
    {
        await using var f = await ApiFixture.StartAsync();
        var inbox = new TelegramUpdateInbox(f.Database, InboxKey, f.Clock);
        const string textSentinel = "PRIVATE_MESSAGE_SENTINEL_81c4";
        const string tokenSentinel = "ACCESS_TOKEN_SENTINEL_f321";
        var update = new TelegramUpdate(101, JsonSerializer.SerializeToElement(new
        {
            update_id = 101,
            message = new { text = textSentinel, access_token = tokenSentinel, from = new { id = 7001L } }
        }));

        Assert.True(await inbox.AcceptAsync(update, CancellationToken.None));
        var encrypted = await CipherTextAsync(f, 101);
        Assert.DoesNotContain(textSentinel, Encoding.UTF8.GetString(encrypted), StringComparison.Ordinal);
        Assert.DoesNotContain(tokenSentinel, Encoding.UTF8.GetString(encrypted), StringComparison.Ordinal);

        var claimed = await inbox.ClaimAsync(CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.Equal(101L, claimed!.UpdateId);
        var json = claimed.Body.GetRawText();
        Assert.Contains(textSentinel, json, StringComparison.Ordinal);
        Assert.DoesNotContain(tokenSentinel, json, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await inbox.ClaimAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Webhook_requires_fixed_secret_and_deduplicates_retries()
    {
        await using var f = await ApiFixture.StartAsync();
        var json = JsonSerializer.Serialize(new { update_id = 102L, message = new { text = "hello", from = new { id = 7002L } } });

        using var missing = await f.Anonymous.PostAsync("/v1/telegram/webhook",
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/telegram/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", WebhookSecret);
        using var accepted = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        using var retry = new HttpRequestMessage(HttpMethod.Post, "/v1/telegram/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        retry.Headers.Add("X-Telegram-Bot-Api-Secret-Token", WebhookSecret);
        using var duplicate = await f.Anonymous.SendAsync(retry);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(1L, await CountAsync(f, 102));
    }

    [Fact]
    public async Task Webhook_accepts_100_update_synthetic_burst_under_one_second()
    {
        await using var f = await ApiFixture.StartAsync();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var responses = await Task.WhenAll(Enumerable.Range(2000, 100).Select(async updateId =>
        {
            var json = JsonSerializer.Serialize(new
            {
                update_id = (long)updateId,
                message = new { text = "burst", from = new { id = 8000L + updateId } }
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/telegram/webhook")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", WebhookSecret);
            return await f.Anonymous.SendAsync(request);
        }));
        watch.Stop();
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"Burst took {watch.Elapsed}.");
            Assert.Equal(100L, await CountRangeAsync(f, 2000, 2099));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }
    [Fact]
    public async Task Webhook_rejects_body_over_one_megabyte()
    {
        await using var f = await ApiFixture.StartAsync();
        var huge = "{\"update_id\":103,\"message\":{\"text\":\"" + new string('x', 1024 * 1024) + "\"}}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/telegram/webhook")
        {
            Content = new StringContent(huge, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", WebhookSecret);
        using var response = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0L, await CountAsync(f, 103));
    }

    private static async Task<long> CountRangeAsync(ApiFixture f, long first, long last)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.telegram_updates where update_id between @first and @last", connection);
        command.Parameters.AddWithValue("first", first);
        command.Parameters.AddWithValue("last", last);
        return (long)(await command.ExecuteScalarAsync())!;
    }
    private static async Task<long> CountAsync(ApiFixture f, long updateId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.telegram_updates where update_id=@id", connection);
        command.Parameters.AddWithValue("id", updateId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<byte[]> CipherTextAsync(ApiFixture f, long updateId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select payload_cipher from licensing.telegram_updates where update_id=@id", connection);
        command.Parameters.AddWithValue("id", updateId);
        return (byte[])(await command.ExecuteScalarAsync())!;
    }
}