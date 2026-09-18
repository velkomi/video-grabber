using System.Text.Json;
using VideoGrabber.Core.Licensing;

namespace VideoGrabber.Infrastructure.Licensing;

public sealed class ManagedQueueStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> AllowedStates = new(StringComparer.Ordinal)
    {
        "pending", "running", "failed", "needs_revalidation"
    };

    private static readonly HashSet<string> AllowedOutputModes = new(StringComparer.Ordinal)
    {
        "video", "audio"
    };

    private readonly string _root;

    public ManagedQueueStore(string root)
    {
        _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
    }

    public async Task SaveAsync(SavedQueue queue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queue);
        var normalized = Normalize(queue);
        Directory.CreateDirectory(_root);
        var path = QueuePath(normalized.AccountId);
        var staging = Path.Combine(_root, $"queue-{normalized.AccountId:N}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(staging, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(staging)) File.Delete(staging); } catch (IOException) { }
        }
    }

    public async Task<SavedQueue> LoadAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account id is required.", nameof(accountId));
        var path = QueuePath(accountId);
        if (!File.Exists(path)) return Empty(accountId);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var saved = await JsonSerializer.DeserializeAsync<SavedQueue>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? throw new JsonException("Queue snapshot was empty.");
            var normalized = Normalize(saved);
            if (normalized.AccountId != accountId) throw new InvalidDataException("Queue account mismatch.");
            var restored = normalized.Items
                .Select(item => item with { State = "needs_revalidation" })
                .ToArray();
            return new SavedQueue(1, accountId, restored);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException)
        {
            Quarantine(path, accountId);
            return Empty(accountId);
        }
    }

    private SavedQueue Normalize(SavedQueue queue)
    {
        if (queue.Version != 1) throw new ArgumentException("Unsupported queue version.", nameof(queue));
        if (queue.AccountId == Guid.Empty) throw new ArgumentException("Account id is required.", nameof(queue));
        if (queue.Items is null) throw new ArgumentException("Queue items are required.", nameof(queue));
        if (queue.Items.Length > 500) throw new ArgumentException("Queue item limit exceeded.", nameof(queue));
        var seen = new HashSet<Guid>();
        var items = new SavedQueueItem[queue.Items.Length];
        for (var i = 0; i < queue.Items.Length; i++)
        {
            var item = queue.Items[i] ?? throw new ArgumentException("Queue item is required.", nameof(queue));
            if (item.IntentId == Guid.Empty || !seen.Add(item.IntentId))
                throw new ArgumentException("Queue intent ids must be unique and non-empty.", nameof(queue));
            if (item.Ordinal < 1 || item.Ordinal > 100000)
                throw new ArgumentException("Queue ordinal is invalid.", nameof(queue));
            if (string.IsNullOrWhiteSpace(item.MediaIdentity) || item.MediaIdentity.Length > 512 || HasControl(item.MediaIdentity))
                throw new ArgumentException("Media identity is invalid.", nameof(queue));
            if (string.IsNullOrWhiteSpace(item.Quality) || item.Quality.Length > 32 || HasControl(item.Quality))
                throw new ArgumentException("Quality is invalid.", nameof(queue));
            if (!AllowedOutputModes.Contains(item.OutputMode))
                throw new ArgumentException("Output mode is invalid.", nameof(queue));
            if (!AllowedStates.Contains(item.State))
                throw new ArgumentException("Queue state is invalid.", nameof(queue));

            items[i] = item with { PageOriginPath = NormalizePage(item.PageOriginPath) };
        }
        return new SavedQueue(1, queue.AccountId, items);
    }

    private static string NormalizePage(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Queue page must use HTTPS.", nameof(value));
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Queue page cannot contain user info.", nameof(value));
        var clean = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri.AbsoluteUri;
        return clean.EndsWith('/') && uri.AbsolutePath != "/" ? clean.TrimEnd('/') : clean;
    }

    private string QueuePath(Guid accountId) => Path.Combine(_root, $"queue-{accountId:N}.json");

    private static SavedQueue Empty(Guid accountId) => new(1, accountId, []);

    private static bool HasControl(string value) => value.Any(char.IsControl);

    private static void Quarantine(string path, Guid accountId)
    {
        if (!File.Exists(path)) return;
        var directory = Path.GetDirectoryName(path)!;
        var quarantine = Path.Combine(directory,
            $"queue-{accountId:N}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
        try { File.Move(path, quarantine); }
        catch (IOException) { }
    }
}
