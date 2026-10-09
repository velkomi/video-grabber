using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Support;

public sealed class SupportBotService(NpgsqlDataSource database, ITelegramAccountResolver accounts,
    SupportStore store, SupportOptions options, IBotApiClient bot, TimeProvider clock)
{
    public async Task<bool> HandleAsync(JsonElement message, long userId, long chatId, string chatType, string text, CancellationToken ct)
    {
        var trimmed = text.Trim();
        var word = trimmed.Split(' ', 2)[0].Split('@', 2)[0];
        var start = trimmed == "Поддержка" || word.Equals("/support", StringComparison.OrdinalIgnoreCase);
        var cancel = trimmed is "Отмена поддержки" or "/cancel";
        if (!start && !cancel && trimmed.StartsWith('/')) { await CloseAsync(userId, ct); return false; }
        if (!start && !await ActiveAsync(userId, chatId, ct)) return false;
        if (chatType != "private" || userId != chatId)
        {
            await bot.SendMessageAsync(new BotMessage(chatId, "Напишите в поддержку в личном диалоге с ботом."), ct);
            return true;
        }
        if (message.TryGetProperty("from", out var sender) && sender.TryGetProperty("is_bot", out var isBot)
            && isBot.ValueKind == JsonValueKind.True) return true;
        if (cancel)
        {
            await CloseAsync(userId, ct);
            await bot.SendMessageAsync(new BotMessage(chatId, "Обращение отменено. Вернуть основные кнопки: /menu."), ct);
            return true;
        }
        if (!options.Ready)
        {
            await bot.SendMessageAsync(new BotMessage(chatId, "Сейчас не получилось открыть поддержку. Контакты: /contacts."), ct);
            return true;
        }
        var content = start && word.StartsWith('/') && trimmed.Contains(' ') ? trimmed[(trimmed.IndexOf(' ') + 1)..].Trim() : start ? "" : trimmed;
        if (content.Length == 0)
        {
            await using var connection = await database.OpenConnectionAsync(ct);
            await using var open = new NpgsqlCommand("""
                insert into support.bot_modes(user_id,chat_id,expires_at) values(@user,@chat,@expires)
                on conflict(user_id) do update set chat_id=excluded.chat_id,expires_at=excluded.expires_at
                """, connection);
            open.Parameters.AddWithValue("user", userId); open.Parameters.AddWithValue("chat", chatId);
            open.Parameters.AddWithValue("expires", clock.GetUtcNow().AddMinutes(15)); await open.ExecuteNonQueryAsync(ct);
            await bot.SendMessageAsync(new BotMessage(chatId,
                "Опишите вопрос одним сообщением: от 20 до 4000 символов. Пароли и коды входа присылать не нужно. Отмена: /cancel.",
                JsonSerializer.SerializeToElement(new { keyboard = new[] { new[] { new { text = "Отмена поддержки" } } }, resize_keyboard = true })), ct);
            return true;
        }
        try
        {
            var accountId = await accounts.ResolveAsync(userId, clock.GetUtcNow(), ct);
            var contact = "telegram:" + userId.ToString(CultureInfo.InvariantCulture);
            if (message.TryGetProperty("from", out var from) && from.TryGetProperty("username", out var name)
                && name.ValueKind == JsonValueKind.String && Regex.IsMatch(name.GetString() ?? "", "^[A-Za-z][A-Za-z0-9_]{4,31}$", RegexOptions.CultureInvariant))
                contact = "@" + name.GetString();
            var requestId = message.TryGetProperty("message_id", out var id) && id.TryGetInt64(out var messageId)
                ? MessageIdentity(userId, chatId, messageId) : Guid.NewGuid();
            var result = await store.SubmitTrustedTelegramAsync(new SupportRequest(requestId, "other", contact, content), accountId, userId, ct);
            await CloseAsync(userId, ct);
            await bot.SendMessageAsync(new BotMessage(chatId,
                "Обращение сохранено. Номер VG-" + result.TicketId.ToString("N")[..8].ToUpperInvariant() + ". Основное меню: /menu."), ct);
        }
        catch (SupportRequestException ex)
        {
            var reply = ex.Status == 429 ? "Слишком много обращений. Пожалуйста, попробуйте позже."
                : "Опишите вопрос сообщением от 20 до 4000 символов. Отмена: /cancel.";
            await bot.SendMessageAsync(new BotMessage(chatId, reply), ct);
        }
        return true;
    }

    private static Guid MessageIdentity(long user, long chat, long message)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"VideoGrabber.support.telegram:{user}:{chat}:{message}"));
        return new Guid(bytes.AsSpan(0,16));
    }

    private async Task<bool> ActiveAsync(long userId, long chatId, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var query = new NpgsqlCommand("select exists(select 1 from support.bot_modes where user_id=@user and chat_id=@chat and expires_at>@now)", connection);
        query.Parameters.AddWithValue("user", userId); query.Parameters.AddWithValue("chat", chatId); query.Parameters.AddWithValue("now", clock.GetUtcNow());
        return (bool)(await query.ExecuteScalarAsync(ct))!;
    }

    private async Task CloseAsync(long userId, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var close = new NpgsqlCommand("update support.bot_modes set expires_at=@now where user_id=@user", connection);
        close.Parameters.AddWithValue("now", clock.GetUtcNow()); close.Parameters.AddWithValue("user", userId);
        await close.ExecuteNonQueryAsync(ct);
    }
}
