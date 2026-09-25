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
    private ManagedOperationCoordinator _managedCoordinator = null!;

#if VIDEOGRABBER_MANAGED
    private HttpClient _managedHttp = null!;
    private WindowsSessionStore _managedSessionStore = null!;
    private WindowsSessionStore _managedDeviceKeyStore = null!;
    private WindowsSessionStore _managedLeaseStore = null!;
    private ManagedQueueStore _managedQueueStore = null!;
    private OfflineAccessCache? _managedOfflineCache;
    private string? _managedAccessToken;
    private Guid? _managedAccountId;
    private Guid? _managedDeviceId;
    private VideoGrabber.Platform.Contracts.AccessSnapshot? _managedAccessSnapshot;
    private SavedQueue _managedRestoredQueue = new(1, Guid.Empty, []);
    private readonly SemaphoreSlim _managedQueueWriteLock = new(1, 1);
#endif

    private static string AppDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppProductName);

    private void InitializeManagedServices()
    {
#if VIDEOGRABBER_MANAGED
        _managedHttp = new HttpClient { BaseAddress = ResolveManagedApiBaseUri() };
        var authRoot = Path.Combine(AppDataRoot, "auth");
        _managedSessionStore = new WindowsSessionStore(authRoot, "refresh.bin");
        _managedDeviceKeyStore = new WindowsSessionStore(authRoot, "device-key.bin");
        _managedLeaseStore = new WindowsSessionStore(authRoot, "lease.bin");
        _managedQueueStore = new ManagedQueueStore(Path.Combine(AppDataRoot, "queue"));
        RebuildManagedCoordinator();
#else
        _managedCoordinator = new ManagedOperationCoordinator(new LocalAccessClient());
#endif
    }

#if VIDEOGRABBER_MANAGED
    private void RebuildManagedCoordinator()
        => _managedCoordinator = new ManagedOperationCoordinator(new LicensingApiClient(
            _managedHttp,
            () => _managedAccessToken,
            () => _managedDeviceId,
            _managedOfflineCache,
            refreshAccessToken: async cancellationToken =>
            {
                await RefreshManagedSensitiveSessionAsync(cancellationToken);
                return _managedAccessToken;
            }));

    private static Uri ResolveManagedApiBaseUri()
    {
        var configured = Environment.GetEnvironmentVariable("VIDEOGRABBER_PLATFORM_URL");
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && IsAllowedManagedApiBase(uri))
            return EnsureTrailingSlash(uri);
        return new Uri("https://videograbber.srv1902378.hstgr.cloud/");
    }

    private static bool IsAllowedManagedApiBase(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp
                && string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal));

    private static Uri EnsureTrailingSlash(Uri uri)
        => uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + "/");
#endif

    private Guid ManagedIntentId(BrowserDownloadQueueItem entry)
        => ManagedIntent(entry).IntentId;

    private ManagedIntentHolder ManagedIntent(BrowserDownloadQueueItem entry)
        => _managedQueueIntents.GetValue(entry, _ => new ManagedIntentHolder(
            Guid.NewGuid(), _audioOnlyBox?.IsChecked == true ? "audio" : "video"));

    private void BindManagedIntent(BrowserDownloadQueueItem entry, Guid intentId, string outputMode = "video")
    {
        _managedQueueIntents.Remove(entry);
        _managedQueueIntents.Add(entry, new ManagedIntentHolder(intentId, outputMode));
    }

    private ManagedOperation CreateDownloadOperation(
        UserDownloadIntent intent,
        string kind,
        BrowserDownloadQueueItem? queuedEntry)
    {
        var intentId = queuedEntry is null ? Guid.NewGuid() : ManagedIntentId(queuedEntry);
        var canonical = string.Join("\n",
            intent.SelectedSource.AbsoluteUri,
            intent.Quality,
            intent.AudioOnly ? "audio" : "video",
            kind);
        return new(intentId, HashManagedRequest(canonical), kind, "desktop_worker", CurrentManagedDeviceId());
    }

    private ManagedOperation CreateLocalOperation(string kind, params string[] values)
        => new(Guid.NewGuid(), HashManagedRequest(string.Join("\n", values)),
            kind, "desktop_worker", CurrentManagedDeviceId());

    private Guid? CurrentManagedDeviceId()
    {
#if VIDEOGRABBER_MANAGED
        return _managedDeviceId;
#else
        return null;
#endif
    }

    private static string HashManagedRequest(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ManagedReport(OperationOutcome outcome) => outcome switch
    {
        OperationOutcome.Succeeded => "completed",
        OperationOutcome.Cancelled => "cancel_requested",
        _ => "failed"
    };

    private sealed record ManagedIntentHolder(Guid IntentId, string OutputMode);

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
