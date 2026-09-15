using System.Text.Json;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadFailurePrivacyTests
{
    [Fact]
    public async Task Timeout_reason_is_logged_in_full_mode_without_private_parameters()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-failure-private-" + Guid.NewGuid().ToString("N"));
        using var job = DiagnosticHub.Begin("test.failure");
        const string tail = "view?id=483927155&token=private-query-secret";
        const string error = "ERROR: [generic] " + tail + ": Connection timed out. Cookie: sid=private-cookie; other=private-other";
        try
        {
            var result = await new YtDlpDownloader(new FailedRunner(error), new ToolLocator(root, root))
                .DownloadAsync(new DownloadRequest(new Uri("https://school.example/" + tail), root, "best"), null, CancellationToken.None);
            Assert.Contains("NETWORK_TIMEOUT", result.Message);
            Assert.Contains("timed out", result.Details);
            Assert.DoesNotContain("483927155", result.Details);
            Assert.DoesNotContain("private-query-secret", result.Details);
            Assert.DoesNotContain("private-cookie", result.Details);
            var records = Directory.GetFiles(DiagnosticHub.Log.DirectoryPath, "vg-*.jsonl")
                .SelectMany(ReadRecords);
            var record = Assert.Single(records, e => e.GetProperty("jobId").GetString() == job.Id
                && e.GetProperty("stage").GetString() == "download"
                && e.GetProperty("status").GetString() != "started");
            Assert.Equal("failed", record.GetProperty("status").GetString());
            Assert.Equal(1, record.GetProperty("exitCode").GetInt32());
            var saved = record.GetProperty("message").GetString();
            Assert.Contains("NETWORK_TIMEOUT", saved);
            Assert.Contains("timed out", saved);
            Assert.DoesNotContain("483927155", saved);
            Assert.DoesNotContain("private-", saved);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static IEnumerable<JsonElement> ReadRecords(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            JsonElement record;
            try { record = JsonSerializer.Deserialize<JsonElement>(line); }
            catch (JsonException) { continue; }
            yield return record;
        }
    }
    private sealed class FailedRunner(string error) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
            => Task.FromResult(new ProcessResult(1, "", error));
    }
}
