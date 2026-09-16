using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("VideoGrabber.Infrastructure.Tests")]

namespace VideoGrabber.Infrastructure.Processes;

internal interface INativeChildProcessHandle : IDisposable
{
    int Id { get; }
    StreamReader StandardOutput { get; }
    StreamReader StandardError { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken token);
    void TerminateOwnedTree();
}
