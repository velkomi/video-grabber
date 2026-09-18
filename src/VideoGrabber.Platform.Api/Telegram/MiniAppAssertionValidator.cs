using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record TelegramPrincipal(long UserId, DateTimeOffset AuthTime, string AssertionHash);

public interface IMiniAppAssertionValidator
{
    TelegramPrincipal Validate(string initData, DateTimeOffset now);
}

public sealed record TelegramSecurityOptions(
    string BotToken,
    string WebhookSecret,
    byte[] InboxEncryptionKey)
{
    public static TelegramSecurityOptions FromConfiguration(IConfiguration configuration)
    {
        var botToken = configuration["VG_TELEGRAM_BOT_TOKEN"];
        var webhookSecret = configuration["VG_TELEGRAM_WEBHOOK_SECRET"];
        var encodedKey = configuration["VG_TELEGRAM_INBOX_KEY"];
        if (string.IsNullOrWhiteSpace(botToken))
            throw new InvalidOperationException("Telegram bot token is required.");
        if (string.IsNullOrWhiteSpace(webhookSecret))
            throw new InvalidOperationException("Telegram webhook secret is required.");
        if (string.IsNullOrWhiteSpace(encodedKey))
            throw new InvalidOperationException("Telegram inbox encryption key is required.");
        byte[] key;
        try { key = Convert.FromBase64String(encodedKey); }
        catch (FormatException ex) { throw new InvalidOperationException("Telegram inbox key must be Base64.", ex); }
        if (key.Length != 32)
            throw new InvalidOperationException("Telegram inbox encryption key must contain exactly 32 bytes.");
        return new(botToken, webhookSecret, key);
    }
}

public sealed class MiniAppAssertionValidator : IMiniAppAssertionValidator
{
    private const int MaxInitDataBytes = 16 * 1024;
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromSeconds(30);
    private readonly byte[] _botToken;
    private readonly TimeSpan _maxAge;

    public MiniAppAssertionValidator(string botToken, TimeSpan? maxAge = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botToken);
        _botToken = Encoding.UTF8.GetBytes(botToken);
        _maxAge = maxAge ?? TimeSpan.FromMinutes(5);
        if (_maxAge <= TimeSpan.Zero || _maxAge > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(maxAge));
    }

    public TelegramPrincipal Validate(string initData, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(initData)
            || Encoding.UTF8.GetByteCount(initData) > MaxInitDataBytes)
            throw Denied();

        var fields = ParseUnique(initData);
        if (!fields.TryGetValue("hash", out var rawHash)
            || rawHash.Length != 64
            || !fields.TryGetValue("auth_date", out var rawDate)
            || !fields.TryGetValue("user", out var rawUser))
            throw Denied();

        byte[] suppliedHash;
        try { suppliedHash = Convert.FromHexString(rawHash); }
        catch (FormatException) { throw Denied(); }
        if (suppliedHash.Length != 32) throw Denied();

        var checkString = string.Join("\n", fields
            .Where(pair => !string.Equals(pair.Key, "hash", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + pair.Value));
        var secret = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"), _botToken);
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(checkString));
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(expected, suppliedHash))
                throw Denied();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(suppliedHash);
        }

        if (!long.TryParse(rawDate, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            throw Denied();
        DateTimeOffset authTime;
        try { authTime = DateTimeOffset.FromUnixTimeSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { throw Denied(); }
        if (authTime > now + MaxFutureSkew || now - authTime > _maxAge)
            throw Denied();

        long userId;
        try
        {
            using var user = JsonDocument.Parse(rawUser);
            if (user.RootElement.ValueKind != JsonValueKind.Object
                || !user.RootElement.TryGetProperty("id", out var id)
                || id.ValueKind != JsonValueKind.Number
                || !id.TryGetInt64(out userId)
                || userId <= 0)
                throw Denied();
        }
        catch (JsonException) { throw Denied(); }

        var assertionHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(initData))).ToLowerInvariant();
        return new(userId, authTime, assertionHash);
    }

    private static Dictionary<string, string> ParseUnique(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in input.Split('&', StringSplitOptions.None))
        {
            if (pair.Length == 0) throw Denied();
            var separator = pair.IndexOf('=');
            if (separator <= 0) throw Denied();
            var key = Decode(pair[..separator]);
            var value = Decode(pair[(separator + 1)..]);
            if (key.Length == 0 || !result.TryAdd(key, value)) throw Denied();
        }
        return result;
    }

    private static string Decode(string value)
    {
        try { return Uri.UnescapeDataString(value.Replace('+', ' ')); }
        catch (UriFormatException) { throw Denied(); }
    }

    private static UnauthorizedAccessException Denied()
        => new("telegram_assertion_invalid");
}