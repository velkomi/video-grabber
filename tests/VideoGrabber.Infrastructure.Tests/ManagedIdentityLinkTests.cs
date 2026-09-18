using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Infrastructure.Licensing;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ManagedIdentityLinkTests
{
    [Fact]
    public async Task Browser_link_binds_flow_to_challenge_and_uses_loopback_pkce()
    {
        var challengeId = Guid.NewGuid();
        var handler = new LinkHandler(challengeId);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://licensing.example.test/") };
        var link = new SystemBrowserSignIn(http, "telegram", async (authorization, cancellationToken) =>
        {
            Assert.Equal("state-link", Query(authorization, "state"));
            var callback = handler.ReturnUri ?? throw new InvalidOperationException();
            using var callbackClient = new HttpClient();
            using var response = await callbackClient.GetAsync(new UriBuilder(callback)
            {
                Query = "code=link-code&state=state-link"
            }.Uri, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }, TimeSpan.FromSeconds(5));

        await link.LinkAsync(challengeId, CancellationToken.None);

        Assert.True(handler.Completed);
        Assert.Equal(challengeId, handler.StartChallenge);
        Assert.Equal(challengeId, handler.CompleteChallenge);
        Assert.Equal("http", handler.ReturnUri!.Scheme);
        Assert.Equal("127.0.0.1", handler.ReturnUri.Host);
        Assert.NotEqual(handler.ClientVerifier, handler.ClientChallenge);
    }

    private sealed class LinkHandler(Guid expectedChallenge) : HttpMessageHandler
    {
        public Uri? ReturnUri { get; private set; }
        public string? ClientChallenge { get; private set; }
        public string? ClientVerifier { get; private set; }
        public Guid StartChallenge { get; private set; }
        public Guid CompleteChallenge { get; private set; }
        public bool Completed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/v1/identities/link/browser/start")
            {
                var begin = await request.Content!.ReadFromJsonAsync<BeginIdentityLink>(cancellationToken: cancellationToken)
                    ?? throw new InvalidDataException();
                Assert.Equal(expectedChallenge, begin.ChallengeId);
                Assert.Equal("telegram", begin.Provider);
                ReturnUri = begin.ReturnUri;
                ClientChallenge = begin.ClientChallenge;
                StartChallenge = begin.ChallengeId;
                return Json(new SignInStart(Guid.NewGuid(),
                    new Uri("https://broker.example.test/authorize?state=state-link"), DateTimeOffset.UtcNow.AddMinutes(5)));
            }
            if (request.RequestUri.AbsolutePath == "/v1/identities/link/browser/complete")
            {
                var complete = await request.Content!.ReadFromJsonAsync<CompleteIdentityLink>(cancellationToken: cancellationToken)
                    ?? throw new InvalidDataException();
                Assert.Equal(expectedChallenge, complete.ChallengeId);
                Assert.Equal("link-code", complete.Code);
                Assert.Equal("state-link", complete.State);
                ClientVerifier = complete.ClientVerifier;
                CompleteChallenge = complete.ChallengeId;
                Completed = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T value)
            => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }

    private static string Query(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name)
                return Uri.UnescapeDataString(parts.Length == 2 ? parts[1] : string.Empty);
        }
        throw new KeyNotFoundException(name);
    }
}