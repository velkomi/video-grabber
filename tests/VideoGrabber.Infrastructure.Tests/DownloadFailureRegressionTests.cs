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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_removes_only_empty_workspace_and_keeps_partial_download(bool partial)
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-failed-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var existing = Path.Combine(root, "Existing.mp4");
        File.WriteAllBytes(existing, [7, 11, 13]);
        try
        {
            var result = await new YtDlpDownloader(new PartialFailureRunner(partial), new ToolLocator(root, root))
                .DownloadAsync(new DownloadRequest(new Uri("https://rutube.ru/video/example/"), root, "best"), null, CancellationToken.None);
            Assert.False(result.Success);
            var jobs = Directory.GetDirectories(root, ".vg-job-*");
            if (partial)
            {
                var job = Assert.Single(jobs);
                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(job, "video.mp4.part")));
                Assert.Contains("Рабочие файлы сохранены", result.Details);
            }
            else
            {
                Assert.Empty(jobs);
                Assert.DoesNotContain("Рабочие файлы сохранены", result.Details ?? "");
            }
            Assert.Equal(new byte[] { 7, 11, 13 }, File.ReadAllBytes(existing));
            Assert.DoesNotContain("кабинете школы", result.Details ?? "");
            Assert.Contains("404 сам по себе не подтверждает", result.Details);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Тёмная материя ⧸ сезон 2 ⧸ 7 из 10", "Тёмная материя — сезон 2 — 7 из 10")]
    [InlineData("Lesson / Part 2", "Lesson — Part 2")]
    [InlineData("CON", "_CON")]
    public void Display_name_keeps_title_and_uses_readable_separators(string input, string expected)
        => Assert.Equal(expected, DownloadFileName.DisplayBaseName(input));

    private sealed class PartialFailureRunner(bool partial) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            if (partial) File.WriteAllBytes(Path.Combine(spec.WorkingDirectory!, "video.mp4.part"), [1, 2, 3]);
            return Task.FromResult(new ProcessResult(1, "", "ERROR: [rutube] video: Unable to download options JSON: HTTP Error 404: Not Found"));
        }
    }
}
