using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Media;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed partial class AuthenticatedHlsDownloadIntegrationTests
{
    [MediaToolsFact]
    public async Task Audit_Six_real_parts_delayed_manifests_match_content_when_binding_is_known()
    {
        var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE")!;
        var root = Path.Combine(evidence, "six-parts");
        var output = Path.Combine(evidence, "six-parts-downloaded");
        Directory.CreateDirectory(output);
        var servers = Enumerable.Range(1, 6).Select(part => new HlsFixture(Path.Combine(root, "part-" + part))).ToArray();
        var runner = new ProcessRunner();
        var tools = new ToolLocator(Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
        var slots = Enumerable.Range(1, 6).Select(part => new BrowserPlayerSlot(part, "PART " + part,
            new Uri("https://api1.gcvh.ru/sign-player/?part=" + part))).ToArray();
        var metadata = new BrowserPageMetadata("SYNTHETIC LESSON", slots.Select(s => s.Title!).ToArray(), slots);
        var arrivals = new List<MediaCandidate>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            // Seeded shuffled delays exercise arrival order without dependence on machine timing.
            var delays = new[] { 600, 400, 0, 800, 200, 1000 };
            await Task.WhenAll(Enumerable.Range(1, 6).Select(async part =>
            {
                await Task.Delay(delays[part - 1], deadline.Token);
                var server = servers[part - 1];
                using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false });
                using var request = new HttpRequestMessage(HttpMethod.Get, server.Playlist);
                request.Headers.Add("Cookie", "vg_session=fixture-only");
                request.Headers.Referrer = server.Lesson;
                using var response = await http.SendAsync(request, deadline.Token);
                response.EnsureSuccessStatusCode();
                var text = await response.Content.ReadAsStringAsync(deadline.Token);
                Assert.True(HlsManifestParser.TryParse(text, new Uri("https://fixture.example/master.m3u8"), out var manifest));
                // Test-only transport mapping: production URL policy is not relaxed.
                manifest = manifest! with { Variants = manifest.Variants.Select(v => v with
                    { Uri = new Uri(server.Playlist, Path.GetFileName(v.Uri.AbsolutePath)) }).ToArray(),
                    DurationSeconds = part <= 2 ? 2 : part };
                lock (arrivals) arrivals.Add(new MediaCandidate(server.Playlist, slots[part - 1].Source!, "HLS", HlsManifest: manifest));
            }));
            var bound = BrowserFrameBindingResolver.BindAll(arrivals, [], metadata);
            var ambiguous = BrowserFrameBindingResolver.BindAll(arrivals.Select(c => c with
                { Referer = new Uri("https://school.example/lesson") }).ToArray(), [], metadata);
            var rows = new List<object>();
            var frameHashes = new Dictionary<int, string>();
            var durations = new Dictionary<int, double?>();
            var neutralNames = ambiguous.Select((candidate, index) =>
                MediaCandidatePresentation.SuggestedBaseName(candidate, index + 1, "720p", metadata)).ToArray();
            Assert.Equal(6, neutralNames.Distinct().Count());
            Assert.All(neutralNames, name => Assert.DoesNotContain("PART", name));
            foreach (var candidate in bound.OrderBy(c => c.PageOrdinal))
            {
                var part = Array.FindIndex(servers, s => s.Playlist == candidate.Source) + 1;
                var height = part % 2 == 1 ? 360 : 720;
                var server = servers[part - 1];
                using var egress = ManagedEgressFixture.RegisterListener("six-part-" + part, server.Playlist);
                var globalQuality = part % 2 == 1 ? "720p" : "360p";
                var selectedQuality = part == 6 ? "best" : height + "p";
                if (part == 6) globalQuality = "best";
                var selectedCandidate = candidate;
                if (part == 1)
                {
                    // A directly captured 360p leaf carries no variant height metadata.
                    Assert.True(HlsManifestParser.TryParse(File.ReadAllText(Path.Combine(root, "part-1", "360.m3u8")),
                        new Uri("https://fixture.example/360.m3u8"), out var leaf));
                    Assert.Empty(leaf!.Variants);
                    selectedCandidate = candidate with { Source = new Uri(server.Playlist, "360.m3u8"), HlsManifest = leaf };
                }
                var controls = new UserDownloadIntent(selectedCandidate.Source, globalQuality, false, output, "embedded", 1);
                var intent = controls with { Quality = selectedQuality };
                var plan = MediaDownloadPlanResolver.Resolve(selectedCandidate, intent.Quality, intent.AudioOnly);
                Assert.True(plan.IsResolved);
                Assert.True(plan.ResolvedHlsLeaf);
                using var cookies = ScopedCookieFile.Create([plan.Source, server.Lesson],
                    [new BrowserCookie("127.0.0.1", "/", "vg_session", "fixture-only", false, true)]);
                var request = await DownloadRequestFactory.PrepareAsync(intent, (captured, _) => Task.FromResult(new PreparedDownload(
                    plan.Source, server.Lesson, cookies.Path, null, null, plan.HlsVideoSource, plan.HlsAudioSource,
                    plan.DirectManifest, plan.ResolvedHlsLeaf,
                    MediaCandidatePresentation.SuggestedBaseName(candidate, 99, captured.Quality, metadata),
                    candidate.HlsManifest!.DurationSeconds, true)), deadline.Token);
                Assert.Equal(selectedQuality, request.Quality);
                request = egress.Apply(request);
                var result = await new YtDlpDownloader(runner, tools, egressRegistry: egress.Registry)
                    .DownloadAsync(request, null, deadline.Token);
                Assert.True(result.Success, result.Message + result.Details);
                Assert.Contains("PART " + part, Path.GetFileName(result.OutputPath!));
                var probe = await new FfprobeMediaProbe(runner, tools).ProbeAsync(result.OutputPath!, deadline.Token);
                Assert.True(probe.HasVideo && probe.HasAudio);
                Assert.InRange(probe.DurationSeconds, request.ExpectedDurationSeconds!.Value - .2, request.ExpectedDurationSeconds.Value + .2);
                var frame = await FrameHash(result.OutputPath!);
                var referencePath = Path.Combine(root, "part-" + part, "part-" + part + "-" + height + ".mp4");
                var reference = await FrameHash(referencePath);
                var audio = await AudioHash(result.OutputPath!);
                // Compare the transmitted HLS samples: the pre-HLS MP4 trims AAC priming differently.
                var referenceAudio = await AudioHash(Path.Combine(root, "part-" + part, height + ".m3u8"));
                frameHashes[part] = frame;
                durations[part] = probe.DurationSeconds;
                var decode = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                    ["-v", "error", "-nostdin", "-i", result.OutputPath!, "-f", "null", "-"]), null, deadline.Token);
                var rawProbe = await runner.RunAsync(new ProcessSpec(tools.Ffprobe,
                    ["-v", "error", "-show_streams", "-of", "json", result.OutputPath!]), null, deadline.Token);
                using var parsed = JsonDocument.Parse(rawProbe.StandardOutput);
                var actualHeight = parsed.RootElement.GetProperty("streams").EnumerateArray()
                    .Single(s => s.GetProperty("codec_type").GetString() == "video").GetProperty("height").GetInt32();
                var unknownOrdinal = ambiguous.Single(c => c.Source == candidate.Source).PageOrdinal;
                rows.Add(new { part, selectedOrdinal = candidate.PageOrdinal, ambiguousOrdinal = unknownOrdinal,
                    globalQuality, selectedQuality, resolvedHlsLeaf = request.ResolvedHlsLeaf,
                    unknownManifestHeight = part == 1,
                    expectedHeight = height, actualHeight, frame, reference, sameDecodedFrame = frame == reference,
                    audio, referenceAudio, sameDecodedAudio = audio == referenceAudio,
                    output = result.OutputPath, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(result.OutputPath!))),
                    fullDecode = decode.IsSuccess, duration = probe.DurationSeconds });
                File.WriteAllText(Path.Combine(evidence, "six-parts-behavior.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
                Assert.Equal(part, candidate.PageOrdinal);
                Assert.Equal(height, actualHeight);
                Assert.Equal(reference, frame);
                Assert.Equal(referenceAudio, audio);
                Assert.True(decode.IsSuccess, decode.StandardError);
            }
            Assert.Equal(durations[1], durations[2]);
            Assert.NotEqual(frameHashes[1], frameHashes[2]);
            Assert.All(ambiguous, c => { Assert.Null(c.PageOrdinal); Assert.Null(c.PageSectionTitle); });
        }
        finally { foreach (var server in servers) await server.DisposeAsync(); }

        async Task<string> FrameHash(string path)
        {
            var result = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                ["-v", "error", "-nostdin", "-i", path, "-map", "0:v:0", "-frames:v", "1", "-f", "hash", "-hash", "sha256", "-"]), null, deadline.Token);
            Assert.True(result.IsSuccess, result.StandardError);
            return result.StandardOutput.Trim();
        }

        async Task<string> AudioHash(string path)
        {
            var result = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                ["-v", "error", "-nostdin", "-i", path, "-map", "0:a:0", "-f", "hash", "-hash", "sha256", "-"]), null, deadline.Token);
            Assert.True(result.IsSuccess, result.StandardError);
            return result.StandardOutput.Trim();
        }
    }
}
