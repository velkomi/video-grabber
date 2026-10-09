using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Altcha;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Licensing;

public sealed class SupportClientException(int status, string code) : Exception("Support request failed.")
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class SupportApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task<SupportAccepted> SubmitAsync(SupportRequest request, string? accessToken, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        using var challengeRequest = Request(HttpMethod.Get, "/v1/support/challenge", accessToken);
        using var response = await http.SendAsync(challengeRequest, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        if (!response.IsSuccessStatusCode) throw new SupportClientException((int)response.StatusCode, "support_unavailable");
        var json = await ReadBoundedAsync(response.Content, 16384, deadline.Token);
        var challenge = JsonSerializer.Deserialize<Challenge>(json, AltchaJson.SerializerOptions)
            ?? throw new InvalidDataException("Support challenge is unavailable.");
        var parameters = challenge.Parameters;
        if (parameters.Algorithm != "PBKDF2/SHA-256" || parameters.Cost != 5000 || parameters.KeyLength != 32
            || parameters.MemoryCost is not null || parameters.Parallelism is not null || parameters.KeyPrefix.Length != 32)
            throw new InvalidDataException("Unsupported support challenge.");
        var solution = await Task.Run(() => AltchaPow.SolveChallenge(new SolveChallengeOptions { Challenge = challenge }, deadline.Token), deadline.Token);
        var proof = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Payload
        { Challenge = challenge, Solution = solution }, AltchaJson.SerializerOptions));
        using var submit = Request(HttpMethod.Post, "/v1/support/requests", accessToken);
        submit.Content = new StringContent(JsonSerializer.Serialize(request with { Altcha = proof, Website = "" }, Wire), Encoding.UTF8, "application/json");
        using var sent = await http.SendAsync(submit, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        var result = await ReadBoundedAsync(sent.Content, 16384, deadline.Token);
        if (!sent.IsSuccessStatusCode)
        {
            string code = "support_unavailable";
            try { using var error = JsonDocument.Parse(result); code = error.RootElement.GetProperty("code").GetString() ?? code; }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
            throw new SupportClientException((int)sent.StatusCode, code);
        }
        return JsonSerializer.Deserialize<SupportAccepted>(result, Wire) ?? throw new InvalidDataException("Support response is unavailable.");
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-VideoGrabber-Client", "windows");
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (memory.Length + read > limit) throw new InvalidDataException("Support response exceeds the allowed size.");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }
}
