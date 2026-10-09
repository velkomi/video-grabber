using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace VideoGrabber.App;

// Compiled only into isolated QA builds; never restores the user's account.
public sealed partial class MainWindow
{
    private static string StudioProbeRoot => Path.GetFullPath(
        Environment.GetEnvironmentVariable("VIDEOGRABBER_PRESENTATION_ROOT")
        ?? throw new InvalidOperationException("An isolated probe directory is required."));

    internal void PrepareStudioProbePage()
    {
        ShowPage(Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_PAGE") ?? "account");
        if (Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_PAGE_ONLY") == "1")
        {
            ((Grid)VisualTreeHelper.GetParent(_accountPage)).Children.Remove(_accountPage);
            _rootHost.Children.Clear();
            _rootHost.Children.Add(_accountPage);
        }
        if (Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_SHELL") == "1")
        {
            _rootHost.Children.Clear();
            _rootHost.Children.Add(new TextBlock { Text = "MainWindow native baseline", FontSize = 28 });
            if (Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_BUTTON") == "1")
                _rootHost.Children.Add(StudioButton("Native button", true));
        }
    }

    internal async void StartStudioPresentationProbe()
    {
        var checks = new List<object>();
        Directory.CreateDirectory(StudioProbeRoot);
        File.AppendAllText(Path.Combine(StudioProbeRoot, "stages.txt"), "entered\n");
        try
        {
            Directory.CreateDirectory(StudioProbeRoot);
            await Task.Delay(800);
            var updateScenario = Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_UPDATES");
            if (!string.IsNullOrWhiteSpace(updateScenario)) PrepareClientUpdateProbe(updateScenario);
            if (Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_REFERRALS") == "1")
            {
                _managedReferralsCard.Visibility = Visibility.Visible;
                RenderManagedReferrals(new VideoGrabber.Platform.Contracts.ReferralSummary("QAONLYCODE",
                    new Uri("https://example.test/web/?ref=QAONLYCODE"), new Uri("https://t.me/example_bot?start=ref_QAONLYCODE"),3,1,
                    [new("RUB",13500,22500,0,0),new("XTR",27,45,0,0)],
                    [new("reward",new(13500,"RUB"),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(14),DateTimeOffset.UtcNow.AddDays(379))]));
            }
            await CaptureStudioProbeAsync("initial-page.png");
            foreach (var width in new[] { 1268, 1100, 960, 930, 800, 640 })
            {
                AppWindow.Resize(new SizeInt32(width, 900));
                await Task.Delay(350);
                foreach (var page in new[] { "download", "account", "settings", "editor", "info" })
                {
                    File.AppendAllText(Path.Combine(StudioProbeRoot, "stages.txt"), page + "-" + width + "\n");
                    ShowPage(page);
                    _rootHost.UpdateLayout();
                    await Task.Delay(250);
                    var current = page switch
                    {
                        "download" => _downloadPage, "account" => _accountPage,
                        "settings" => _settingsPage, "editor" => _editorPage, _ => _infoPage
                    };
                    current.ChangeView(null, 0, null, true);
                    await Task.Delay(100);
                    await CaptureStudioProbeAsync($"{page}-{width}.png");
                    if (page == "info" && !string.IsNullOrWhiteSpace(updateScenario))
                    {
                        var card = (FrameworkElement)VisualTreeHelper.GetParent(_clientUpdateStatus);
                        var offset = card.TransformToVisual((UIElement)current.Content).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
                        current.ChangeView(null, offset, null, true);
                        await Task.Delay(100);
                        await CaptureStudioProbeAsync($"updates-{updateScenario}-{width}.png");
                        if (_clientUpdateNotice.Visibility != Visibility.Visible || _clientUpdateNotice.ActualHeight > _rootHost.ActualHeight - 180)
                            throw new InvalidOperationException("Update notice is missing or leaves no room for the app.");
                    }
                    if (page == "account" && Environment.GetEnvironmentVariable("VIDEOGRABBER_PROBE_REFERRALS") == "1")
                    {
                        var offset = _managedReferralsCard.TransformToVisual((UIElement)current.Content).TransformPoint(new Windows.Foundation.Point(0,0)).Y;
                        current.ChangeView(null,offset,null,true);
                        await Task.Delay(100);
                        await CaptureStudioProbeAsync($"referrals-{width}.png");
                        if (!_managedReferralsCard.IsLoaded || _managedReferralsCard.ActualWidth<200)
                            throw new InvalidOperationException("Referral account card was not rendered.");
                    }
                    var buttons = Descendants(current).OfType<Button>()
                        .Where(b => b.ActualWidth > 0 && b.Visibility == Visibility.Visible).ToArray();
                    var bot = Descendants(_accountPage).OfType<HyperlinkButton>()
                        .FirstOrDefault(b => b.NavigateUri?.AbsoluteUri == "https://t.me/VideoGra_bot");
                    var authorLink = Descendants(_rootHost).OfType<HyperlinkButton>()
                        .Single(b => Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(b) == "AuthorTelegramLink");
                    var authorContent = (Grid)authorLink.Content;
                    var authorHandle = authorContent.Children.OfType<TextBlock>().Single();
                    var authorIcon = authorContent.Children.OfType<Image>().Single();
                    var authorHandleBounds = authorHandle.TransformToVisual(authorContent)
                        .TransformBounds(new Windows.Foundation.Rect(0, 0, authorHandle.ActualWidth, authorHandle.ActualHeight));
                    var authorHandleVisible = authorHandle.Visibility == Visibility.Visible;
                    var authorHandleFits = !authorHandleVisible || (!authorHandle.IsTextTrimmed
                        && authorHandle.DesiredSize.Width <= authorHandle.ActualWidth + 1
                        && authorHandleBounds.Right <= authorContent.ActualWidth + 1);
                    var authorContactAvailable = authorLink is { IsEnabled: true, IsTabStop: true, Visibility: Visibility.Visible }
                        && authorLink.ActualWidth >= 44 && authorLink.ActualHeight >= 44;
                    var authorIconLoaded = authorIcon.Source is BitmapImage { PixelWidth: > 0, PixelHeight: > 0 };
                    if (!authorHandleFits || !authorContactAvailable || !authorIconLoaded)
                        throw new InvalidOperationException("Author Telegram contact is clipped, unavailable or missing its official icon.");
                    var disclosures = Descendants(current).OfType<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>()
                        .Where(b => b.Content is string text && text.StartsWith("›  ", StringComparison.Ordinal)).ToArray();
                    foreach (var disclosure in disclosures)
                    {
                        disclosure.IsChecked = true;
                        if (!disclosure.Content.ToString()!.StartsWith("⌄  ", StringComparison.Ordinal))
                            throw new InvalidOperationException("Disclosure did not expand.");
                        disclosure.IsChecked = false;
                        if (!disclosure.Content.ToString()!.StartsWith("›  ", StringComparison.Ordinal))
                            throw new InvalidOperationException("Disclosure did not collapse.");
                    }
                    checks.Add(new
                    {
                        page, requestedWindowWidth = width, rootWidth = _rootHost.ActualWidth,
                        viewportWidth = current.ViewportWidth, extentWidth = current.ExtentWidth,
                        horizontalOverflow = current.ExtentWidth > current.ViewportWidth + 1,
                        bodyFont = StudioBodyFont.Source, headingFont = StudioHeadingFont.Source,
                        botClickable = bot is { IsEnabled: true, IsTabStop: true },
                        authorContactAvailable, authorHandleVisible, authorHandleFits, authorIconLoaded,
                        authorContactWidth = authorLink.ActualWidth,
                        disclosuresChecked = disclosures.Length,
                        selectedNav = _navigationItems.Where(x => Microsoft.UI.Xaml.Automation.AutomationProperties.GetItemStatus(x.Value) == "Текущий раздел").Select(x => x.Key).ToArray(),
                        buttons = buttons.Select(b => new { label = b.Content?.ToString(), width = b.ActualWidth, height = b.ActualHeight, b.IsTabStop }).ToArray()
                    });
                }
            }
            ApplyTheme(VideoGrabber.Infrastructure.Settings.AppThemeMode.Light, false);
            ShowPage("account");
            await Task.Delay(200);
            await CaptureStudioProbeAsync("account-light-640.png");
            await File.WriteAllTextAsync(Path.Combine(StudioProbeRoot, "result.json"), JsonSerializer.Serialize(new { ok = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(Path.Combine(StudioProbeRoot, "result.json"), JsonSerializer.Serialize(new { ok = false, error = ex.ToString(), checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { _allowWindowClose = true; Close(); }
    }

    private async Task CaptureStudioProbeAsync(string name)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(_rootHost);
        var buffer = await bitmap.GetPixelsAsync();
        using var reader = DataReader.FromBuffer(buffer);
        var pixels = new byte[buffer.Length];
        reader.ReadBytes(pixels);
        var path = Path.Combine(StudioProbeRoot, name);
        await File.WriteAllBytesAsync(path, []);
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
    }

    private void PrepareClientUpdateProbe(string scenario)
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        _activeClientServices = BootstrapClientServices;
        _clientReleaseVerifier = new VideoGrabber.Core.ClientUpdates.ClientReleaseVerifier(
            new Dictionary<string, string> { ["qa-only"] = key.ExportSubjectPublicKeyInfoPem() });
        _clientReleaseCache = new VideoGrabber.Infrastructure.ClientUpdates.ClientReleaseCache(
            Path.Combine(StudioProbeRoot, "qa-update-cache.json"), _clientReleaseVerifier, BootstrapClientServices);
        var now = DateTimeOffset.UtcNow;
        var services = scenario == "migration"
            ? new VideoGrabber.Platform.Contracts.ClientUpdates.ClientServiceEndpoints(new Uri("https://new.example.com/"), new Uri("https://new.example.com/web/"))
            : BootstrapClientServices;
        var notes = scenario == "long-note" ? string.Concat(Enumerable.Repeat("Улучшены загрузки и синхронизация. ", 30)) : "Улучшены загрузки и синхронизация.";
        var artifact = new VideoGrabber.Platform.Contracts.ClientUpdates.ClientUpdateArtifact("999.0.0", new Uri("https://updates.example.com/setup"), 1024, new string('a', 64));
        var manifest = new VideoGrabber.Platform.Contracts.ClientUpdates.ClientReleaseManifest(1, "videograbber", "preview", 1,
            now, now.AddDays(1), "videograbber-main", services, new("999.0.0", now, notes, artifact, null));
        var envelope = VideoGrabber.Core.ClientUpdates.ClientReleaseSigner.Sign(manifest, key, "qa-only");
        _clientReleaseCache.AcceptChecked(envelope);
        RenderClientReleaseState(manual: true);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}


public partial class App
{
    private async void StartNativeBaseline()
    {
        var root = Environment.GetEnvironmentVariable("VIDEOGRABBER_PRESENTATION_ROOT")!;
        File.AppendAllText(Path.Combine(root, "baseline.txt"), "starting\n");
        var panel = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Navy) };
        panel.Children.Add(new TextBlock { Text = "Native baseline", FontFamily = new FontFamily("Segoe UI"), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 28 });
        _window = new Window { Content = panel };
        _window.Activate();
        File.AppendAllText(Path.Combine(root, "baseline.txt"), "activated\n");
        await Task.Delay(1200);
        File.AppendAllText(Path.Combine(root, "baseline.txt"), "rendered\n");
        _window.Close();
    }
}
