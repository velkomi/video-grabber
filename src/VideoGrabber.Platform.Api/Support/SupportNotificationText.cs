namespace VideoGrabber.Platform.Api.Support;

public sealed record SupportNotification(Guid TicketId, Guid ClaimId, int Attempt,
    string Topic, string Source, DateTimeOffset CreatedAt, SupportPayload Payload);

public sealed record SupportSendResult(string State, string? Code = null, bool Retryable = false, int RetryAfterSeconds = 30)
{
    public static SupportSendResult Delivered { get; } = new("delivered");
}

public interface ISupportNotificationTransport
{
    string Channel { get; }
    bool Ready { get; }
    Task<SupportSendResult> SendAsync(SupportNotification notification, CancellationToken ct);
}

public static class SupportNotificationText
{
    public static string Subject(SupportNotification item) => "VideoGrabber · обращение VG-" + item.TicketId.ToString("N")[..8].ToUpperInvariant();
    public static string Format(SupportNotification item, bool compact)
    {
        var topic = item.Topic switch
        { "sign_in" => "Вход в аккаунт", "download" => "Скачивание", "subscription" => "Тариф и оплата", "suggestion" => "Предложение", _ => "Другой вопрос" };
        var source = item.Source switch { "windows" => "Windows", "telegram" => "Telegram", "miniapp" => "Mini App", _ => "Сайт" };
        var text = item.Payload.Message;
        if (compact && text.Length > 3000)
        {
            var length = char.IsHighSurrogate(text[2999]) ? 2999 : 3000;
            text = text[..length] + "\n…";
        }
        return $"{Subject(item)}\nТема: {topic}\nОткуда: {source}\nКонтакт: {item.Payload.Contact}\n" +
            $"Время: {item.CreatedAt.ToOffset(TimeSpan.FromHours(3)):dd.MM.yyyy HH:mm} МСК\n\n{text}";
    }
}
