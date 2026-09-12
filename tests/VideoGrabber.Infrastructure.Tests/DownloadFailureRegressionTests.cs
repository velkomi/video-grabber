using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadFailureRegressionTests
{
    [Theory]
    [InlineData("Connection to school.example timed out. (connect timeout=25.0)", "NETWORK_TIMEOUT")]
    [InlineData("Failed to resolve 'school.example' (getaddrinfo failed)", "DNS_FAILURE")]
    [InlineData("[SSL: CERTIFICATE_VERIFY_FAILED] certificate verify failed", "TLS_FAILURE")]
    [InlineData("HTTP Error 401: Unauthorized", "LOGIN_REQUIRED")]
    [InlineData("HTTP Error 403: Forbidden", "ACCESS_DENIED")]
    [InlineData("HTTP Error 404: Not Found", "NOT_FOUND")]
    [InlineData("Unsupported URL: https://school.example/course", "UNSUPPORTED_PAGE")]
    [InlineData("Connection reset by peer", "CONNECTION_FAILED")]
    [InlineData("Unexpected transport error", "DOWNLOAD_FAILED")]
    public async Task Failures_are_explained_without_exposing_raw_error_as_title(string error, string code)
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new YtDlpDownloader(new FailedRunner(error), new ToolLocator(root, root))
                .DownloadAsync(new DownloadRequest(new Uri("https://school.example/course"), root, "best"), null, CancellationToken.None);
            Assert.False(result.Success);
            Assert.DoesNotContain("ERROR:", result.Message);
            Assert.Null(result.OutputPath);
            Assert.Contains(code, result.Message);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class FailedRunner(string error) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
            => Task.FromResult(new ProcessResult(1, "", "ERROR: [generic] lesson: " + error));
    }
}
