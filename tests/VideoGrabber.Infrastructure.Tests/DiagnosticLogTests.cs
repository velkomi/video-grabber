using System.Text.Json;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DiagnosticLogTests
{
    [Fact]
    public void Log_redacts_secrets_filters_debug_and_reports_write_failure()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var log = new DiagnosticLog(root);
            log.Write("download", "succeeded", "Cookie: sid=secret; second=another");
            log.Write("process", "output", "debug-only", debug: true);
            var text = string.Join("", Directory.GetFiles(root, "vg-*.jsonl").Select(File.ReadAllText));
            Assert.DoesNotContain("secret", text);
            Assert.DoesNotContain("another", text);
            Assert.DoesNotContain("debug-only", text);
            log.Mode = LogMode.Debug;
            log.Write("process", "output", "debug-included", debug: true);
            Assert.Contains("debug-included", string.Join("", Directory.GetFiles(root, "vg-*.jsonl").Select(File.ReadAllText)));
            var blocked = Path.Combine(root, "file-not-directory");
            File.WriteAllText(blocked, "keep");
            var bad = new DiagnosticLog(blocked);
            Assert.False(bad.Write("test", "failed", "x"));
            Assert.NotNull(bad.LastError);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Log_rotates_bounds_size_prunes_age_and_supports_parallel_writes()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var old = Path.Combine(root, "vg-old.jsonl");
            File.WriteAllText(old, "{}");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-31));
            File.WriteAllText(Path.Combine(root, "unrelated.txt"), "keep");
            var log = new DiagnosticLog(root, maxFileBytes: 4096, maxTotalBytes: 16384);
            Parallel.For(0, 160, i => log.Write("test", "succeeded", "Event " + i + new string('x', 90)));
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(Path.Combine(root, "unrelated.txt")));
            var files = Directory.GetFiles(root, "vg-*.jsonl");
            Assert.True(files.Sum(p => new FileInfo(p).Length) <= 16384);
            Assert.All(files, path => Assert.True(new FileInfo(path).Length <= 4096));
            foreach (var line in files.SelectMany(File.ReadLines))
            {
                using var json = JsonDocument.Parse(line);
                Assert.True(json.RootElement.TryGetProperty("timestamp", out _));
                Assert.True(json.RootElement.TryGetProperty("jobId", out _));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticLog(root, retentionDays: 31));
        }
        finally { Directory.Delete(root, true); }
    }
}
