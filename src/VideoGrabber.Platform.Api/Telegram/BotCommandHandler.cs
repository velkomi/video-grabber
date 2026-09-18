using System.Globalization;
using System.Text;
using System.Text.Json;
using VideoGrabber.Platform.Api.Admin;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed class BotCommandHandler(
    ITelegramAccountResolver telegramAccounts,
    IAccountStore accounts,
    GrantStore grants,
    DeviceStore devices,
    DestinationService destinations,
    AdminService admin,
    BotCallbackStore callbacks,
    IBotApiClient bot,
    TimeProvider clock,
    IConfiguration configuration)
{
    public async Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        var root = update.Body;
        if (root.ValueKind != JsonValueKind.Object) return;
        if (root.TryGetProperty("callback_query", out var callback)
            && callback.ValueKind == JsonValueKind.Object)
        {
            await HandleCallbackAsync(callback, cancellationToken);
            return;
        }
        if (!root.TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.Object)
            return;
        await HandleMessageAsync(message, cancellationToken);
    }

    private async Task HandleMessageAsync(JsonElement message, CancellationToken cancellationToken)
    {
        if (!TrySender(message, out var userId)
            || !TryChat(message, out var chatId, out var chatType)
            || !TryText(message, out var text))
            return;
        var accountId = await telegramAccounts.ResolveAsync(userId, clock.GetUtcNow(), cancellationToken);
        var (command, args) = ParseCommand(text);
        if (command.Length == 0) return;

        if (command is "/start" or "/help")
        {
            await bot.SendMessageAsync(new BotMessage(chatId, HelpText()), cancellationToken);
            return;
        }

        if (command is "/account" or "/balance" or "/link" or "/devices" or "/destinations" or "/admin")
        {
            if (!string.Equals(chatType, "private", StringComparison.OrdinalIgnoreCase))
            {
                await bot.SendMessageAsync(new BotMessage(chatId, PrivateChatText()), cancellationToken);
                return;
            }
        }

        switch (command)
        {
            case "/account":
                await SendAccountAsync(chatId, accountId, cancellationToken);
                return;
            case "/balance":
                await SendBalanceAsync(chatId, accountId, cancellationToken);
                return;
            case "/link":
                await bot.SendMessageAsync(new BotMessage(chatId,
                    "Привязанные способы входа управляются в Mini App или Desktop → Аккаунт. Совпадение email аккаунты не объединяет.",
                    MiniAppMarkup()), cancellationToken);
                return;
            case "/devices":
                await SendDevicesAsync(chatId, accountId, cancellationToken);
                return;
            case "/destinations":
                await SendDestinationsAsync(chatId, accountId, cancellationToken);
                return;
            case "/admin":
                await HandleAdminAsync(chatId, accountId, args, cancellationToken);
                return;
            default:
                await bot.SendMessageAsync(new BotMessage(chatId,
                    "Неизвестная команда. Используйте /help."), cancellationToken);
                return;
        }
    }

    private async Task HandleCallbackAsync(JsonElement callback, CancellationToken cancellationToken)
    {
        if (!callback.TryGetProperty("id", out var callbackIdElement)
            || callbackIdElement.ValueKind != JsonValueKind.String
            || callbackIdElement.GetString() is not { Length: > 0 } callbackId
            || !callback.TryGetProperty("from", out var from)
            || !TryLong(from, "id", out var userId)
            || userId <= 0
            || !callback.TryGetProperty("data", out var dataElement)
            || dataElement.ValueKind != JsonValueKind.String
            || dataElement.GetString() is not { Length: > 0 } token)
            return;
        var accountId = await telegramAccounts.ResolveAsync(userId, clock.GetUtcNow(), cancellationToken);
        var grant = await callbacks.ConsumeAsync(accountId, token, cancellationToken);
        if (grant is null)
        {
            await bot.AnswerCallbackAsync(callbackId,
                "Кнопка устарела, уже использована или принадлежит другому аккаунту.", cancellationToken);
            return;
        }
        switch (grant.Action)
        {
            case "account:refresh":
                await bot.AnswerCallbackAsync(callbackId, "Данные аккаунта обновлены.", cancellationToken);
                return;
            default:
                await bot.AnswerCallbackAsync(callbackId, "Действие больше недоступно.", cancellationToken);
                return;
        }
    }

    private async Task SendAccountAsync(long chatId, Guid accountId, CancellationToken cancellationToken)
    {
        var profile = await accounts.ReadAsync(accountId, cancellationToken)
            ?? throw new InvalidDataException("Telegram account points to a missing profile.");
        var access = await grants.EvaluateAsync(accountId, cancellationToken);
        var refresh = await callbacks.CreateAsync(accountId, "account:refresh", null, cancellationToken);
        var text = $"Аккаунт\nРоль: {profile.Role}\n" +
                   $"Способы входа: {(profile.LinkedProviders.Length == 0 ? "—" : string.Join(", ", profile.LinkedProviders))}\n" +
                   $"Скачивания: {(access.Unlimited ? "безлимит" : access.RemainingDownloads.ToString(CultureInfo.InvariantCulture))}\n" +
                   $"Доступ: {access.Reason}";
        await bot.SendMessageAsync(new BotMessage(chatId, text, AccountMarkup(refresh)), cancellationToken);
    }

    private async Task SendBalanceAsync(long chatId, Guid accountId, CancellationToken cancellationToken)
    {
        var access = await grants.EvaluateAsync(accountId, cancellationToken);
        var balance = access.Unlimited ? "безлимит" : access.RemainingDownloads.ToString(CultureInfo.InvariantCulture);
        var until = access.ValidUntil?.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture) ?? "—";
        await bot.SendMessageAsync(new BotMessage(chatId,
            $"Баланс: {balance} загрузок. Доступ до: {until}. Причина: {access.Reason}."), cancellationToken);
    }

    private async Task SendDevicesAsync(long chatId, Guid accountId, CancellationToken cancellationToken)
    {
        var rows = await devices.ListAsync(accountId, cancellationToken);
        var active = rows.Where(x => !x.Revoked).ToArray();
        var text = active.Length == 0
            ? "Активных компьютеров нет."
            : "Компьютеры:\n" + string.Join("\n", active.Select(x => $"• {x.Name} — {x.DeviceId:D}"));
        await bot.SendMessageAsync(new BotMessage(chatId, text), cancellationToken);
    }

    private async Task SendDestinationsAsync(long chatId, Guid accountId, CancellationToken cancellationToken)
    {
        var rows = await destinations.ListAsync(accountId, cancellationToken);
        var active = rows.Where(x => !x.Revoked).ToArray();
        var text = active.Length == 0
            ? "Проверенных получателей пока нет. Добавьте получателя в Mini App."
            : "Проверенные получатели:\n" + string.Join("\n", active.Select(x => $"• {x.Kind} {x.ChatId}"));
        await bot.SendMessageAsync(new BotMessage(chatId, text, MiniAppMarkup()), cancellationToken);
    }
    private async Task HandleAdminAsync(
        long chatId,
        Guid actorId,
        string args,
        CancellationToken cancellationToken)
    {
        if (!await admin.IsAdminAsync(actorId, cancellationToken))
        {
            await bot.SendMessageAsync(new BotMessage(chatId,
                "Раздел /admin недоступен для этого аккаунта."), cancellationToken);
            return;
        }
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 && string.Equals(parts[0], "lookup", StringComparison.OrdinalIgnoreCase))
        {
            var identity = string.Join(' ', parts.Skip(1));
            var found = await admin.SearchAsync(actorId, identity, cancellationToken);
            var text = found.Count == 0 ? "Аккаунт не найден."
                : string.Join("\n", found.Select(x => $"{x.AccountId:D} • {x.Role} • blocked={x.Blocked}"));
            await bot.SendMessageAsync(new BotMessage(chatId, text), cancellationToken);
            return;
        }
        if (parts.Length == 2
            && string.Equals(parts[0], "audit", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(parts[1], out var targetId))
        {
            var entries = await admin.ReadAuditAsync(actorId, targetId, cancellationToken);
            var text = entries.Count == 0 ? "Аудит пуст."
                : string.Join("\n", entries.Take(10).Select(x => $"{x.CreatedAt:u} {x.EventType}"));
            await bot.SendMessageAsync(new BotMessage(chatId, text), cancellationToken);
            return;
        }

        var token = await callbacks.CreateAsync(actorId, "admin:console", null, cancellationToken);
        var url = AdminLink(token);
        var textBody = "Admin: lookup и audit можно выполнять из бота. " +
                       "gift/revoke/block/unblock/reset требуют fresh MFA и выполняются только в защищённой web-консоли.";
        await bot.SendMessageAsync(new BotMessage(chatId, textBody, UrlMarkup("Открыть admin + MFA", url)), cancellationToken);
    }

    private JsonElement AccountMarkup(string refreshToken)
    {
        var rows = new List<object>
        {
            new[] { new { text = "Обновить", callback_data = refreshToken } }
        };
        if (TryMiniAppUrl(out var miniApp))
            rows.Add(new[] { new { text = "Mini App", web_app = new { url = miniApp.AbsoluteUri } } });
        return JsonSerializer.SerializeToElement(new { inline_keyboard = rows });
    }

    private JsonElement? MiniAppMarkup()
        => TryMiniAppUrl(out var uri)
            ? UrlMarkup("Открыть Mini App", uri)
            : null;

    private static JsonElement UrlMarkup(string label, Uri url)
        => JsonSerializer.SerializeToElement(new
        {
            inline_keyboard = new[] { new[] { new { text = label, url = url.AbsoluteUri } } }
        });

    private bool TryMiniAppUrl(out Uri uri)
        => TryHttpsUrl(configuration["VG_TELEGRAM_MINIAPP_URL"], "/miniapp/", out uri);

    private Uri AdminLink(string token)
    {
        if (!TryHttpsUrl(configuration["VG_PLATFORM_PUBLIC_URL"], "/", out var baseUri))
            baseUri = new Uri("https://platform.invalid/");
        return new Uri(baseUri, "v1/telegram/admin-links/" + Uri.EscapeDataString(token));
    }

    private static bool TryHttpsUrl(string? configured, string fallbackPath, out Uri uri)
    {
        if (Uri.TryCreate(configured, UriKind.Absolute, out var candidate)
            && candidate.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(candidate.UserInfo))
        {
            uri = candidate;
            return true;
        }
        uri = new Uri("https://platform.invalid" + fallbackPath);
        return false;
    }

    private string PrivateChatText()
    {
        var username = configuration["VG_TELEGRAM_BOT_USERNAME"];
        if (!string.IsNullOrWhiteSpace(username)
            && username.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_'))
            return $"Личные данные здесь не показываются. Откройте личный чат: https://t.me/{username}?start=account";
        return "Личные данные здесь не показываются. Откройте личный чат с ботом и повторите команду.";
    }

    private static string HelpText()
        => "Команды: /account, /balance, /link, /devices, /destinations, /help. " +
           "Media-команды появятся только после подключения worker в P5. /admin доступен только owner_admin.";

    private static (string Command, string Args) ParseCommand(string text)
    {
        text = text.Trim();
        if (!text.StartsWith("/", StringComparison.Ordinal)) return (string.Empty, string.Empty);
        var space = text.IndexOf(' ');
        var command = space < 0 ? text : text[..space];
        var at = command.IndexOf('@');
        if (at > 0) command = command[..at];
        var args = space < 0 ? string.Empty : text[(space + 1)..].Trim();
        return (command.ToLowerInvariant(), args);
    }

    private static bool TrySender(JsonElement message, out long userId)
    {
        userId = 0;
        return message.TryGetProperty("from", out var from) && TryLong(from, "id", out userId) && userId > 0;
    }

    private static bool TryChat(JsonElement message, out long chatId, out string type)
    {
        chatId = 0;
        type = string.Empty;
        if (!message.TryGetProperty("chat", out var chat) || chat.ValueKind != JsonValueKind.Object) return false;
        if (!TryLong(chat, "id", out chatId) || chatId == 0) return false;
        if (!chat.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String) return false;
        type = typeElement.GetString() ?? string.Empty;
        return type.Length > 0;
    }

    private static bool TryText(JsonElement message, out string text)
    {
        text = string.Empty;
        if (!message.TryGetProperty("text", out var element) || element.ValueKind != JsonValueKind.String) return false;
        text = element.GetString() ?? string.Empty;
        return text.Length > 0;
    }

    private static bool TryLong(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value);
    }
}