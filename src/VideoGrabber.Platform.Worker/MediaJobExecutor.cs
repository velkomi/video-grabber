using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Worker;

public sealed record WorkerResolvedSource(
    Uri Source,
    string FormatSelector,
    int? Width,
    int? Height,
    string MediaType);

public interface IWorkerSourceResolver
{
    Task<WorkerResolvedSource> ResolveAsync(
        string sourceId,
        string quality,
        CancellationToken cancellationToken);
}

public interface IMediaJobExecutor
{
    Task<ArtifactReceipt> ExecuteAsync(
        AttemptLease lease,
        CancellationToken cancellationToken);
}

public sealed class MediaJobExecutor(
    BoundedProcessRunner runner,
    WorkerToolLocator tools,
    ArtifactVerifier verifier,
    IWorkerSourceResolver sources,
    string jobRoot,
    Uri proxyUri) : IMediaJobExecutor
{
    public async Task<ArtifactReceipt> ExecuteAsync(
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.Work.Executor != "server_worker")
            throw new UnauthorizedAccessException("worker_scope_mismatch");
        if (lease.Work.Kind != "download")
            throw new NotSupportedException("This worker build only accepts download jobs.");
        if (proxyUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("A validated worker egress proxy is required.");

        var source = await sources.ResolveAsync(
            lease.Work.SourceId, lease.Work.Quality, cancellationToken).ConfigureAwait(false);
        if (source.Source.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(source.Source.UserInfo))
            throw new UnauthorizedAccessException("source_uri_invalid");

        var root = Path.GetFullPath(jobRoot);
        Directory.CreateDirectory(root);
        var attemptRoot = Path.Combine(
            root, lease.JobId.ToString("N"), lease.AttemptId.ToString("N"));
        if (Directory.Exists(attemptRoot))
            throw new IOException("Attempt directory already exists.");
        Directory.CreateDirectory(attemptRoot);

        var outputTemplate = Path.Combine(attemptRoot, "result.%(ext)s");
        var arguments = new List<string>
        {
            "--no-playlist",
            "--no-progress",
            "--no-overwrites",
            "--proxy", proxyUri.AbsoluteUri,
            "--ffmpeg-location", Path.GetDirectoryName(tools.Ffmpeg) ?? tools.Ffmpeg,
            "-f", source.FormatSelector,
            "-o", outputTemplate,
            "--",
            source.Source.AbsoluteUri
        };
        var result = await runner.RunAsync(new WorkerProcessSpec(
            tools.YtDlp, arguments, attemptRoot, TimeSpan.FromHours(2),
            new Dictionary<string, string?>
            {
                ["HOME"] = attemptRoot,
                ["TMPDIR"] = attemptRoot,
                ["XDG_CACHE_HOME"] = Path.Combine(attemptRoot, ".cache")
            }), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException("yt-dlp failed for the admitted source.");

        var outputs = Directory.EnumerateFiles(attemptRoot, "result.*", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (outputs.Length != 1)
            throw new InvalidDataException("Worker did not produce exactly one final artifact.");

        var evidence = "worker-verify-" + lease.AttemptId.ToString("N");
        return await verifier.VerifyAsync(
            attemptRoot, outputs[0], source.Width, source.Height,
            source.MediaType, evidence, cancellationToken).ConfigureAwait(false);
    }
}
