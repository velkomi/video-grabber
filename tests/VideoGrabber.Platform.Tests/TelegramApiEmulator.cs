using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace VideoGrabber.Platform.Tests;

public sealed record TelegramApiRequest(string Method, string Path, string Body);

public sealed class TelegramApiEmulator : HttpMessageHandler
{
    public const long BotUserId = 9990001L;
    private readonly ConcurrentQueue<TelegramApiRequest> _requests = new();
    private readonly ConcurrentDictionary<long, RightsState> _rights = new();
    public IReadOnlyCollection<TelegramApiRequest> Requests => _requests.ToArray();
    public bool LoseNextDocumentAck { get; set; }

    public void SetRights(long chatId, long userId, bool userCanPublish, bool botCanPublish, string kind)
    {
        if (chatId == 0 || userId <= 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        if (kind is not ("private" or "channel" or "group" or "supergroup"))
            throw new ArgumentException("Unsupported chat kind.", nameof(kind));
        _rights[chatId] = new(userId, userCanPublish, botCanPublish, kind);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        _requests.Enqueue(new TelegramApiRequest(request.Method.Method, path, body));
        if (path.EndsWith("/getChat", StringComparison.Ordinal))
            return HandleGetChat(body);
        if (path.EndsWith("/getChatMember", StringComparison.Ordinal))
            return HandleGetChatMember(body);
        if (path.EndsWith("/answerCallbackQuery", StringComparison.Ordinal))
            return Json(new { ok = true, result = true });
        if (path.EndsWith("/sendDocument", StringComparison.Ordinal))
        {
            if (LoseNextDocumentAck)
            {
                LoseNextDocumentAck = false;
                throw new HttpRequestException("synthetic lost ACK after accepted upload");
            }
            return Json(new { ok = true, result = new { message_id = 77L, chat = new { id = 1L }, document = new { file_id = "file-77" } } });
        }
        return Json(new { ok = true, result = new { message_id = 1L, chat = new { id = 1L } } });
    }

    private HttpResponseMessage HandleGetChat(string body)
    {
        var chatId = ReadLong(body, "chat_id");
        if (!_rights.TryGetValue(chatId, out var rights)) return NotFound();
        return Json(new { ok = true, result = new { id = chatId, type = rights.Kind } });
    }

    private HttpResponseMessage HandleGetChatMember(string body)
    {
        var chatId = ReadLong(body, "chat_id");
        var memberId = ReadLong(body, "user_id");
        if (!_rights.TryGetValue(chatId, out var rights)) return NotFound();
        var allowed = memberId == rights.UserId ? rights.UserCanPublish
            : memberId == BotUserId && rights.BotCanPublish;
        object member = rights.Kind switch
        {
            "private" => new { status = allowed ? "member" : "left" },
            "channel" => allowed
                ? new { status = "administrator", can_post_messages = true }
                : new { status = "member", can_post_messages = false },
            _ => new { status = allowed ? "administrator" : "member" }
        };
        return Json(new { ok = true, result = member });
    }

    private static long ReadLong(string body, string name)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty(name).GetInt64();
    }

    private static HttpResponseMessage Json(object value)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage NotFound()
        => new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"ok\":false,\"description\":\"chat not found\"}", Encoding.UTF8, "application/json")
        };

    public void Clear()
    {
        while (_requests.TryDequeue(out _)) { }
    }

    private sealed record RightsState(long UserId, bool UserCanPublish, bool BotCanPublish, string Kind);
}