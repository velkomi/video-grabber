using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VideoGrabber.Platform.Api.ClientUpdates;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class ClientReleaseEndpointTests
{
    [Fact]
    public async Task Catalog_serves_exact_envelope_anonymously_without_cache()
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        var envelope = JsonSerializer.Serialize(new
        {
            keyId = "test-key",
            payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"schemaVersion\":1}")),
            signature = Convert.ToBase64String(new byte[256])
        });
        await File.WriteAllTextAsync(fixture.ManifestPath, envelope);
        var response = await fixture.Client.GetAsync("/v1/client-release/preview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(envelope, await response.Content.ReadAsStringAsync());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Missing_catalog_returns_404()
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/v1/client-release/preview")).StatusCode);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"keyId\":\"test\",\"payload\":\"!\",\"signature\":\"!\"}")]
    public async Task Bad_catalog_returns_503(string contents)
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        await File.WriteAllTextAsync(fixture.ManifestPath, contents);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await fixture.Client.GetAsync("/v1/client-release/preview")).StatusCode);
    }

    [Fact]
    public async Task Oversize_catalog_returns_503()
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        await File.WriteAllTextAsync(fixture.ManifestPath, new string(' ', 65537));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await fixture.Client.GetAsync("/v1/client-release/preview")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_envelope_fields_return_503()
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"schemaVersion\":1}"));
        var signature = Convert.ToBase64String(new byte[256]);
        await File.WriteAllTextAsync(fixture.ManifestPath,
            $"{{\"keyId\":\"first\",\"keyId\":\"second\",\"payload\":\"{payload}\",\"signature\":\"{signature}\"}}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await fixture.Client.GetAsync("/v1/client-release/preview")).StatusCode);
    }

    [Fact]
    public async Task Immutable_setup_serves_requested_version_and_range()
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        var versionFolder = Path.Combine(fixture.ReleasesPath, "0.1.0-preview.62");
        Directory.CreateDirectory(versionFolder);
        await File.WriteAllBytesAsync(Path.Combine(versionFolder, "VideoGrabber-Setup.exe"), [1, 2, 3, 4]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/download/windows/releases/0.1.0-preview.62/setup");
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(1, 2);
        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(new byte[] { 2, 3 }, await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.Headers.CacheControl?.Extensions.Any(x => x.Name == "immutable"));
        Assert.Equal("VideoGrabber-Setup-0.1.0-preview.62.exe", response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("01.1.0")]
    [InlineData("0.1.0-preview.01")]
    [InlineData("..%5Coutside")]
    [InlineData("0.1.0%2Foutside")]
    public async Task Invalid_version_does_not_serve_any_file(string version)
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/download/windows/releases/{version}/setup")).StatusCode);
    }

    [Fact]
    public async Task Missing_immutable_setup_returns_404()
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/download/windows/releases/0.1.0/setup")).StatusCode);
    }

    [Fact]
    public async Task Linked_version_directory_cannot_expose_an_outside_installer()
    {
        await using var fixture = await ReleaseFileFixture.StartAsync();
        Directory.CreateDirectory(fixture.ReleasesPath);
        var outside = Path.Combine(Path.GetDirectoryName(fixture.ReleasesPath)!, "outside");
        Directory.CreateDirectory(outside);
        await File.WriteAllBytesAsync(Path.Combine(outside, "VideoGrabber-Setup.exe"), [9, 8, 7]);
        var link = Path.Combine(fixture.ReleasesPath, "0.1.0-preview.62");
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, outside })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
        }
        else
            Directory.CreateSymbolicLink(link, outside);
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/download/windows/releases/0.1.0-preview.62/setup")).StatusCode);
            Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(Path.Combine(outside, "VideoGrabber-Setup.exe")));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    private sealed class ReleaseFileFixture : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly string _directory;
        public string ManifestPath { get; }
        public string ReleasesPath { get; }
        public HttpClient Client { get; }

        private ReleaseFileFixture(WebApplication app, string directory, string address)
        {
            _app = app;
            _directory = directory;
            ManifestPath = Path.Combine(directory, "client-release.json");
            ReleasesPath = Path.Combine(directory, "releases");
            Client = new HttpClient { BaseAddress = new Uri(address) };
        }

        public static async Task<ReleaseFileFixture> StartAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VideoGrabber-ReleaseTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VG_CLIENT_RELEASE_MANIFEST_PATH"] = Path.Combine(directory, "client-release.json"),
                ["VG_WINDOWS_RELEASES_PATH"] = Path.Combine(directory, "releases")
            });
            var app = builder.Build();
            app.MapClientReleaseEndpoints();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new ReleaseFileFixture(app, directory, address);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
            Directory.Delete(_directory, true);
        }
    }
}
