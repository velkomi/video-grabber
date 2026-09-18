using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class BotAccountWorkflowTests
{
    [Fact]
    public async Task Telegram_and_desktop_see_the_same_account_balance()
    {
        await using var f = await ApiFixture.StartAsync();
        var desktop = await f.AccountAsync("telegram", "5001");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(desktop.Id, "credits", 0, 3, null, "shared gift", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var profile = await desktop.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        var access = await desktop.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");

        using var miniApp = await f.MiniAppAsync(5001);
        var telegramProfile = await miniApp.GetFromJsonAsync<AccountProfile>("/v1/me");
        var telegramAccess = await miniApp.GetFromJsonAsync<AccessSnapshot>("/v1/access");

        Assert.Equal(profile!.AccountId, telegramProfile!.AccountId);
        Assert.Equal(access, telegramAccess);
        Assert.Contains("telegram", profile.LinkedProviders);
        Assert.Equal(3, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Callback_is_single_use_actor_action_and_resource_bound()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("telegram", "6101");
        var b = await f.AccountAsync("telegram", "6102");
        var store = f.Service<BotCallbackStore>();
        var resource = Guid.NewGuid();
        var token = await store.CreateAsync(a.Id, "job:retry", resource, CancellationToken.None);

        Assert.Null(await store.ConsumeAsync(b.Id, token, CancellationToken.None));
        var grant = await store.ConsumeAsync(a.Id, token, CancellationToken.None);
        Assert.NotNull(grant);
        Assert.Equal("job:retry", grant!.Action);
        Assert.Equal(resource, grant.ResourceId);
        Assert.Null(await store.ConsumeAsync(a.Id, token, CancellationToken.None));

        var stale = await store.CreateAsync(a.Id, "account:refresh", null, CancellationToken.None);
        f.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Null(await store.ConsumeAsync(a.Id, stale, CancellationToken.None));
    }

    [Fact]
    public async Task Webhook_inbox_worker_sends_real_balance_through_Bot_API_emulator()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "6201");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 7, null, "bot balance", Guid.NewGuid()))).EnsureSuccessStatusCode();
        f.TelegramApi.Clear();

        await PostWebhookAsync(f, 9001, new
        {
            update_id = 9001L,
            message = new
            {
                message_id = 1L,
                from = new { id = 6201L },
                chat = new { id = 6201L, type = "private" },
                text = "/balance"
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>().RunOnceAsync(CancellationToken.None));

        var request = Assert.Single(f.TelegramApi.Requests, x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        var messageText = BotText(request);
        Assert.Contains("7", messageText, StringComparison.Ordinal);
        Assert.Contains("баланс", messageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Group_account_command_never_discloses_private_profile()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "6301");
        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9002, new
        {
            update_id = 9002L,
            message = new
            {
                message_id = 2L,
                from = new { id = 6301L },
                chat = new { id = -1001234567890L, type = "group" },
                text = "/account"
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>().RunOnceAsync(CancellationToken.None));
        var request = Assert.Single(f.TelegramApi.Requests, x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        Assert.DoesNotContain(account.Id.ToString("D"), request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("личн", BotText(request), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ordinary_user_cannot_forge_admin_keyboard()
    {
        await using var f = await ApiFixture.StartAsync();
        _ = await f.AccountAsync("telegram", "6401");
        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9003, new
        {
            update_id = 9003L,
            message = new
            {
                message_id = 3L,
                from = new { id = 6401L },
                chat = new { id = 6401L, type = "private" },
                text = "/admin"
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>().RunOnceAsync(CancellationToken.None));
        var request = Assert.Single(f.TelegramApi.Requests, x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        Assert.Contains("недоступ", BotText(request), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("callback_data", request.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stolen_callback_does_not_consume_the_rightful_owners_token()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("telegram", "6501");
        _ = await f.AccountAsync("telegram", "6502");
        var token = await f.Service<BotCallbackStore>().CreateAsync(
            a.Id, "account:refresh", null, CancellationToken.None);
        f.TelegramApi.Clear();

        await PostWebhookAsync(f, 9004, new
        {
            update_id = 9004L,
            callback_query = new
            {
                id = "stolen-callback",
                from = new { id = 6502L },
                data = token,
                message = new { chat = new { id = 6502L, type = "private" } }
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>().RunOnceAsync(CancellationToken.None));
        var stolen = Assert.Single(f.TelegramApi.Requests, x => x.Path.EndsWith("/answerCallbackQuery", StringComparison.Ordinal));
        Assert.Contains("устар", BotText(stolen), StringComparison.OrdinalIgnoreCase);

        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9005, new
        {
            update_id = 9005L,
            callback_query = new
            {
                id = "owner-callback",
                from = new { id = 6501L },
                data = token,
                message = new { chat = new { id = 6501L, type = "private" } }
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>().RunOnceAsync(CancellationToken.None));
        Assert.Contains(f.TelegramApi.Requests,
            x => x.Path.EndsWith("/answerCallbackQuery", StringComparison.Ordinal)
                 && BotText(x).Contains("обнов", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Owner_admin_command_generates_one_use_MFA_console_link()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "6650");
        await f.PromoteAdminAsync(account.Id);
        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9010, new
        {
            update_id = 9010L,
            message = new
            {
                message_id = 10L,
                from = new { id = 6650L },
                chat = new { id = 6650L, type = "private" },
                text = "/admin"
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>().RunOnceAsync(CancellationToken.None));
        var request = Assert.Single(f.TelegramApi.Requests, x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        Assert.Contains("fresh MFA", BotText(request), StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(request.Body);
        var url = json.RootElement.GetProperty("reply_markup")
            .GetProperty("inline_keyboard")[0][0].GetProperty("url").GetString();
        Assert.NotNull(url);
        Assert.Contains("/v1/telegram/admin-links/", url!, StringComparison.Ordinal);
        Assert.DoesNotContain("callback_data", request.Body, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Admin_console_link_requires_fresh_MFA_and_is_single_use()
    {
        await using var f = await ApiFixture.StartAsync();
        var adminAccount = await f.AccountAsync("telegram", "6701");
        await f.PromoteAdminAsync(adminAccount.Id);
        var token = await f.Service<BotCallbackStore>().CreateAsync(
            adminAccount.Id, "admin:console", null, CancellationToken.None);

        using var withoutMfa = await adminAccount.Client.GetAsync(
            "/v1/telegram/admin-links/" + Uri.EscapeDataString(token));
        Assert.Equal(HttpStatusCode.Unauthorized, withoutMfa.StatusCode);

        using var mfa = f.FreshMfaClient(adminAccount.Id);
        using var accepted = await mfa.GetAsync(
            "/v1/telegram/admin-links/" + Uri.EscapeDataString(token));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal("/admin/", accepted.Headers.Location?.OriginalString);

        using var replay = await mfa.GetAsync(
            "/v1/telegram/admin-links/" + Uri.EscapeDataString(token));
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
    }
    private static string BotText(TelegramApiRequest request)
    {
        using var json = JsonDocument.Parse(request.Body);
        return json.RootElement.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }
    private static async Task PostWebhookAsync(ApiFixture f, long updateId, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/telegram/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", TelegramWebhookTests.WebhookSecret);
        using var response = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}