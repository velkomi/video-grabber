using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace VideoGrabber.Platform.Tests;

public sealed record YooKassaRequest(
    string Method,
    string Path,
    string? IdempotenceKey,
    bool HasBasicAuthorization,
    string Body);

public sealed class YooKassaEmulator : HttpMessageHandler
{
    private readonly ConcurrentQueue<YooKassaRequest> _requests = new();
    private readonly ConcurrentDictionary<string, JsonElement> _payments = new();
    private readonly ConcurrentDictionary<string, string> _createKeys = new();
    private int _paymentCounter;
    private int _refundCounter;

    public IReadOnlyCollection<YooKassaRequest> Requests
        => _requests.ToArray();

    public string? LastPaymentId { get; private set; }

    public HttpStatusCode? FailNextStatusCode { get; set; }

    public void SetPayment(string id, object value)
    {
        _payments[id] = JsonSerializer.SerializeToElement(value);
    }

    public JsonElement ReadPayment(string id)
        => _payments.TryGetValue(id, out var value)
            ? value.Clone()
            : throw new KeyNotFoundException(id);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        _requests.Enqueue(new YooKassaRequest(
            request.Method.Method,
            path,
            request.Headers.TryGetValues("Idempotence-Key", out var values)
                ? values.SingleOrDefault()
                : null,
            string.Equals(
                request.Headers.Authorization?.Scheme,
                "Basic",
                StringComparison.Ordinal),
            body));

        if (FailNextStatusCode is HttpStatusCode failure)
        {
            FailNextStatusCode = null;
            return new HttpResponseMessage(failure)
            {
                Content = JsonContent(new { type = "error", code = "synthetic" })
            };
        }

        if (request.Method == HttpMethod.Post
            && path.EndsWith("/v3/payments", StringComparison.Ordinal))
        {
            var key = request.Headers.TryGetValues("Idempotence-Key", out var createValues)
                ? createValues.SingleOrDefault()
                : null;
            if (key is { Length: > 0 } && _createKeys.TryGetValue(key, out var knownId)
                && _payments.TryGetValue(knownId, out var knownPayment))
                return Json(knownPayment);
            var created = CreatePayment(body);
            if (key is { Length: > 0 } && LastPaymentId is { Length: > 0 } createdId)
                _createKeys[key] = createdId;
            return created;
        }

        const string paymentPrefix = "/v3/payments/";
        var index = path.IndexOf(paymentPrefix, StringComparison.Ordinal);
        if (request.Method == HttpMethod.Get && index >= 0)
        {
            var id = Uri.UnescapeDataString(path[(index + paymentPrefix.Length)..]);
            return _payments.TryGetValue(id, out var payment)
                ? Json(payment)
                : new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = JsonContent(new { type = "error", code = "not_found" })
                };
        }

        if (request.Method == HttpMethod.Post
            && path.EndsWith("/v3/refunds", StringComparison.Ordinal))
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var id = "yk-ref-" + Interlocked.Increment(ref _refundCounter)
                .ToString("D4");
            return Json(new
            {
                id,
                status = "succeeded",
                amount = root.GetProperty("amount"),
                payment_id = root.GetProperty("payment_id").GetString(),
                created_at = DateTimeOffset.UtcNow.ToString("O")
            });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent(new { type = "error", code = "unknown_path" })
        };
    }

    private HttpResponseMessage CreatePayment(string body)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var id = "yk-pay-" + Interlocked.Increment(ref _paymentCounter)
            .ToString("D4");
        LastPaymentId = id;
        var response = JsonSerializer.SerializeToElement(new
        {
            id,
            status = "pending",
            paid = false,
            test = true,
            amount = root.GetProperty("amount"),
            confirmation = new
            {
                type = "redirect",
                confirmation_url = "https://yookassa.test/confirm/" + id
            },
            created_at = DateTimeOffset.UtcNow.ToString("O"),
            description = root.TryGetProperty("description", out var description)
                ? description.GetString()
                : null,
            metadata = root.GetProperty("metadata"),
            payment_method = new
            {
                type = "bank_card",
                id = "pm-" + id,
                saved = root.TryGetProperty("save_payment_method", out var save)
                    && save.ValueKind == JsonValueKind.True
            }
        });
        _payments[id] = response;
        return Json(response);
    }

    public void Clear()
    {
        while (_requests.TryDequeue(out _)) { }
    }

    private static HttpResponseMessage Json(object value)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent(value)
        };

    private static StringContent JsonContent(object value)
        => new(
            JsonSerializer.Serialize(value),
            Encoding.UTF8,
            "application/json");
}
