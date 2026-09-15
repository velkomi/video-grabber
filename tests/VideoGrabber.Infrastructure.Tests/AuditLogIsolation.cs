using System.Reflection;
using System.Runtime.CompilerServices;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.Infrastructure.Tests;

internal static class AuditLogIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE")
            ?? throw new InvalidOperationException("Audit evidence directory is required.");
        var directory = Path.GetFullPath(Path.Combine(evidence, "audit-diagnostics"));
        var field = typeof(DiagnosticLog).GetField("<DirectoryPath>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DiagnosticLog directory field was not found.");
        field.SetValue(DiagnosticHub.Log, directory);
        if (DiagnosticHub.Log.DirectoryPath != directory)
            throw new InvalidOperationException("Diagnostic log isolation failed.");
    }
}
