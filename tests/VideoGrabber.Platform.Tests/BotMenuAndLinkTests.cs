using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class BotMenuAndLinkTests
{
    [Theory]
    [InlineData("/contacts")]
    [InlineData("Контакты")]
    public async Task Contacts_offer_owner_site_and_support_without_payment_or_private_data(string command)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(command);
        var message = fixture.LastMessage();
        Assert.Contains("velkoshkin@gmail.com", message.GetProperty("text").GetString());
        var urls = message.GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray()
            .SelectMany(row => row.EnumerateArray()).Select(button => button.GetProperty("url").GetString());
        Assert.Contains("https://valery.srv1902378.hstgr.cloud/", urls);
        Assert.Contains("https://t.me/Velkoshkin", urls);
        Assert.DoesNotContain(fixture.Api.Requests, request => request.Path.EndsWith("/sendVideo") || request.Path.EndsWith("/sendInvoice"));
    }

    [Theory]
    [InlineData("/documents")]
    [InlineData("Документы")]
    public async Task Documents_use_configured_site_origin_and_offer_separate_pages(string command)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(command);
        var buttons = fixture.LastMessage().GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray()
            .SelectMany(row => row.EnumerateArray()).ToArray();
        Assert.Contains(buttons, button => button.GetProperty("url").GetString()=="https://videograbber.example.test/info/?document=privacy");
        Assert.Contains(buttons, button => button.GetProperty("url").GetString()=="https://videograbber.example.test/info/?document=terms");
        Assert.DoesNotContain(buttons, button => button.GetProperty("url").GetString()!.Contains("token="));
    }

    [Fact]
    public async Task Menu_can_be_hidden_and_restored_without_opening_the_mini_app()
    {
        using var fixture = new Fixture();
        await fixture.SendAsync("/menu");
        var shown = fixture.LastMessage();
        var markup = shown.GetProperty("reply_markup");
        var buttons = markup.GetProperty("keyboard").EnumerateArray()
            .SelectMany(row => row.EnumerateArray()).Select(button => button.GetProperty("text").GetString());
        Assert.Contains("Скачать видео", buttons);
        Assert.Contains("Инструменты", buttons);
        Assert.Contains("Скрыть меню", buttons);
        Assert.Contains("Приложение", buttons);
        Assert.False(markup.GetProperty("one_time_keyboard").GetBoolean());

        await fixture.SendAsync("Скрыть меню");
        Assert.True(fixture.LastMessage().GetProperty("reply_markup").GetProperty("remove_keyboard").GetBoolean());
        Assert.Contains("/menu", fixture.LastMessage().GetProperty("text").GetString());

        await fixture.SendAsync("/menu");
        Assert.True(fixture.LastMessage().GetProperty("reply_markup").TryGetProperty("keyboard", out _));
        await fixture.SendAsync("/hide");
        Assert.True(fixture.LastMessage().GetProperty("reply_markup").GetProperty("remove_keyboard").GetBoolean());
    }

    [Theory]
    [InlineData("https://media.example.test/video.mp4")]
    [InlineData("/download https://media.example.test/video.mp4")]
    public async Task A_link_in_private_chat_reaches_the_same_account_gate_as_download(string input)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(input);
        Assert.Contains("/link", fixture.LastMessage().GetProperty("text").GetString());
        Assert.DoesNotContain(fixture.Api.Requests, request => request.Path.EndsWith("/sendVideo"));
    }

    [Theory]
    [InlineData("https://media.example.test/video.mp4")]
    [InlineData("Скачать видео")]
    [InlineData("Аккаунт")]
    [InlineData("/menu")]
    [InlineData("Приложение")]
    public async Task Link_and_menu_actions_cannot_bypass_the_private_chat_guard(string input)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(input, "group");
        Assert.Contains("личн", fixture.LastMessage().GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(fixture.LastMessage().TryGetProperty("reply_markup", out _));
        Assert.DoesNotContain(fixture.Api.Requests, request => request.Path.EndsWith("/sendVideo"));
    }

    [Theory]
    [InlineData("/start")]
    [InlineData("Помощь")]
    public async Task Welcome_and_help_offer_the_keyboard_and_explain_plain_links(string input)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(input);
        var message = fixture.LastMessage();
        Assert.Contains("ссылку", message.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(message.GetProperty("reply_markup").TryGetProperty("keyboard", out _));
    }

    [Theory]
    [InlineData("/settings")]
    [InlineData("Приложение")]
    public async Task Mini_app_opens_in_telegram_instead_of_an_unauthenticated_external_browser(string input)
    {
        using var fixture = new Fixture();
        await fixture.SendAsync(input);
        var button = fixture.LastMessage().GetProperty("reply_markup").GetProperty("inline_keyboard")[0][0];
        Assert.Equal("https://videograbber.example.test/miniapp/", button.GetProperty("web_app").GetProperty("url").GetString());
        Assert.False(button.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Video_caption_is_plain_text_and_oversize_captions_never_reach_the_transport()
    {
        using var api = new TelegramApiEmulator();
        using var http = new HttpClient(api);
        var bot = new BotApiClient(new ClientFactory(http), new TelegramSecurityOptions("synthetic-caption-token", "synthetic-webhook", new byte[32]), new ConfigurationBuilder().Build());
        var source = new Uri("https://media.example.test/video.mp4");
        await Assert.ThrowsAsync<ArgumentException>(() => bot.SendVideoUrlAsync(9123, source, new string('a', 1025), default));
        Assert.Empty(api.Requests);
        const string caption = "Название: <b>Видео</b> & пример\nДлительность: неизвестна";
        await bot.SendVideoUrlAsync(9123, source, caption, default);
        var sent = Assert.Single(api.Requests);
        using var json = JsonDocument.Parse(sent.Body);
        Assert.Equal(caption, json.RootElement.GetProperty("caption").GetString());
        Assert.False(json.RootElement.TryGetProperty("parse_mode", out _));
    }

    [Fact]
    public async Task Tools_explain_the_windows_requirement_and_back_restores_the_main_keyboard()
    {
        using var fixture = new Fixture();
        await fixture.SendAsync("Инструменты");
        var tools = fixture.LastMessage();
        Assert.Contains("Windows", tools.GetProperty("text").GetString());
        var buttons = tools.GetProperty("reply_markup").GetProperty("keyboard").EnumerateArray()
            .SelectMany(row => row.EnumerateArray()).Select(button => button.GetProperty("text").GetString());
        Assert.Contains("MP3", buttons);
        Assert.Contains("Курс", buttons);
        Assert.Contains("Назад", buttons);
        await fixture.SendAsync("Назад");
        var restored = fixture.LastMessage().GetProperty("reply_markup").GetProperty("keyboard").EnumerateArray()
            .SelectMany(row => row.EnumerateArray()).Select(button => button.GetProperty("text").GetString());
        Assert.Contains("Скачать видео", restored);
    }

    [Fact]
    public async Task Already_linked_account_gets_a_clear_next_step_and_menu()
    {
        var profile = new AccountProfile(Guid.Empty, "user", false, ["email", "telegram"], null) { PrimaryAuthProvider = "email" };
        using var fixture = new Fixture(profile);
        await fixture.SendAsync("/link");
        var message = fixture.LastMessage();
        Assert.Contains("уже привязан", message.GetProperty("text").GetString());
        Assert.Contains("ссылку", message.GetProperty("text").GetString());
        Assert.True(message.GetProperty("reply_markup").TryGetProperty("keyboard", out _));
    }

    [Fact]
    public async Task Confirmed_video_metadata_updates_the_caption_once_without_exposing_transport_details()
    {
        using var api = new TelegramApiEmulator
        {
            DirectVideoMetadata = JsonSerializer.SerializeToElement(new { width = 1920, height = 1080, duration = 125, file_name = "My\nvideo.mp4", file_id = "private-telegram-file-id" }),
            DirectVideoDate = 1791403200
        };
        using var http = new HttpClient(api);
        var bot = VideoClient(http);
        var sent = await bot.SendVideoUrlAsync(9123, new Uri("https://media.example.test/transport.mp4?signature=synthetic-private-signature"), ProvisionalCaption, default);
        Assert.Equal(88, sent.MessageId);
        Assert.Single(api.Requests, request => request.Path.EndsWith("/sendVideo"));
        var edit = Assert.Single(api.Requests, request => request.Path.EndsWith("/editMessageCaption"));
        using var json = JsonDocument.Parse(edit.Body);
        var caption = json.RootElement.GetProperty("caption").GetString()!;
        Assert.Contains("1920 × 1080", caption);
        Assert.Contains("02:05", caption);
        Assert.Contains(DateTimeOffset.FromUnixTimeSeconds(1791403200).ToString("dd.MM.yyyy HH:mm 'UTC'"), caption);
        Assert.Contains("My video.mp4", caption);
        Assert.Contains("https://public.example.test/original", caption);
        Assert.DoesNotContain("synthetic-private-signature", caption);
        Assert.DoesNotContain("private-telegram-file-id", caption);
        Assert.True(caption.Length <= 1024);
        Assert.False(json.RootElement.TryGetProperty("parse_mode", out _));
    }

    [Fact]
    public async Task Missing_video_metadata_keeps_the_provisional_caption_without_an_edit()
    {
        using var api = new TelegramApiEmulator();
        using var http = new HttpClient(api);
        var result = await VideoClient(http).SendVideoUrlAsync(9123, new Uri("https://media.example.test/video.mp4"), ProvisionalCaption, default);
        Assert.Equal(88, result.MessageId);
        Assert.Single(api.Requests);
        Assert.DoesNotContain(api.Requests, request => request.Path.EndsWith("/editMessageCaption"));
    }

    [Fact]
    public async Task Caption_edit_failure_keeps_the_confirmed_video_ack_and_never_resends_the_video()
    {
        using var api = new TelegramApiEmulator
        {
            DirectVideoMetadata = JsonSerializer.SerializeToElement(new { width = 1280, height = 720, duration = 12 }),
            FailCaptionUpdate = true
        };
        using var http = new HttpClient(api);
        var logger = new RecordingLogger();
        var result = await VideoClient(http, logger).SendVideoUrlAsync(9123, new Uri("https://media.example.test/video.mp4"), ProvisionalCaption, default);
        Assert.Equal(88, result.MessageId);
        Assert.Single(api.Requests, request => request.Path.EndsWith("/sendVideo"));
        Assert.Single(api.Requests, request => request.Path.EndsWith("/editMessageCaption"));
        var log = Assert.Single(logger.Entries);
        Assert.Contains("caption.update", log);
        Assert.Contains(nameof(HttpRequestException), log);
        Assert.DoesNotContain("private details", log);
    }

    [Theory]
    [InlineData("https://private.example.test/file.mp4?token=synthetic-file-name-secret")]
    [InlineData("C:\\private\\token-file-name.mp4")]
    public async Task Unsafe_response_file_names_do_not_replace_the_safe_original_title(string fileName)
    {
        using var api = new TelegramApiEmulator
        {
            DirectVideoMetadata = JsonSerializer.SerializeToElement(new { width = 1280, height = 720, duration = 12, file_name = fileName })
        };
        using var http = new HttpClient(api);
        await VideoClient(http).SendVideoUrlAsync(9123, new Uri("https://media.example.test/video.mp4"), ProvisionalCaption, default);
        var edit = Assert.Single(api.Requests, request => request.Path.EndsWith("/editMessageCaption"));
        using var json = JsonDocument.Parse(edit.Body);
        var caption = json.RootElement.GetProperty("caption").GetString()!;
        Assert.Contains("Название: original.mp4", caption);
        Assert.DoesNotContain(fileName, caption);
        Assert.True(caption.Length <= 1024);
    }

    private const string ProvisionalCaption = "Название: original.mp4 (имя файла)\nИсточник: https://public.example.test/original\nКачество: разрешение неизвестно\nДлительность: неизвестна";

    private static BotApiClient VideoClient(HttpClient http, ILogger<BotApiClient>? logger = null)
        => new(new ClientFactory(http), new TelegramSecurityOptions("synthetic-metadata-token", "synthetic-webhook", new byte[32]), new ConfigurationBuilder().Build(), logger);

    private sealed class RecordingLogger : ILogger<BotApiClient>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(formatter(state, exception));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        private readonly BotCommandHandler _handler;
        public TelegramApiEmulator Api { get; } = new();

        public Fixture(AccountProfile? profile = null)
        {
            _http = new HttpClient(Api);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VG_TELEGRAM_MINIAPP_URL"] = "https://videograbber.example.test/miniapp/"
            }).Build();
            var bot = new BotApiClient(new ClientFactory(_http), new TelegramSecurityOptions("synthetic-menu-token", "synthetic-webhook", new byte[32]), config);
            var accounts = new UnlinkedAccountStore(profile);
            var media = new BotMediaHandler(null!, null!, null!, null!, accounts, TimeProvider.System, bot, config, null!);
            _handler = new BotCommandHandler(new AccountResolver(), accounts, null!, null!, null!, null!, null!, null!, bot, media, null!, null!, TimeProvider.System, config);
        }

        public Task SendAsync(string text, string chatType = "private")
            => _handler.HandleAsync(new TelegramUpdate(1, JsonSerializer.SerializeToElement(new
            {
                message = new { message_id = 1L, from = new { id = 9123L }, chat = new { id = chatType == "private" ? 9123L : -9123L, type = chatType }, text }
            })), CancellationToken.None);

        public JsonElement LastMessage()
        {
            var last = Api.Requests.Last(request => request.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
            using var json = JsonDocument.Parse(last.Body);
            return json.RootElement.Clone();
        }

        public void Dispose() => _http.Dispose();
    }

    private sealed class ClientFactory(HttpClient http) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => http;
    }

    private sealed class AccountResolver : ITelegramAccountResolver
    {
        public Task<Guid> ResolveAsync(long userId, DateTimeOffset authTime, CancellationToken cancellationToken)
            => Task.FromResult(Guid.Empty);
    }

    private sealed class UnlinkedAccountStore(AccountProfile? profile) : IAccountStore
    {
        public Task<AccountProfile?> ReadAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task<AccountProfile> ResolveAsync(VerifiedIdentity identity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
