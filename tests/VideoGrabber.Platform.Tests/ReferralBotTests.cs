using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class ReferralBotTests
{
    [Theory]
    [InlineData("/referral")]
    [InlineData("/bonus")]
    [InlineData("/promo")]
    public async Task Promotion_commands_do_not_disclose_private_data_in_groups(string command)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(command, "group");
        Assert.Contains("личн", fixture.LastText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("opaque", fixture.LastText());
    }

    [Theory]
    [InlineData("/referral")]
    [InlineData("/bonus")]
    [InlineData("/start ref_opaque")]
    public async Task Disabled_promotions_explain_availability_without_fake_balances(string command)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(command);
        Assert.Contains("недоступ", fixture.LastText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("opaque", fixture.LastText());
        Assert.DoesNotContain("0 ₽", fixture.LastText());
    }

    [Fact]
    public async Task Promo_command_guides_to_mini_app_without_issuing_an_invoice()
    {
        using var fixture = new Fixture();
        await fixture.SendAsync("/promo SAVE10");
        Assert.Contains("промокод", fixture.LastText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stars", fixture.LastText());
        Assert.DoesNotContain(fixture.Api.Requests, request => request.Path.EndsWith("/createInvoiceLink"));
        Assert.DoesNotContain("SAVE10", fixture.LastText());
    }

    [Fact]
    public async Task Group_start_referral_never_claims_or_echoes_the_code()
    {
        using var fixture = new Fixture();
        await fixture.SendAsync("/start ref_opaque", "group");
        Assert.DoesNotContain("opaque", fixture.LastText());
    }

    [Fact]
    public async Task Enabled_private_start_claims_once_and_linked_account_reads_real_bonus_summary()
    {
        await using var f = await ApiFixture.StartAsync();
        f.PromotionsEnabled = true;
        await f.RestartAsync();
        var inviter = await f.AccountAsync("email", "bot-referral-owner");
        var source = await f.AccountAsync("telegram", "963201", includeStarter: true, telegramOnly: true);
        var target = await f.AccountAsync("email", "bot-referral-linked-target", includeStarter: true);
        var store = f.Service<PromotionStore>();
        var invitation = await store.SummaryAsync(inviter.Id, CancellationToken.None);
        var handler = f.Service<BotCommandHandler>();
        async Task Send(string text, string type = "private") => await handler.HandleAsync(new TelegramUpdate(2, JsonSerializer.SerializeToElement(new
        {
            message = new { from = new { id = 963201L }, chat = new { id = 963201L, type }, text }
        })), CancellationToken.None);

        await Send("/start ref_" + invitation.Code, "group");
        Assert.Equal(0, (await store.SummaryAsync(inviter.Id, CancellationToken.None)).Invited);
        await Send("/start ref_" + invitation.Code);
        await Send("/start ref_" + invitation.Code);
        Assert.Equal(1, (await store.SummaryAsync(inviter.Id, CancellationToken.None)).Invited);
        var ticket = await f.Service<TelegramAccountLinkService>().BeginAsync(source.Id, 963201L, CancellationToken.None);
        var token = ticket.LinkUri.Query.TrimStart('?').Split('&').Select(pair => pair.Split('=', 2))
            .Where(pair => Uri.UnescapeDataString(pair[0]) == "telegram_link")
            .Select(pair => Uri.UnescapeDataString(pair[1])).Single();
        using var linked = await target.Client.PostAsJsonAsync("/v1/telegram/account-link/complete", new CompleteTelegramAccountLinkRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);
        Assert.Equal(target.Id, await f.Service<ITelegramAccountResolver>().ResolveAsync(963201L, f.Clock.GetUtcNow(), CancellationToken.None));
        await Send("/bonus");
        using var message = JsonDocument.Parse(f.TelegramApi.Requests.Last(row => row.Path.EndsWith("/sendMessage")).Body);
        var body = message.RootElement.GetProperty("text").GetString()!;
        Assert.Contains("Доступно", body);
        Assert.Contains("Ожидает", body);
        Assert.Contains("Stars", body);
        Assert.DoesNotContain(invitation.Code, body);
        Assert.DoesNotContain(f.TelegramApi.Requests, row => row.Path.EndsWith("/createInvoiceLink"));
    }

    private sealed class Fixture : IDisposable
    {
        public TelegramApiEmulator Api { get; } = new();
        private readonly HttpClient _http;
        private readonly BotCommandHandler _handler;
        public Fixture()
        {
            _http = new HttpClient(Api);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VG_TELEGRAM_MINIAPP_URL"] = "https://videograbber.example.test/miniapp/"
            }).Build();
            var bot = new BotApiClient(new ClientFactory(_http), new TelegramSecurityOptions("synthetic-referral-token", "synthetic-webhook", new byte[32]), configuration);
            _handler = new BotCommandHandler(new AccountResolver(), null!, null!, null!, null!, null!, null!, null!, bot, null!, null!, null!, TimeProvider.System, configuration);
        }
        public Task SendAsync(string text, string chatType = "private") => _handler.HandleAsync(new TelegramUpdate(1, JsonSerializer.SerializeToElement(new
        {
            message = new { from = new { id = 9991L }, chat = new { id = 9991L, type = chatType }, text }
        })), CancellationToken.None);
        public string LastText()
        {
            using var json = JsonDocument.Parse(Api.Requests.Last(request => request.Path.EndsWith("/sendMessage")).Body);
            return json.RootElement.GetProperty("text").GetString()!;
        }
        public void Dispose() => _http.Dispose();
    }
    private sealed class ClientFactory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class AccountResolver : ITelegramAccountResolver
    {
        public Task<Guid> ResolveAsync(long userId, DateTimeOffset authTime, CancellationToken cancellationToken) => Task.FromResult(Guid.Empty);
    }
}
