using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Diagnostics;

public enum LogMode { Full, Debug }

public sealed class DiagnosticLog
{
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate;
    private readonly int _retentionDays;
    private readonly long _maxFileBytes;
    private readonly long _maxTotalBytes;
    private string? _active;
    private DateTime _activeDate;
    public string DirectoryPath { get; }
    public LogMode Mode { get; set; }
    public string? LastError { get; private set; }

    public DiagnosticLog(string directory, LogMode mode = LogMode.Full, int retentionDays = 30,
        long maxFileBytes = 2 * 1024 * 1024, long maxTotalBytes = 32 * 1024 * 1024)
    {
        if (retentionDays is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        if (maxFileBytes < 2048 || maxTotalBytes < maxFileBytes) throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        DirectoryPath = Path.GetFullPath(directory);
        _gate = Gates.GetOrAdd(DirectoryPath, _ => new object());
        Mode = mode;
        _retentionDays = retentionDays;
        _maxFileBytes = maxFileBytes;
        _maxTotalBytes = maxTotalBytes;
    }

    public bool Write(string stage, string status, string message = "", bool debug = false,
        string? jobId = null, double? durationMs = null, int? exitCode = null)
    {
        if (debug && Mode != LogMode.Debug) return true;
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var now = DateTime.UtcNow;
                Prune(now);
                var safe = SensitiveDataRedactor.Redact(message);
                var maxMessage = (int)Math.Min(1800, (_maxFileBytes - 1024) / 6);
                if (safe.Length > maxMessage) safe = safe[..maxMessage] + " [обрезано]";
                var line = JsonSerializer.Serialize(new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    level = debug ? "Debug" : status is "failed" or "error" ? "Error" : "Information",
                    stage = Clean(stage, 80), status = Clean(status, 32),
                    jobId = Clean(jobId ?? DiagnosticHub.CurrentJobId ?? "application", 80),
                    durationMs, exitCode, message = safe
                }) + "\n";
                var bytes = Encoding.UTF8.GetByteCount(line);
                if (bytes > _maxFileBytes) throw new IOException("Событие превышает лимит файла журнала.");
                if (_active is null || _activeDate != now.Date || !File.Exists(_active) || new FileInfo(_active).Length + bytes > _maxFileBytes)
                {
                    _activeDate = now.Date;
                    _active = Path.Combine(DirectoryPath, $"vg-{now:yyyyMMdd}-{Environment.ProcessId}-{Guid.NewGuid():N}.jsonl");
                }
                File.AppendAllText(_active, line, new UTF8Encoding(false));
                Prune(now);
                LastError = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                LastError = "Журнал не записан: " + SensitiveDataRedactor.Redact(ex.Message);
                return false;
            }
        }
    }

    private void Prune(DateTime now)
    {
        var files = new DirectoryInfo(DirectoryPath).GetFiles("vg-*.jsonl").OrderBy(f => f.LastWriteTimeUtc).ToList();
        var cutoff = now.AddDays(-_retentionDays);
        foreach (var file in files.ToArray())
        {
            var dated = file.Name.Length >= 11 && DateTime.TryParseExact(file.Name.Substring(3, 8), "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day <= cutoff.Date;
            if (file.LastWriteTimeUtc < cutoff || dated) { file.Delete(); files.Remove(file); }
        }
        var total = files.Sum(f => f.Length);
        foreach (var file in files)
        {
            if (total <= _maxTotalBytes) break;
            total -= file.Length;
            file.Delete();
        }
    }

    private static string Clean(string value, int length)
    {
        var safe = SensitiveDataRedactor.Redact(value).Replace('\r', ' ').Replace('\n', ' ');
        return safe.Length <= length ? safe : safe[..length];
    }
}
