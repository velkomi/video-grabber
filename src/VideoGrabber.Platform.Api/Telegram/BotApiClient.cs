using System.Net.Http.Json;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record BotMessage(long ChatId, string Text, JsonElement? ReplyMarkup = null);
public sealed record BotSentMessage(long ChatId, long MessageId);

public interface IBotApiClient
{
    Task<BotSentMessage> SendMessageAsync(BotMessage message, CancellationToken cancellationToken);
    Task AnswerCallbackAsync(string callbackQueryId, string text, CancellationToken cancellationToken);
    Task<TelegramChatRights> GetRightsAsync(long chatId, long userId, CancellationToken cancellationToken);
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

    public async Task<TelegramChatRights> GetRightsAsync(
        long chatId,
        long userId,
        CancellationToken cancellationToken)
    {
        if (chatId == 0 || userId <= 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        if (!long.TryParse(configuration["VG_TELEGRAM_BOT_USER_ID"], out var botUserId) || botUserId <= 0)
            throw new InvalidOperationException("Telegram bot numeric user ID is required.");
        var chat = await CallAsync("getChat", new { chat_id = chatId }, cancellationToken).ConfigureAwait(false);
        var kind = RequiredString(chat, "type");
        var user = await CallAsync("getChatMember", new { chat_id = chatId, user_id = userId }, cancellationToken)
            .ConfigureAwait(false);
        var botMember = await CallAsync("getChatMember", new { chat_id = chatId, user_id = botUserId }, cancellationToken)
            .ConfigureAwait(false);
        var userCanPublish = CanPublish(kind, chatId, userId, user);
        var botCanPublish = CanPublish(kind, chatId, botUserId, botMember);
        return new TelegramChatRights(userCanPublish, botCanPublish, kind);
    }

    private async Task<JsonElement> CallAsync(string method, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, MethodUri(method))
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await clients.CreateClient("TelegramBotApi")
            .SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<TelegramEnvelope>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Telegram Bot API response was empty.");
        if (!envelope.Ok || envelope.Result.ValueKind != JsonValueKind.Object)
            throw new HttpRequestException("Telegram Bot API rejected " + method + ".");
        return envelope.Result.Clone();
    }

    private static bool CanPublish(string kind, long chatId, long memberId, JsonElement member)
    {
        var status = RequiredString(member, "status");
        if (kind == "private")
            return memberId == chatId
                ? status is "member" or "administrator" or "creator"
                : status is "member" or "administrator" or "creator";
        if (status == "creator") return true;
        if (status != "administrator") return false;
        if (kind == "channel")
            return member.TryGetProperty("can_post_messages", out var canPost)
                && canPost.ValueKind == JsonValueKind.True;
        return kind is "group" or "supergroup";
    }

    private static string RequiredString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
                ? text
                : throw new InvalidDataException("Telegram Bot API field " + name + " is missing.");
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