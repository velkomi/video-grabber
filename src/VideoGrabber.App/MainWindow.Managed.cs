using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Licensing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Licensing;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
#if VIDEOGRABBER_MANAGED
    private const string AppProductName = "VideoGrabber.Managed";
    private const string AppDisplayName = "VideoGrabber Managed";
#else
    private const string AppProductName = "VideoGrabber";
    private const string AppDisplayName = "VideoGrabber";
#endif

    private readonly ConditionalWeakTable<BrowserDownloadQueueItem, ManagedIntentHolder> _managedQueueIntents = new();
    private readonly ManagedOperationCoordinator _managedCoordinator = CreateManagedCoordinator();

    private static string AppDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppProductName);

    private static ManagedOperationCoordinator CreateManagedCoordinator()
    {
#if VIDEOGRABBER_MANAGED
        var http = new HttpClient { BaseAddress = new Uri("https://licensing.invalid/") };
        return new ManagedOperationCoordinator(new LicensingApiClient(http, () => null, () => null));
#else
        return new ManagedOperationCoordinator(new LocalAccessClient());
#endif
    }
    private ManagedOperation CreateDownloadOperation(
        UserDownloadIntent intent,
        string kind,
        BrowserDownloadQueueItem? queuedEntry)
    {
        var intentId = queuedEntry is null
            ? Guid.NewGuid()
            : _managedQueueIntents.GetValue(queuedEntry, _ => new ManagedIntentHolder(Guid.NewGuid())).IntentId;
        var canonical = string.Join("\n",
            intent.SelectedSource.AbsoluteUri,
            intent.Quality,
            intent.AudioOnly ? "audio" : "video",
            kind);
        return new(intentId, HashManagedRequest(canonical), kind, "desktop_worker", null);
    }

    private static ManagedOperation CreateLocalOperation(string kind, params string[] values)
        => new(Guid.NewGuid(), HashManagedRequest(string.Join("\n", values)), kind, "desktop_worker", null);

    private static string HashManagedRequest(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ManagedReport(OperationOutcome outcome) => outcome switch
    {
        OperationOutcome.Succeeded => "completed",
        OperationOutcome.Cancelled => "cancel_requested",
        _ => "failed"
    };

    private sealed record ManagedIntentHolder(Guid IntentId);

    private sealed class LocalAccessClient : IManagedAccessClient
    {
        public Task<OperationPermit> AuthorizeAsync(ManagedOperation operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new OperationPermit(operation.IntentId, null, false));
        }

        public Task ReportAsync(OperationPermit permit, string outcome, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
