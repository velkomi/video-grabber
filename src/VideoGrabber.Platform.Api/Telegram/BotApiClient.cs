using System.Net.Http.Json;
using System.Text.Json;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record BotMessage(long ChatId, string Text, JsonElement? ReplyMarkup = null);
public sealed record BotSentMessage(long ChatId, long MessageId);

public interface IBotApiClient
{
    Task<BotSentMessage> SendMessageAsync(BotMessage message, CancellationToken cancellationToken);
    Task AnswerCallbackAsync(string callbackQueryId, string text, CancellationToken cancellationToken);
}

public sealed class BotApiClient(
    IHttpClientFactory clients,
    TelegramSecurityOptions security,
    IConfiguration configuration) : IBotApiClient
{
    private readonly Uri _baseUri = ResolveBaseUri(configuration);

    public async Task<BotSentMessage> SendMessageAsync(
        BotMessage message,
        CancellationToken cancellationToken)
    {
        if (message.ChatId == 0) throw new ArgumentException("Telegram chat ID is required.", nameof(message));
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Text);
        if (message.Text.Length > 4096) throw new ArgumentException("Telegram message is too long.", nameof(message));
        var payload = message.ReplyMarkup is { } markup
            ? new Dictionary<string, object?>
            {
                ["chat_id"] = message.ChatId,
                ["text"] = message.Text,
                ["reply_markup"] = markup
            }
            : new Dictionary<string, object?>
            {
                ["chat_id"] = message.ChatId,
                ["text"] = message.Text
            };
        using var request = new HttpRequestMessage(HttpMethod.Post, MethodUri("sendMessage"))
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await clients.CreateClient("TelegramBotApi")
            .SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<TelegramEnvelope>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Telegram Bot API response was empty.");
        if (!envelope.Ok || envelope.Result.ValueKind != JsonValueKind.Object)
            throw new HttpRequestException("Telegram Bot API rejected sendMessage.");
        var result = envelope.Result;
        if (!result.TryGetProperty("message_id", out var messageIdElement)
            || !messageIdElement.TryGetInt64(out var messageId))
            throw new InvalidDataException("Telegram Bot API response has no message_id.");
        var chatId = message.ChatId;
        if (result.TryGetProperty("chat", out var chat)
            && chat.ValueKind == JsonValueKind.Object
            && chat.TryGetProperty("id", out var id)
            && id.TryGetInt64(out var returnedChatId))
            chatId = returnedChatId;
        return new BotSentMessage(chatId, messageId);
    }

    public async Task AnswerCallbackAsync(
        string callbackQueryId,
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackQueryId);
        text ??= string.Empty;
        if (text.Length > 200) text = text[..200];
        using var request = new HttpRequestMessage(HttpMethod.Post, MethodUri("answerCallbackQuery"))
        {
            Content = JsonContent.Create(new
            {
                callback_query_id = callbackQueryId,
                text,
                show_alert = false
            })
        };
        using var response = await clients.CreateClient("TelegramBotApi")
            .SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private Uri MethodUri(string method)
        => new(_baseUri.AbsoluteUri + "bot" + security.BotToken + "/" + method, UriKind.Absolute);

    private static Uri ResolveBaseUri(IConfiguration configuration)
    {
        var raw = configuration["VG_TELEGRAM_API_BASE_URL"];
        if (string.IsNullOrWhiteSpace(raw)) return new Uri("https://api.telegram.org/");
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps
                && !(uri.Scheme == Uri.UriSchemeHttp && uri.Host is "127.0.0.1" or "localhost")))
            throw new InvalidOperationException("Telegram Bot API base URL must be HTTPS or local loopback HTTP.");
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");
    }

    private sealed record TelegramEnvelope(bool Ok, JsonElement Result);
}