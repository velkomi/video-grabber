using System.Net.Mail;
using System.Text.RegularExpressions;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Support;

public sealed class SupportOptions(IConfiguration configuration)
{
    public static readonly string[] Topics = ["sign_in", "download", "subscription", "suggestion", "other"];
    public bool Enabled => string.Equals(configuration["VG_SUPPORT_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
    public long TelegramOwnerId => long.TryParse(configuration["VG_SUPPORT_TELEGRAM_OWNER_ID"], out var id) && id > 0 ? id : 0;
    public bool NotificationsEnabled => string.Equals(configuration["VG_SUPPORT_NOTIFICATIONS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
    public bool MailConfigured => !string.IsNullOrWhiteSpace(configuration["VG_SUPPORT_SMTP_HOST"])
        && !string.IsNullOrWhiteSpace(configuration["VG_SUPPORT_SMTP_USERNAME"])
        && !string.IsNullOrWhiteSpace(configuration["VG_SUPPORT_SMTP_PASSWORD"])
        && ValidEmail(configuration["VG_SUPPORT_SMTP_FROM"] ?? "");
    public bool Ready => Enabled && (TelegramOwnerId > 0 || MailConfigured);

    public static SupportRequest Validate(SupportRequest request)
    {
        var topic = request.Topic?.Trim() ?? "";
        var contact = request.Contact?.Trim().Normalize() ?? "";
        var message = request.Message?.Trim().Normalize() ?? "";
        if (request.RequestId == Guid.Empty || !Topics.Contains(topic, StringComparer.Ordinal))
            throw new SupportRequestException(400, "invalid_support_topic");
        if (contact.Length is < 5 or > 254 || contact.Any(char.IsControl)
            || (!ValidEmail(contact) && !Regex.IsMatch(contact, "^@[A-Za-z][A-Za-z0-9_]{4,31}$", RegexOptions.CultureInvariant)))
            throw new SupportRequestException(400, "invalid_support_contact");
        if (message.Length is < 20 or > 4000 || message.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
            throw new SupportRequestException(400, "invalid_support_message");
        if (!string.IsNullOrWhiteSpace(request.Website)) throw new SupportRequestException(403, "support_verification_required");
        return request with { Topic = topic, Contact = contact, Message = message };
    }

    public static bool ValidEmail(string value)
        => value.Length <= 254 && !value.Any(char.IsControl) && MailAddress.TryCreate(value, out var address)
           && string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase)
           && address.Host.Contains('.');
}

public sealed class SupportRequestException(int status, string code, int retryAfterSeconds = 0) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
