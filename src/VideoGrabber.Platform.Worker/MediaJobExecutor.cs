using System.Globalization;
using System.Text;
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

public interface IWorkerArtifactResolver
{
    Task<IReadOnlyList<WorkerArtifactDescriptor>> ResolveArtifactsAsync(
        AttemptLease lease,
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
    IWorkerArtifactResolver artifacts,
    string jobRoot,
    Uri proxyUri,
    Uri? socialProxyUri = null,
    string? whisperModel = null,
    Uri? youtubePotProviderUri = null,
    string denoPath = "/usr/local/bin/deno") : IMediaJobExecutor
{
    public async Task<ArtifactReceipt> ExecuteAsync(
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.Work.Executor != "server_worker")
            throw new UnauthorizedAccessException("worker_scope_mismatch");
        if (proxyUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("A validated worker egress proxy is required.");

        var root = Path.GetFullPath(jobRoot);
        Directory.CreateDirectory(root);
        var attemptRoot = Path.Combine(
            root, lease.JobId.ToString("N"), lease.AttemptId.ToString("N"));
        if (Directory.Exists(attemptRoot))
            throw new IOException("Attempt directory already exists.");
        Directory.CreateDirectory(attemptRoot);

        return lease.Work.Kind switch
        {
            "download" => await DownloadAsync(lease, attemptRoot, audioOnly: false, cancellationToken),
            "mp3" => lease.Work.InputArtifactIds.Length == 0
                ? await DownloadAsync(lease, attemptRoot, audioOnly: true, cancellationToken)
                : await ExtractMp3Async(lease, attemptRoot, cancellationToken),
            "trim" => await TrimAsync(lease, attemptRoot, cancellationToken),
            "join" => await JoinAsync(lease, attemptRoot, cancellationToken),
            "transcribe" => await TranscribeAsync(lease, attemptRoot, cancellationToken),
            _ => throw new NotSupportedException("Unsupported worker media operation.")
        };
    }

    private async Task<ArtifactReceipt> DownloadAsync(
        AttemptLease lease,
        string attemptRoot,
        bool audioOnly,
        CancellationToken cancellationToken)
    {
        var source = await sources.ResolveAsync(
            lease.Work.SourceId, lease.Work.Quality, cancellationToken).ConfigureAwait(false);
        if (source.Source.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(source.Source.UserInfo))
            throw new UnauthorizedAccessException("source_uri_invalid");

        var outputTemplate = Path.Combine(
            attemptRoot, audioOnly ? "result.%(ext)s" : "result.%(ext)s");
        var selectedProxy = IsSocialVideoHost(source.Source) && socialProxyUri is not null
            ? socialProxyUri
            : proxyUri;
        var arguments = new List<string>
        {
            "--no-playlist", "--no-progress", "--no-overwrites",
            "--js-runtimes", "deno:" + denoPath,
            "--proxy", selectedProxy.AbsoluteUri,
            "--ffmpeg-location", Path.GetDirectoryName(tools.Ffmpeg) ?? tools.Ffmpeg
        };

        if (NeedsBrowserImpersonation(source.Source))
            arguments.AddRange(["--impersonate", "chrome"]);

        if (IsYouTube(source.Source) && youtubePotProviderUri is not null)
        {
            arguments.AddRange([
                "--extractor-args",
                "youtube:player_client=mweb",
                "--extractor-args",
                "youtubepot-bgutilhttp:base_url="
                    + youtubePotProviderUri.AbsoluteUri.TrimEnd('/')
            ]);
        }
        if (audioOnly)
        {
            arguments.AddRange(["-x", "--audio-format", "mp3", "--audio-quality", "0"]);
        }
        else
        {
            arguments.AddRange(["-f", source.FormatSelector]);
            if (TryParseQualityResolution(lease.Work.Quality, out var resolution))
                arguments.AddRange(["-S", "res:" + resolution]);
        }
        arguments.AddRange(["-o", outputTemplate, "--", source.Source.AbsoluteUri]);

        var result = await runner.RunAsync(new WorkerProcessSpec(
            tools.YtDlp, arguments, attemptRoot, TimeSpan.FromHours(2),
            SandboxEnvironment(attemptRoot)), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException("yt-dlp failed for the admitted source.");

        var outputs = FinalOutputs(attemptRoot);
        if (outputs.Length != 1)
            throw new InvalidDataException("Worker did not produce exactly one final artifact.");

        return await verifier.VerifyAsync(
            attemptRoot, outputs[0],
            audioOnly ? null : source.Width,
            audioOnly ? null : source.Height,
            audioOnly ? "audio/mpeg" : source.MediaType,
            Evidence(lease), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsYouTube(Uri source)
    {
        var host = source.Host.TrimEnd('.').ToLowerInvariant();
        return host is "youtu.be" or "youtube.com" or "www.youtube.com"
            or "m.youtube.com" or "music.youtube.com"
            || host.EndsWith(".youtube.com", StringComparison.Ordinal);
    }

    private static bool IsSocialVideoHost(Uri source)
        => IsYouTube(source) || NeedsBrowserImpersonation(source);

    private static bool NeedsBrowserImpersonation(Uri source)
    {
        var host = source.Host.TrimEnd('.').ToLowerInvariant();
        return host == "tiktok.com"
            || host.EndsWith(".tiktok.com", StringComparison.Ordinal)
            || host == "instagram.com"
            || host.EndsWith(".instagram.com", StringComparison.Ordinal)
            || host == "pinterest.com"
            || host.EndsWith(".pinterest.com", StringComparison.Ordinal)
            || host == "pin.it";
    }

    private static bool TryParseQualityResolution(string quality, out int resolution)
    {
        resolution = 0;
        if (string.IsNullOrWhiteSpace(quality)
            || !quality.EndsWith('p')
            || !int.TryParse(quality.AsSpan(0, quality.Length - 1), out var value)
            || value is < 144 or > 4320)
            return false;
        resolution = value;
        return true;
    }

    private async Task<ArtifactReceipt> ExtractMp3Async(
        AttemptLease lease,
        string attemptRoot,
        CancellationToken cancellationToken)
    {
        var input = await OneInputAsync(lease, cancellationToken);
        var output = Path.Combine(attemptRoot, "result.mp3");
        var result = await runner.RunAsync(new WorkerProcessSpec(
            tools.Ffmpeg,
            ["-v","error","-xerror","-i",input.StoragePath,
             "-vn","-codec:a","libmp3lame","-q:a","2","-y",output],
            attemptRoot, TimeSpan.FromHours(2), SandboxEnvironment(attemptRoot)),
            cancellationToken).ConfigureAwait(false);
        if (!result.Success) throw new InvalidDataException("MP3 extraction failed.");
        return await verifier.VerifyAsync(
            attemptRoot, output, null, null, "audio/mpeg",
            Evidence(lease), cancellationToken).ConfigureAwait(false);
    }

    private async Task<ArtifactReceipt> TrimAsync(
        AttemptLease lease,
        string attemptRoot,
        CancellationToken cancellationToken)
    {
        var input = await OneInputAsync(lease, cancellationToken);
        if (lease.Work.TrimStartMs is not long startMs
            || lease.Work.TrimDurationMs is not long durationMs)
            throw new InvalidDataException("Trim bounds are missing.");
        var output = Path.Combine(attemptRoot, "result.mp4");
        var result = await runner.RunAsync(new WorkerProcessSpec(
            tools.Ffmpeg,
            ["-v","error","-xerror",
             "-ss", Seconds(startMs),
             "-t", Seconds(durationMs),
             "-i", input.StoragePath,
             "-c:v","libx264","-preset","veryfast",
             "-c:a","aac","-movflags","+faststart","-y",output],
            attemptRoot, TimeSpan.FromHours(2), SandboxEnvironment(attemptRoot)),
            cancellationToken).ConfigureAwait(false);
        if (!result.Success) throw new InvalidDataException("Trim failed.");
        return await verifier.VerifyAsync(
            attemptRoot, output, null, null, "video/mp4",
            Evidence(lease), cancellationToken).ConfigureAwait(false);
    }

    private async Task<ArtifactReceipt> JoinAsync(
        AttemptLease lease,
        string attemptRoot,
        CancellationToken cancellationToken)
    {
        var inputs = await artifacts.ResolveArtifactsAsync(
            lease, cancellationToken).ConfigureAwait(false);
        if (inputs.Count < 2)
            throw new InvalidDataException("Join requires two owned inputs.");
        foreach (var item in inputs) EnsureReadableWorkerPath(item.StoragePath);

        var output = Path.Combine(attemptRoot, "result.mp4");
        var args = new List<string> { "-v", "error", "-xerror" };
        foreach (var input in inputs)
            args.AddRange(["-i", input.StoragePath]);
        var concatInputs = string.Concat(
            Enumerable.Range(0, inputs.Count).Select(i => $"[{i}:v:0][{i}:a:0]"));
        args.AddRange([
            "-filter_complex", concatInputs + $"concat=n={inputs.Count}:v=1:a=1[outv][outa]",
            "-map", "[outv]", "-map", "[outa]",
            "-c:v", "libx264", "-preset", "veryfast", "-threads", "1",
            "-c:a", "aac", "-movflags", "+faststart", "-y", output
        ]);
        var result = await runner.RunAsync(new WorkerProcessSpec(
            tools.Ffmpeg, args, attemptRoot, TimeSpan.FromHours(2),
            SandboxEnvironment(attemptRoot)), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidDataException("Join failed or streams are incompatible.");
        return await verifier.VerifyAsync(
            attemptRoot, output, null, null, "video/mp4",
            Evidence(lease), cancellationToken).ConfigureAwait(false);
    }
    private async Task<ArtifactReceipt> TranscribeAsync(
        AttemptLease lease,
        string attemptRoot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(whisperModel)
            || !File.Exists(whisperModel))
            throw new UnauthorizedAccessException("server_asr_model_unavailable");
        var input = await OneInputAsync(lease, cancellationToken);
        var outputBase = Path.Combine(attemptRoot, "result");
        var result = await runner.RunAsync(new WorkerProcessSpec(
            tools.Whisper,
            ["-m",whisperModel,"-f",input.StoragePath,
             "-otxt","-osrt","-of",outputBase],
            attemptRoot, TimeSpan.FromHours(2), SandboxEnvironment(attemptRoot)),
            cancellationToken).ConfigureAwait(false);
        if (!result.Success) throw new InvalidDataException("ASR failed.");
        var srt = outputBase + ".srt";
        var txt = outputBase + ".txt";
        if (!File.Exists(srt) || !File.Exists(txt)
            || new FileInfo(srt).Length == 0 || new FileInfo(txt).Length == 0)
            throw new InvalidDataException("ASR output is incomplete.");
        ValidateSrt(await File.ReadAllTextAsync(srt, cancellationToken));
        return await TextReceiptAsync(srt, "application/x-subrip", Evidence(lease), cancellationToken);
    }

    private async Task<WorkerArtifactDescriptor> OneInputAsync(
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        var inputs = await artifacts.ResolveArtifactsAsync(
            lease, cancellationToken).ConfigureAwait(false);
        if (inputs.Count != 1)
            throw new InvalidDataException("Operation requires one owned input.");
        EnsureReadableWorkerPath(inputs[0].StoragePath);
        return inputs[0];
    }

    private static void EnsureReadableWorkerPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path)
            || !File.Exists(path))
            throw new UnauthorizedAccessException("worker_artifact_path_unavailable");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("worker_artifact_symlink_rejected");
    }

    private static Dictionary<string,string?> SandboxEnvironment(string root)
        => new()
        {
            ["HOME"] = root,
            ["TMPDIR"] = root,
            ["XDG_CACHE_HOME"] = Path.Combine(root, ".cache")
        };

    private static string[] FinalOutputs(string root)
        => Directory.EnumerateFiles(root, "result.*", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static string Evidence(AttemptLease lease)
        => "worker-verify-" + lease.AttemptId.ToString("N");

    private static string Seconds(long milliseconds)
        => (milliseconds / 1000d).ToString("0.###", CultureInfo.InvariantCulture);

    private static void ValidateSrt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.Contains("-->", StringComparison.Ordinal)
            || Encoding.UTF8.GetByteCount(value) > 16 * 1024 * 1024)
            throw new InvalidDataException("SRT validation failed.");
    }

    private static async Task<ArtifactReceipt> TextReceiptAsync(
        string path,
        string mediaType,
        string evidence,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await System.Security.Cryptography.SHA256.HashDataAsync(
            stream, cancellationToken).ConfigureAwait(false);
        return new ArtifactReceipt(
            Guid.NewGuid(),
            Convert.ToHexString(digest).ToLowerInvariant(),
            info.Length,
            mediaType,
            evidence,
            path);
    }
}
