using System.Net.Http.Json;
using System.Text.Json;
using VideoGrabber.Platform.Api.Telegram;

namespace VideoGrabber.Platform.Api.Support;

public sealed class SupportTelegramTransport(IHttpClientFactory clients, TelegramSecurityOptions security,
    SupportOptions options, IConfiguration configuration) : ISupportNotificationTransport
{
    public string Channel => "telegram";
    public bool Ready => options.Ready && options.TelegramOwnerId > 0;

    public async Task<SupportSendResult> SendAsync(SupportNotification item, CancellationToken ct)
    {
        if (!Ready) return new("confirmed_failure", "telegram_not_configured");
        var baseUri = new Uri(configuration["VG_TELEGRAM_API_BASE_URL"] ?? "https://api.telegram.org/");
        if (baseUri.Scheme != "https" && !(baseUri.Scheme == "http" && baseUri.Host is "127.0.0.1" or "localhost"))
            return new("confirmed_failure", "telegram_configuration_invalid");
        var uri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/bot" + security.BotToken + "/sendMessage");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new
            { chat_id = options.TelegramOwnerId, text = SupportNotificationText.Format(item, true), link_preview_options = new { is_disabled = true } })
        };
        try
        {
            using var response = await clients.CreateClient("TelegramBotApi").SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                return status >= 500 ? new("unknown", "telegram_ack_unknown")
                    : new("confirmed_failure", "telegram_http_" + status, status == 429);
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
            var root = json.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True
                && root.TryGetProperty("result", out var result)
                && result.TryGetProperty("message_id", out var messageId) && messageId.TryGetInt64(out var id) && id > 0
                && result.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var returned)
                && returned.TryGetInt64(out var recipient) && recipient == options.TelegramOwnerId)
                return SupportSendResult.Delivered;
            return new("unknown", "telegram_ack_unconfirmed");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or JsonException)
        { return new("unknown", "telegram_ack_unknown"); }
    }
}
