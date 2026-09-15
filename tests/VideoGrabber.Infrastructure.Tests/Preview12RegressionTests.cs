using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class Preview12RegressionTests
{
    [Fact]
    public void Dom_player_source_binds_webresource_master_without_frame_id()
    {
        var player = new Uri("https://api1.gcvh.ru/sign-player/?id=three");
        var metadata = new BrowserPageMetadata("День 1 - МОДУЛЬ №1", [],
            [
                new BrowserPlayerSlot(1, "Часть 1", new Uri("https://api1.gcvh.ru/sign-player/?id=one")),
                new BrowserPlayerSlot(2, "Часть 2", new Uri("https://api1.gcvh.ru/sign-player/?id=two")),
                new BrowserPlayerSlot(3, "Часть 3", player)
            ]);
        var candidate = Master(player);

        var bound = BrowserFrameBindingResolver.Bind(candidate, [], metadata);

        Assert.Equal(3, bound.PageOrdinal);
        Assert.Equal("Часть 3", bound.PageSectionTitle);
    }

    [Fact]
    public void Course_filename_puts_module_then_part_then_remaining_title()
    {
        var candidate = Master(new Uri("https://api1.gcvh.ru/sign-player/?id=three")) with
        {
            PageOrdinal = 3,
            PageSectionTitle = "Часть 3"
        };
        var metadata = new BrowserPageMetadata("День 1 - МОДУЛЬ №1", []);

        var name = MediaCandidatePresentation.SuggestedBaseName(candidate, 1, "720p", metadata);

        Assert.StartsWith("МОДУЛЬ №1 - Часть 3 - День 1", name, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("720p", name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("03 - ", name, StringComparison.Ordinal);
    }

    [Fact]
    public void Duration_below_one_hour_is_minutes_and_seconds()
    {
        Assert.Equal("59m19s", DownloadFileName.DurationTag(3559));
        Assert.Equal("01h02m03s", DownloadFileName.DurationTag(3723));
    }

    [Fact]
    public async Task Default_download_work_files_live_in_owned_child_of_selected_output_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-preview12-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new OutputFolderRunner();
            var probe = new StubProbe(new MediaProbeResult(true, true, true, "aac", DurationSeconds: 3559));
            var result = await new YtDlpDownloader(runner, new ToolLocator(root, root), probe).DownloadAsync(
                new DownloadRequest(new Uri("https://cdn.example/master.m3u8"), root, "720p",
                    DirectManifest: true, SuggestedBaseName: "МОДУЛЬ №1 - Часть 3 - День 1 - 720p"),
                null, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal(root, Path.GetDirectoryName(runner.ObservedWorkingDirectory));
            Assert.StartsWith(".vg-job-", Path.GetFileName(runner.ObservedWorkingDirectory));
            Assert.StartsWith(root, result.OutputPath!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("59m19s", Path.GetFileName(result.OutputPath!), StringComparison.Ordinal);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Queue_mutations_are_available_during_active_download_and_runner_is_live()
    {
        var root = FindRepoRoot();
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));

        Assert.DoesNotContain("if (_operation is not null) return;\n        if ((_mediaCandidatesBox.SelectedItem", batch);
        Assert.DoesNotContain("var snapshot = _browserDownloadQueue.Items.ToArray();", batch);
        Assert.Contains("while (_browserDownloadQueue.Items.Count > 0)", batch);
        Assert.Contains("ScheduleBrowserBindingRefresh", devtools);
        var metadata = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Metadata.cs"));
        Assert.Contains("BrowserFrameBindingResolver.BindAll", metadata);
    }

    private static MediaCandidate Master(Uri referer) => new(
        new Uri("https://cdn.example/master.m3u8"), referer, "HLS",
        HlsManifest: new HlsManifestInfo(true,
            [new(new Uri("https://cdn.example/720.m3u8"), 1280, 720, 1_500_000, 30, null)],
            [], false, null, false));

    private sealed class OutputFolderRunner : IProcessRunner
    {
        public string? ObservedWorkingDirectory { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            ObservedWorkingDirectory = spec.WorkingDirectory;
            var path = Path.Combine(spec.WorkingDirectory!, "МОДУЛЬ №1 - Часть 3 - День 1 - 720p - downloading.mp4");
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            onOutput?.Invoke("filepath:" + path);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class StubProbe(MediaProbeResult result) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src", "VideoGrabber.App"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
