using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace VideoGrabber.Platform.Tests;

public sealed record TelegramApiRequest(string Method, string Path, string Body);

public sealed class TelegramApiEmulator : HttpMessageHandler
{
    private readonly ConcurrentQueue<TelegramApiRequest> _requests = new();
    public IReadOnlyCollection<TelegramApiRequest> Requests => _requests.ToArray();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new TelegramApiRequest(
            request.Method.Method,
            request.RequestUri?.AbsolutePath ?? string.Empty,
            body));
        var response = JsonSerializer.Serialize(new
        {
            ok = true,
            result = new { message_id = 1L, chat = new { id = 1L } }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }

    public void Clear()
    {
        while (_requests.TryDequeue(out _)) { }
    }
}