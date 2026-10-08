using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record BotMessage(long ChatId, string Text, JsonElement? ReplyMarkup = null);
public sealed record BotSentMessage(long ChatId, long MessageId, string? FileId = null);

public interface IBotApiClient
{
    Task<BotSentMessage> SendMessageAsync(BotMessage message, CancellationToken cancellationToken);
    Task AnswerCallbackAsync(string callbackQueryId, string text, CancellationToken cancellationToken);
    Task<BotSentMessage> SendDocumentAsync(long chatId, Stream content, string fileName, CancellationToken cancellationToken);
    Task<Uri?> PublicBotLinkAsync(CancellationToken cancellationToken) => Task.FromResult<Uri?>(null);
    Task<BotSentMessage> SendVideoUrlAsync(long chatId, Uri source, CancellationToken cancellationToken)
        => throw new NotSupportedException("Direct cloud delivery is unavailable.");
    Task<BotSentMessage> SendVideoUrlAsync(long chatId, Uri source, string caption, CancellationToken cancellationToken)
        => throw new NotSupportedException("Direct cloud delivery with a caption is unavailable.");
    Task<TelegramChatRights> GetRightsAsync(long chatId, long userId, CancellationToken cancellationToken);
    Task<Uri> CreateInvoiceAsync(long payerId, string payload, string title, Money amount, int? subscriptionPeriod, CancellationToken cancellationToken);
    Task AnswerPreCheckoutAsync(string queryId, bool accepted, string? error, CancellationToken cancellationToken);
    Task<bool> RefundStarsAsync(long payerId, string telegramChargeId, CancellationToken cancellationToken);
    Task<JsonElement> ReadStarTransactionsAsync(int offset, int limit, CancellationToken cancellationToken);
    Task<bool> SetStarSubscriptionCanceledAsync(long payerId, string firstChargeId, bool canceled, CancellationToken cancellationToken);
}

public sealed class BotApiClient(
    IHttpClientFactory clients,
    TelegramSecurityOptions security,
    IConfiguration configuration,
    ILogger<BotApiClient>? logger = null) : IBotApiClient
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

    private Uri? _publicBotLink;
    private readonly SemaphoreSlim _publicLinkGate = new(1, 1);
    public async Task<Uri?> PublicBotLinkAsync(CancellationToken cancellationToken)
    {
        if (_publicBotLink is not null) return _publicBotLink;
        await _publicLinkGate.WaitAsync(cancellationToken);
        try
        {
            if (_publicBotLink is not null) return _publicBotLink;
            using var request = new HttpRequestMessage(HttpMethod.Get, MethodUri("getMe"));
            using var response = await clients.CreateClient("TelegramBotApi").SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var envelope = await response.Content.ReadFromJsonAsync<TelegramEnvelope>(cancellationToken: cancellationToken);
            if (envelope is not { Ok: true } || !envelope.Result.TryGetProperty("username", out var value)) return null;
            var username = value.GetString();
            if (username is null || !System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z0-9_]{5,32}$")) return null;
            return _publicBotLink = new Uri("https://t.me/" + username);
        }
        finally { _publicLinkGate.Release(); }
    }

    public Task<BotSentMessage> SendVideoUrlAsync(long chatId, Uri source, CancellationToken cancellationToken)
        => SendVideoUrlAsync(chatId, source, string.Empty, cancellationToken);

    public async Task<BotSentMessage> SendVideoUrlAsync(long chatId, Uri source, string caption, CancellationToken cancellationToken)
    {
        if (_baseUri.Scheme != "https" || _baseUri.IdnHost != "api.telegram.org")
            throw new NotSupportedException("Direct fetching requires Telegram cloud Bot API.");
        if (chatId == 0 || source.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(source.UserInfo))
            throw new ArgumentException("Invalid direct video request.");
        ArgumentNullException.ThrowIfNull(caption);
        if (caption.Length > 1024) throw new ArgumentException("Telegram video caption is too long.", nameof(caption));
        using var request = new HttpRequestMessage(HttpMethod.Post, MethodUri("sendVideo"))
        {
            Content = JsonContent.Create(new { chat_id = chatId, video = source.AbsoluteUri, caption, supports_streaming = true })
        };
        using var response = await clients.CreateClient("TelegramBotApi").SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<TelegramEnvelope>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Telegram response was empty.");
        if (!envelope.Ok || !envelope.Result.TryGetProperty("message_id", out var message)
            || !message.TryGetInt64(out var messageId))
            throw new HttpRequestException("Telegram did not confirm direct delivery.");
        // A confirmed send must stay confirmed even if this optional cosmetic
        // update times out, is rejected, or returns malformed metadata.
        try
        {
            var confirmedCaption = ConfirmedVideoCaption(caption, envelope.Result);
            if (confirmedCaption is not null && confirmedCaption != caption)
            {
                using var captionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await CallAsync("editMessageCaption", new
                {
                    chat_id = chatId,
                    message_id = messageId,
                    caption = confirmedCaption
                }, captionTimeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning("caption.update failed: {ExceptionType}", ex.GetType().Name);
        }
        return new BotSentMessage(chatId, messageId);
    }

    private static string? ConfirmedVideoCaption(string caption, JsonElement message)
    {
        if (!message.TryGetProperty("video", out var video) || video.ValueKind != JsonValueKind.Object)
            return null;
        var lines = caption.Length == 0 ? new List<string>() : caption.Split('\n').ToList();
        var changed = false;
        if (TryInteger(video, "width", out var width) && width > 0
            && TryInteger(video, "height", out var height) && height > 0)
        {
            SetField("Качество:", $"{width} × {height}");
            changed = true;
        }
        if (TryInteger(video, "duration", out var seconds) && seconds >= 0)
        {
            var duration = seconds >= 3600
                ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
                : $"{seconds / 60:00}:{seconds % 60:00}";
            SetField("Длительность:", duration);
            changed = true;
        }
        if (video.TryGetProperty("file_name", out var fileName) && fileName.ValueKind == JsonValueKind.String
            && SafeVideoFileName(fileName.GetString()) is { } safeName)
        {
            SetField("Название:", safeName + " (имя файла)");
            changed = true;
        }
        if (!changed) return null;
        if (TryInteger(message, "date", out var sentAt) && sentAt > 0)
            SetField("Отправлено:", DateTimeOffset.FromUnixTimeSeconds(sentAt).ToString("dd.MM.yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture));
        var updated = string.Join('\n', lines);
        return updated.Length <= 1024 ? updated : null;

        void SetField(string prefix, string value)
        {
            var index = lines.FindIndex(line => line.StartsWith(prefix, StringComparison.Ordinal));
            if (index >= 0) lines[index] = prefix + " " + value;
            else lines.Add(prefix + " " + value);
        }
    }

    private static bool TryInteger(JsonElement element, string name, out int number)
    {
        number = 0;
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out number);
    }

    private static string? SafeVideoFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160
            || name.IndexOfAny(['/', '\\', '?', '#', ':']) >= 0)
            return null;
        var safe = string.Concat(name.Select(character => char.IsControl(character)
            || char.GetUnicodeCategory(character) == UnicodeCategory.Format ? ' ' : character)).Trim();
        return safe.Length == 0 ? null : safe;
    }

    public async Task<BotSentMessage> SendDocumentAsync(
        long chatId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (chatId == 0) throw new ArgumentException("Telegram chat ID is required.", nameof(chatId));
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id");
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(streamContent, "document", Path.GetFileName(fileName));
        using var request = new HttpRequestMessage(HttpMethod.Post, MethodUri("sendDocument"))
        {
            Content = form
        };
        using var response = await clients.CreateClient("TelegramBotApi")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<TelegramEnvelope>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Telegram Bot API response was empty.");
        if (!envelope.Ok || envelope.Result.ValueKind != JsonValueKind.Object)
            throw new HttpRequestException("Telegram Bot API rejected sendDocument.");
        var result = envelope.Result;
        if (!result.TryGetProperty("message_id", out var messageIdElement)
            || !messageIdElement.TryGetInt64(out var messageId))
            throw new InvalidDataException("Telegram Bot API response has no message_id.");
        var returnedChatId = chatId;
        if (result.TryGetProperty("chat", out var chat)
            && chat.ValueKind == JsonValueKind.Object
            && chat.TryGetProperty("id", out var id)
            && id.TryGetInt64(out var parsedChatId))
            returnedChatId = parsedChatId;
        string? fileId = null;
        if (result.TryGetProperty("document", out var document)
            && document.ValueKind == JsonValueKind.Object
            && document.TryGetProperty("file_id", out var fileIdElement)
            && fileIdElement.ValueKind == JsonValueKind.String)
            fileId = fileIdElement.GetString();
        return new BotSentMessage(returnedChatId, messageId, fileId);
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

    public async Task<Uri> CreateInvoiceAsync(
        long payerId,
        string payload,
        string title,
        Money amount,
        int? subscriptionPeriod,
        CancellationToken cancellationToken)
    {
        if (payerId <= 0) throw new ArgumentOutOfRangeException(nameof(payerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (payload.Length > 128 || title.Length > 32)
            throw new ArgumentException("Stars invoice text exceeds Telegram limits.");
        if (amount.Currency != "XTR" || amount.MinorUnits <= 0)
            throw new ArgumentException("Stars invoices require positive XTR amount.");
        if (subscriptionPeriod is not null && subscriptionPeriod != 2592000)
            throw new ArgumentException("Stars subscription period must be 2592000 seconds.");
        var requestPayload = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["description"] = "VideoGrabber digital service",
            ["payload"] = payload,
            ["provider_token"] = string.Empty,
            ["currency"] = "XTR",
            ["prices"] = new[] { new { label = title, amount = amount.MinorUnits } }
        };
        if (subscriptionPeriod is int period)
            requestPayload["subscription_period"] = period;
        var result = await CallAnyAsync("createInvoiceLink", requestPayload, cancellationToken)
            .ConfigureAwait(false);
        if (result.ValueKind != JsonValueKind.String
            || !Uri.TryCreate(result.GetString(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Telegram invoice link is invalid.");
        return uri;
    }

    public async Task AnswerPreCheckoutAsync(
        string queryId,
        bool accepted,
        string? error,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        var result = await CallAnyAsync(
            "answerPreCheckoutQuery",
            accepted
                ? new { pre_checkout_query_id = queryId, ok = true }
                : new { pre_checkout_query_id = queryId, ok = false, error_message = string.IsNullOrWhiteSpace(error) ? "Payment validation failed" : error },
            cancellationToken).ConfigureAwait(false);
        if (result.ValueKind is not JsonValueKind.True)
            throw new HttpRequestException("Telegram rejected pre-checkout answer.");
    }

    public async Task<bool> RefundStarsAsync(
        long payerId,
        string telegramChargeId,
        CancellationToken cancellationToken)
    {
        if (payerId <= 0) throw new ArgumentOutOfRangeException(nameof(payerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(telegramChargeId);
        var result = await CallAnyAsync(
            "refundStarPayment",
            new { user_id = payerId, telegram_payment_charge_id = telegramChargeId },
            cancellationToken).ConfigureAwait(false);
        return result.ValueKind == JsonValueKind.True;
    }

    public async Task<JsonElement> ReadStarTransactionsAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var result = await CallAnyAsync(
            "getStarTransactions", new { offset, limit }, cancellationToken).ConfigureAwait(false);
        if (result.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Telegram StarTransactions response is invalid.");
        return result;
    }

    public async Task<bool> SetStarSubscriptionCanceledAsync(
        long payerId,
        string firstChargeId,
        bool canceled,
        CancellationToken cancellationToken)
    {
        if (payerId <= 0) throw new ArgumentOutOfRangeException(nameof(payerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(firstChargeId);
        var result = await CallAnyAsync(
            "editUserStarSubscription",
            new { user_id = payerId, telegram_payment_charge_id = firstChargeId, is_canceled = canceled },
            cancellationToken).ConfigureAwait(false);
        return result.ValueKind == JsonValueKind.True;
    }

    private async Task<JsonElement> CallAnyAsync(
        string method,
        object payload,
        CancellationToken cancellationToken)
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
        if (!envelope.Ok)
            throw new HttpRequestException("Telegram Bot API rejected " + method + ".");
        return envelope.Result.Clone();
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
