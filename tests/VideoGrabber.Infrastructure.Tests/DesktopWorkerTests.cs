using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Infrastructure.Licensing;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DesktopWorkerTests
{
    [Fact]
    public async Task Poll_signs_fresh_device_challenge_and_rejects_foreign_scope()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceId = Guid.NewGuid();
        var handler = new DesktopHandler(deviceId, key, foreignLease: false);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") };
        var client = new DesktopWorkerClient(
            http, () => "access-token",
            nonce => key.SignData(Encoding.UTF8.GetBytes(nonce),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        var lease = await client.PollAsync(deviceId, CancellationToken.None);

        Assert.NotNull(lease);
        Assert.Equal(deviceId, lease!.Work.DeviceId);
        Assert.True(handler.ProofVerified);

        var foreignHandler = new DesktopHandler(deviceId, key, foreignLease: true);
        using var foreignHttp = new HttpClient(foreignHandler)
        { BaseAddress = new Uri("https://platform.test/") };
        var foreignClient = new DesktopWorkerClient(
            foreignHttp, () => "access-token",
            nonce => key.SignData(Encoding.UTF8.GetBytes(nonce),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => foreignClient.PollAsync(deviceId, CancellationToken.None));
    }

    [Fact]
    public async Task Upload_streams_selected_file_without_sending_local_path()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceId = Guid.NewGuid();
        var handler = new DesktopHandler(deviceId, key, foreignLease: false);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") };
        var client = new DesktopWorkerClient(
            http, () => "access-token",
            nonce => key.SignData(Encoding.UTF8.GetBytes(nonce),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var lease = (await client.PollAsync(deviceId, CancellationToken.None))!;
        var root = Path.Combine(Path.GetTempPath(), "vg-desktop-client-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var localPath = Path.Combine(root, "SECRET_LOCAL_PATH_marker.txt");
        await File.WriteAllTextAsync(localPath, "hello desktop worker");
        try
        {
            var artifact = await client.UploadAsync(
                lease, localPath, CancellationToken.None);
            Assert.Equal("text/plain", artifact.MediaType);
            Assert.Equal("hello desktop worker", Encoding.UTF8.GetString(handler.UploadedBytes!));
            Assert.DoesNotContain(
                "SECRET_LOCAL_PATH_marker", handler.TicketJson ?? string.Empty,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                localPath, handler.TicketJson ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                localPath, handler.UploadRequestPath ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);

            var completed = await client.CompleteAsync(
                lease, artifact, CancellationToken.None);
            Assert.Equal("completed", completed.State);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task Unsupported_local_path_type_is_rejected_before_upload_ticket()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceId = Guid.NewGuid();
        var handler = new DesktopHandler(deviceId, key, foreignLease: false);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") };
        var client = new DesktopWorkerClient(
            http, () => "access-token",
            nonce => key.SignData(Encoding.UTF8.GetBytes(nonce),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var lease = (await client.PollAsync(deviceId, CancellationToken.None))!;
        var file = Path.Combine(Path.GetTempPath(), "vg-shell-" + Guid.NewGuid().ToString("N") + ".cmd");
        await File.WriteAllTextAsync(file, "echo should-not-run");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => client.UploadAsync(lease, file, CancellationToken.None));
            Assert.Null(handler.TicketJson);
        }
        finally { try { File.Delete(file); } catch { } }
    }

    private sealed class DesktopHandler(
        Guid expectedDevice,
        ECDsa key,
        bool foreignLease) : HttpMessageHandler
    {
        private const string Nonce = "desktop-challenge-nonce";
        public bool ProofVerified { get; private set; }
        public string? TicketJson { get; private set; }
        public byte[]? UploadedBytes { get; private set; }
        public string? UploadRequestPath { get; private set; }
        private AttemptLease? _lease;
        private readonly Guid _uploadId = Guid.NewGuid();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-token", request.Headers.Authorization?.Parameter);
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/challenge", StringComparison.Ordinal))
                return Json(new DeviceChallenge(
                    expectedDevice, Nonce, DateTimeOffset.UtcNow.AddMinutes(1)));

            if (path.EndsWith("/claim", StringComparison.Ordinal))
            {
                var proof = await request.Content!.ReadFromJsonAsync<DeviceLeaseProof>(
                    cancellationToken: cancellationToken);
                var signature = Convert.FromBase64String(proof!.Signature);
                ProofVerified = proof.Nonce == Nonce
                    && key.VerifyData(
                        Encoding.UTF8.GetBytes(Nonce), signature,
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                var device = foreignLease ? Guid.NewGuid() : expectedDevice;
                var work = new CreateJob(
                    Guid.NewGuid(), new string('a', 64), "download",
                    "desktop_worker", device, "src_test", "720p", [], null, null);
                _lease = new AttemptLease(
                    Guid.NewGuid(), Guid.NewGuid(), 1,
                    DateTimeOffset.UtcNow.AddMinutes(1), "capability", work);
                return Json(_lease);
            }

            if (path.EndsWith("/uploads", StringComparison.Ordinal)
                && request.Method == HttpMethod.Post)
            {
                TicketJson = await request.Content!.ReadAsStringAsync(cancellationToken);
                var ticketRequest = await request.Content.ReadFromJsonAsync<UploadTicketRequest>(
                    cancellationToken: cancellationToken);
                Assert.NotNull(ticketRequest);
                Assert.Equal(_lease!.AttemptId, ticketRequest!.Lease.AttemptId);
                return Json(new UploadTicket(
                    _uploadId, _lease.JobId, _lease.AttemptId, _lease.Fence,
                    1024 * 1024, DateTimeOffset.UtcNow.AddMinutes(5)));
            }

            if (path.EndsWith("/" + _uploadId.ToString("D"), StringComparison.Ordinal)
                && request.Method == HttpMethod.Put)
            {
                UploadRequestPath = request.RequestUri.AbsolutePath;
                UploadedBytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                var sha = Convert.ToHexString(SHA256.HashData(UploadedBytes)).ToLowerInvariant();
                return Json(new ArtifactReceipt(
                    _uploadId, sha, UploadedBytes.LongLength,
                    request.Content.Headers.ContentType!.MediaType!,
                    "desktop-upload-" + _uploadId.ToString("N")));
            }

            if (path.EndsWith("/complete", StringComparison.Ordinal))
            {
                var completion = await request.Content!
                    .ReadFromJsonAsync<DesktopCompletionRequest>(
                        cancellationToken: cancellationToken);
                return Json(new JobView(
                    _lease!.JobId, Guid.NewGuid(), _lease.Work.IntentId,
                    "completed", "desktop_worker",
                    completion!.Artifact.ArtifactId, "verified_output"));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T value)
            => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
