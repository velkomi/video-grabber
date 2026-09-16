using System.Text;
using System.Text.Json;
using VideoGrabber.Core.Processes;

namespace VideoGrabber.Infrastructure.Components;

public sealed class ComponentInstallTransaction(IProcessRunner runner) : IComponentInstallTransaction
{
    private readonly object _rollbackGate = new();
    private string? _lastCommittedRoot;
    private string? _lastCommittedOldPointer;
    private bool _lastCommittedRollbackAvailable;
    public const string PreparedFileName = "components-prepared.json";
    public const string JournalFileName = "components-transaction.json";
    public const string PointerFileName = "components-current.json";
    private const string LockFileName = ".components-transaction.lock";

    public async Task CommitAsync(string destination, CancellationToken recoveryDeadline)
    {
        var root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        await using var transactionLock = AcquireLock(root);
        RecoverPendingUnsafe(root);
        recoveryDeadline.ThrowIfCancellationRequested();

        var prepared = ReadPrepared(root);
        var snapshot = CreateSnapshot(root, prepared.Generation, prepared.Sha256);
        var oldPointer = ReadOptional(Path.Combine(root, PointerFileName));
        var newPointer = CreatePointerJson(root, snapshot);
        var journal = new Journal(1, "pending", oldPointer, newPointer, prepared.Generation);
        WriteDurableJson(Path.Combine(root, JournalFileName), journal);

        try
        {
            WriteDurableText(Path.Combine(root, PointerFileName), newPointer);
            await SelfTestAsync(snapshot, recoveryDeadline).ConfigureAwait(false);
            WriteDurableJson(Path.Combine(root, JournalFileName), journal with { State = "committed" });
            TryDelete(Path.Combine(root, PreparedFileName));
            lock (_rollbackGate)
            {
                _lastCommittedRoot = root;
                _lastCommittedOldPointer = oldPointer;
                _lastCommittedRollbackAvailable = true;
            }
        }
        catch
        {
            RestorePointer(root, oldPointer);
            WriteDurableJson(Path.Combine(root, JournalFileName), journal with { State = "rolledBack" });
            throw;
        }
    }

    public Task AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline)
    {
        recoveryDeadline.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        using var transactionLock = AcquireLock(root);
        if (!RollbackLastCommittedUnsafe(root)) RecoverPendingUnsafe(root);
        TryDelete(Path.Combine(root, PreparedFileName));
        return Task.CompletedTask;
    }

    private async Task SelfTestAsync(ComponentSetSnapshot snapshot, CancellationToken token)
    {
        var tests = new[]
        {
            new ProcessSpec(snapshot.YtDlp, ["--version"], Timeout: TimeSpan.FromSeconds(10)),
            new ProcessSpec(snapshot.Ffmpeg, ["-version"], Timeout: TimeSpan.FromSeconds(10)),
            new ProcessSpec(snapshot.Ffprobe, ["-version"], Timeout: TimeSpan.FromSeconds(10)),
            new ProcessSpec(snapshot.Deno, ["--version"], Timeout: TimeSpan.FromSeconds(10))
        };
        foreach (var spec in tests)
        {
            token.ThrowIfCancellationRequested();
            var result = await runner.RunAsync(spec, null, token).ConfigureAwait(false);
            if (!result.IsSuccess)
                throw new InvalidDataException("Component self-test failed: " + Path.GetFileName(spec.FileName));
        }
    }

    private static Prepared ReadPrepared(string root)
    {
        var path = Path.Combine(root, PreparedFileName);
        if (!File.Exists(path)) throw new InvalidDataException("Prepared component manifest is missing.");
        try
        {
            var value = JsonSerializer.Deserialize<Prepared>(File.ReadAllText(path), JsonOptions());
            if (value is null || value.SchemaVersion != 1 || string.IsNullOrWhiteSpace(value.Generation)
                || value.Sha256 is null)
                throw new InvalidDataException("Prepared component manifest is incomplete.");
            return value;
        }
        catch (JsonException ex) { throw new InvalidDataException("Prepared component manifest is malformed.", ex); }
    }

    private static ComponentSetSnapshot CreateSnapshot(
        string root, string generationValue, IReadOnlyDictionary<string, string> hashes)
    {
        if (Path.IsPathRooted(generationValue))
            throw new InvalidDataException("Prepared generation must be relative.");
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var generation = Path.GetFullPath(Path.Combine(root,
            generationValue.Replace('/', Path.DirectorySeparatorChar)));
        if (!generation.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Prepared generation escapes the component root.");
        return new ComponentSetSnapshot(generation,
            Path.Combine(generation, "yt-dlp.exe"),
            Path.Combine(generation, "ffmpeg.exe"),
            Path.Combine(generation, "ffprobe.exe"),
            Path.Combine(generation, "deno.exe"), hashes);
    }

    private static string CreatePointerJson(string root, ComponentSetSnapshot snapshot)
    {
        var relative = Path.GetRelativePath(root, snapshot.GenerationDirectory).Replace('\\', '/');
        return JsonSerializer.Serialize(new Pointer(1, relative,
            new Dictionary<string, string>(snapshot.Sha256, StringComparer.OrdinalIgnoreCase)));
    }

    private bool RollbackLastCommittedUnsafe(string root)
    {
        string? oldPointer;
        lock (_rollbackGate)
        {
            if (!_lastCommittedRollbackAvailable
                || !string.Equals(_lastCommittedRoot, root, StringComparison.OrdinalIgnoreCase))
                return false;
            oldPointer = _lastCommittedOldPointer;
            _lastCommittedRollbackAvailable = false;
            _lastCommittedRoot = null;
            _lastCommittedOldPointer = null;
        }
        RestorePointer(root, oldPointer);
        var journalPath = Path.Combine(root, JournalFileName);
        if (File.Exists(journalPath))
        {
            Journal? journal;
            try { journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(journalPath), JsonOptions()); }
            catch (JsonException ex) { throw new InvalidDataException("Component transaction journal is malformed.", ex); }
            if (journal is null || journal.SchemaVersion != 1)
                throw new InvalidDataException("Component transaction journal is invalid.");
            WriteDurableJson(journalPath, journal with { State = "rolledBack" });
        }
        return true;
    }

    private static void RecoverPendingUnsafe(string root)
    {
        var path = Path.Combine(root, JournalFileName);
        if (!File.Exists(path)) return;
        Journal? journal;
        try { journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path), JsonOptions()); }
        catch (JsonException ex) { throw new InvalidDataException("Component transaction journal is malformed.", ex); }
        if (journal is null || journal.SchemaVersion != 1)
            throw new InvalidDataException("Component transaction journal is invalid.");
        if (!string.Equals(journal.State, "pending", StringComparison.OrdinalIgnoreCase)) return;
        RestorePointer(root, journal.OldPointer);
        WriteDurableJson(path, journal with { State = "rolledBack" });
    }

    private static FileStream AcquireLock(string root)
        => new(Path.Combine(root, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.None, 1, FileOptions.WriteThrough);

    private static void RestorePointer(string root, string? oldPointer)
    {
        var path = Path.Combine(root, PointerFileName);
        if (oldPointer is null) { TryDelete(path); return; }
        WriteDurableText(path, oldPointer);
    }

    private static string? ReadOptional(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static void WriteDurableJson<T>(string path, T value)
        => WriteDurableText(path, JsonSerializer.Serialize(value));

    private static void WriteDurableText(string path, string text)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else File.Move(temp, path);
        }
        finally { TryDelete(temp); }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };
    private sealed record Prepared(int SchemaVersion, string Generation, Dictionary<string, string> Sha256);
    private sealed record Pointer(int SchemaVersion, string Generation, Dictionary<string, string> Sha256);
    private sealed record Journal(int SchemaVersion, string State, string? OldPointer, string NewPointer, string Generation);
}
