using System.Diagnostics;

namespace VideoGrabber.Infrastructure.Diagnostics;

public static class DiagnosticHub
{
    private static readonly AsyncLocal<string?> Job = new();
    public static string? CurrentJobId => Job.Value;
    public static DiagnosticLog Log { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGrabber", "logs"));
    public static Operation Begin(string stage, string message = "") => new(stage, message);
    public sealed class Operation : IDisposable
    {
        private readonly string? _parent;
        private readonly string _stage;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private string _status = "failed";
        private bool _disposed;
        public string Id { get; }
        internal Operation(string stage, string message)
        {
            _stage = stage;
            _parent = Job.Value;
            Id = _parent ?? Guid.NewGuid().ToString("N");
            Job.Value = Id;
            Log.Write(stage, "started", message, jobId: Id);
        }
        public void Complete(bool success = true) => _status = success ? "succeeded" : "failed";
        public void Cancel() => _status = "cancelled";
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Log.Write(_stage, _status, durationMs: _watch.Elapsed.TotalMilliseconds, jobId: Id);
            Job.Value = _parent;
        }
    }
}
