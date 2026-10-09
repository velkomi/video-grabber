using System.Net;
using System.Text;
using System.Text.Json;
using Altcha;
using VideoGrabber.Infrastructure.Licensing;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class SupportApiClientTests
{
    [Fact]
    public async Task Native_client_keeps_identity_and_sends_long_unicode_as_bounded_utf8_json()
    {
        var handler = new SupportHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://support.example.test/") };
        var request = new SupportRequest(Guid.NewGuid(), "download", "client@example.test", new string('Ж',4000));
        var client = new SupportApiClient(http);
        var result = await client.SubmitAsync(request, "synthetic-access-token", CancellationToken.None);
        Assert.Equal(handler.Ticket, result.TicketId);
        Assert.Equal(request.RequestId, handler.RequestId);
        Assert.True(handler.Bytes < 16384);
        Assert.Equal("windows", handler.Source);
    }

    [Fact]
    public async Task Native_client_rejects_expensive_unexpected_kdf_before_solving()
    {
        using var http = new HttpClient(new SupportHandler { Expensive = true }) { BaseAddress = new Uri("https://support.example.test/") };
        await Assert.ThrowsAsync<InvalidDataException>(() => new SupportApiClient(http).SubmitAsync(
            new SupportRequest(Guid.NewGuid(),"other","client@example.test","Это тестовое сообщение поддержки."), null, CancellationToken.None));
    }

    private sealed class SupportHandler : HttpMessageHandler
    {
        public Guid Ticket { get; } = Guid.NewGuid();
        public Guid RequestId { get; private set; }
        public int Bytes { get; private set; }
        public string? Source { get; private set; }
        public bool Expensive { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/v1/support/challenge")
            {
                var challenge = AltchaPow.CreateChallenge(new CreateChallengeOptions
                { Algorithm="PBKDF2/SHA-256",Cost=Expensive?1000000:5000,Counter=60,ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(5),HmacSignatureSecret="synthetic-signing-key",HmacKeySignatureSecret="synthetic-key-signature" });
                return Json(HttpStatusCode.OK,JsonSerializer.Serialize(challenge,AltchaJson.SerializerOptions));
            }
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            Bytes = bytes.Length;
            using var json = JsonDocument.Parse(bytes);
            RequestId = json.RootElement.GetProperty("requestId").GetGuid();
            Source = request.Headers.GetValues("X-VideoGrabber-Client").Single();
            Assert.NotEmpty(json.RootElement.GetProperty("altcha").GetString()!);
            return Json(HttpStatusCode.Created,JsonSerializer.Serialize(new {ticketId=Ticket,status="accepted"}));
        }
        private static HttpResponseMessage Json(HttpStatusCode status,string body)
            => new(status) {Content=new StringContent(body,Encoding.UTF8,"application/json")};
    }
}
