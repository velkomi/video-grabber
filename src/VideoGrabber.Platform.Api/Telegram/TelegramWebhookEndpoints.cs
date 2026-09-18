using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VideoGrabber.Platform.Api.Telegram;

public static class TelegramWebhookEndpoints
{
    private const int MaxBodyBytes = 1024 * 1024;
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static IEndpointRouteBuilder MapTelegramWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/telegram/webhook", AcceptWebhookAsync);
        return endpoints;
    }

    private static async Task<IResult> AcceptWebhookAsync(
        HttpContext http,
        TelegramSecurityOptions options,
        ITelegramUpdateInbox inbox,
        CancellationToken cancellationToken)
    {
        var suppliedSecret = http.Request.Headers[SecretHeader].ToString();
        if (!FixedTextEquals(options.WebhookSecret, suppliedSecret))
            return Results.Unauthorized();
        if (http.Request.ContentLength is > MaxBodyBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        byte[] body;
        try { body = await ReadLimitedBytesAsync(http.Request.Body, MaxBodyBytes, cancellationToken); }
        catch (PayloadTooLargeException) { return Results.StatusCode(StatusCodes.Status413PayloadTooLarge); }
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("update_id", out var updateIdElement)
                || updateIdElement.ValueKind != JsonValueKind.Number
                || !updateIdElement.TryGetInt64(out var updateId)
                || updateId < 0)
                return Results.BadRequest(new { code = "telegram_update_invalid" });
            try
            {
                var accepted = await inbox.AcceptAsync(
                    new TelegramUpdate(updateId, root.Clone()), cancellationToken);
                return Results.Ok(new { accepted });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { code = "telegram_update_invalid" });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(body);
        }
    }

    private static bool FixedTextEquals(string expected, string actual)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual)) return false;
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        try
        {
            return left.Length == right.Length
                && CryptographicOperations.FixedTimeEquals(left, right);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(left);
            CryptographicOperations.ZeroMemory(right);
        }
    }

    private static async Task<byte[]> ReadLimitedBytesAsync(
        Stream stream,
        int limit,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream(Math.Min(limit, 8192));
        var buffer = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > limit) throw new PayloadTooLargeException();
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }

    private sealed class PayloadTooLargeException : Exception;
}