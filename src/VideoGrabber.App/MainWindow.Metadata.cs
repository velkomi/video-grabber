using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private sealed record BrowserPlayerPayload(int ordinal, string? title, string? source);
    private sealed record BrowserMetadataPayload(string? pageTitle, string[]? sections, BrowserPlayerPayload[]? playerSlots);
    private BrowserPageMetadata _browserMetadata = BrowserPageMetadata.Empty;

    private async Task CaptureBrowserMetadataAsync(CoreWebView2 core)
    {
        const string script = """
            (() => {
              const clean = v => (v || '').replace(/\s+/g, ' ').trim();
              const ignored = new Set(['Поиск','Обучение','Служба поддержки','Покупки','Следующий урок','Предыдущий урок']);
              const nodes = [...document.querySelectorAll('h1,h2,h3,h4,.lesson-title,.part-title,.lesson-header,[class*=\"lesson-title\"],[class*=\"part-title\"]')];
              const sections = [];
              for (const node of nodes) {
                const text = clean(node.innerText || node.textContent);
                if (text.length < 2 || text.length > 120 || ignored.has(text) || sections.includes(text)) continue;
                sections.push(text);
                if (sections.length >= 40) break;
              }
              const isPlayer = frame => {
                try {
                  const u = new URL(frame.src, document.baseURI);
                  const host = u.hostname.toLowerCase();
                  const path = u.pathname.toLowerCase();
                  return path.includes('/sign-player/') && (host.endsWith('.gcvh.ru') || host.endsWith('.gcfiles.net') || host.endsWith('.getcourse.ru') || host.endsWith('.vhcdn.com'));
                } catch { return false; }
              };
              const headingSelector = 'h1,h2,h3,h4,.lesson-title,.part-title,.lesson-header,[class*=\"lesson-title\"],[class*=\"part-title\"]';
              const partPattern = /^(?:\u0427\u0430\u0441\u0442\u044c|part)\s*\u2116?\s*\d+(?:\b|[.: -])/i;
              const nearestPartTitle = frame => {
                const all = [...document.querySelectorAll('body *')];
                const frameIndex = all.indexOf(frame);
                for (let i = frameIndex - 1, scanned = 0; i >= 0 && scanned < 250; i--, scanned++) {
                  const node = all[i];
                  if (node.contains(frame) || node.children.length > 4) continue;
                  const text = clean(node.innerText || node.textContent);
                  if (text && text.length <= 80 && partPattern.test(text)) return text;
                }
                return '';
              };
              const nearestTitle = frame => {
                let node = frame;
                for (let depth = 0; node && depth < 8; depth++, node = node.parentElement) {
                  for (let sibling = node.previousElementSibling; sibling; sibling = sibling.previousElementSibling) {
                    const heading = sibling.matches?.(headingSelector) ? sibling : sibling.querySelector?.(headingSelector);
                    const text = clean(heading?.innerText || heading?.textContent);
                    if (text && text.length <= 120 && !ignored.has(text)) return text;
                  }
                }
                return '';
              };
              const playerSlots = [...document.querySelectorAll('iframe[src]')].filter(isPlayer)
                .map((frame, index) => ({ ordinal: index + 1, title: nearestPartTitle(frame) || nearestTitle(frame), source: frame.src }));
              return { pageTitle: clean(document.title), sections, playerSlots };
            })()
            """;
        try
        {
            var json = await core.ExecuteScriptAsync(script);
            var payload = JsonSerializer.Deserialize<BrowserMetadataPayload>(json);
            if (payload is null) return;
            var sections = (payload.sections ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Take(40).ToArray();
            var slots = (payload.playerSlots ?? [])
                .Where(slot => slot.ordinal > 0 && slot.ordinal <= 200)
                .Select(slot => new BrowserPlayerSlot(slot.ordinal, slot.title, SafePlayerUri(slot.source)))
                .ToArray();
            _browserMetadata = new BrowserPageMetadata(payload.pageTitle, sections, slots);
            foreach (var slot in slots)
                DiagnosticHub.Log.Write("browser.binding.slot", "observed",
                    $"ordinal={slot.Ordinal} source={BrowserBindingFingerprint.Describe(slot.Source)}");
            RebindAndReorderMediaCandidates();
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("browser.metadata", "failed", ex.GetType().Name);
        }
    }

    private static Uri? SafePlayerUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        return uri;
    }
    private int _bindingRefreshVersion;

    private void ScheduleBrowserBindingRefresh(CoreWebView2 core)
    {
        var version = Interlocked.Increment(ref _bindingRefreshVersion);
        _ = RefreshBrowserBindingsAfterDelayAsync(core, version);
    }

    private async Task RefreshBrowserBindingsAfterDelayAsync(CoreWebView2 core, int version)
    {
        await Task.Delay(350);
        if (version != Volatile.Read(ref _bindingRefreshVersion) || _mediaBrowser?.CoreWebView2 != core) return;
        await RefreshBrowserBindingsAsync(core);
    }

    private async Task RefreshBrowserBindingsAsync(CoreWebView2 core)
    {
        await RefreshBrowserFrameTreeAsync(core);
        await CaptureBrowserMetadataAsync(core);
    }

    private async Task<MediaCandidate> RefreshCandidateBindingAsync(MediaCandidate candidate)
    {
        if (_mediaBrowser?.CoreWebView2 is { } core) await RefreshBrowserBindingsAsync(core);
        var current = _mediaCandidateItems.TryGetValue(candidate.Source.AbsoluteUri, out var item)
            && item.Tag is MediaCandidate latest ? latest : candidate;
        var all = _mediaCandidatesBox.Items.OfType<ComboBoxItem>()
            .Select(entry => entry.Tag as MediaCandidate)
            .Where(entry => entry is not null)
            .Cast<MediaCandidate>()
            .ToList();
        if (!all.Any(entry => string.Equals(entry.Source.AbsoluteUri, current.Source.AbsoluteUri, StringComparison.Ordinal)))
            all.Add(current);
        var bound = BrowserFrameBindingResolver.BindAll(all, _browserFrames, _browserMetadata);
        return bound.FirstOrDefault(entry => string.Equals(entry.Source.AbsoluteUri, current.Source.AbsoluteUri, StringComparison.Ordinal)) ?? current;
    }
    private void RefreshMediaCandidateLabels()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            for (var i = 0; i < _mediaCandidatesBox.Items.Count; i++)
            {
                if (_mediaCandidatesBox.Items[i] is ComboBoxItem { Tag: MediaCandidate candidate } item)
                    item.Content = MediaCandidatePresentation.DisplayName(candidate, i + 1, _browserMetadata);
            }
        });
    }
}
