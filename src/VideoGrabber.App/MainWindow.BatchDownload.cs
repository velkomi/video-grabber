using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private bool _updatingMediaQuality;
    private bool _queueRunRequested;
    private bool _queueRunnerActive;

    private async Task<DownloadAttemptOutcome> DownloadCandidateAsync(
        MediaCandidate candidate,
        int ordinal,
        bool resetCookieSelectionAfterUse = true,
        string? qualityOverride = null)
    {
        var intent = CaptureDownloadIntent(candidate.Source, qualityOverride ?? SelectedBrowserQuality(candidate));
        return await RunDownloadOperationAsync(intent, async (operationToken, routeScope) =>
        {
            EnsureIntentSession(intent, operationToken);
            candidate = await RefreshCandidateBindingAsync(candidate);
            EnsureIntentSession(intent, operationToken);
            ordinal = candidate.PageOrdinal ?? ordinal;
            var plan = MediaDownloadPlanResolver.Resolve(candidate, intent.Quality, intent.AudioOnly);
            using var preflight = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
            preflight.CancelAfter(TimeSpan.FromSeconds(60));
            return await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
                async () =>
                {
                    EnsureIntentSession(intent, operationToken);
                    var verified = await EnsureSelectedHlsVerifiedAsync(candidate, plan, preflight.Token, routeScope);
                    EnsureIntentSession(intent, operationToken);
                    return verified;
                },
                () =>
                {
                    EnsureIntentSession(intent, operationToken);
                    var suggested = MediaCandidatePresentation.SuggestedBaseName(candidate, ordinal, intent.Quality, _browserMetadata);
                    var duration = candidate.HlsManifest?.DurationSeconds;
                    var expectedDuration = duration is > 0 && double.IsFinite(duration.Value) ? duration : null;
                    bool? expectedAudio = intent.AudioOnly || plan.HlsAudioSource is not null ? true : null;
                    var selected = new PreparedDownload(plan.Source, candidate.Referer, null, null, null,
                        plan.HlsVideoSource, plan.HlsAudioSource, plan.DirectManifest, plan.ResolvedHlsLeaf,
                        suggested, expectedDuration, expectedAudio);
                    return DownloadPreparedSourceAsync(intent, selected, operationToken, routeScope);
                },
                error =>
                {
                    if (error is not null) _browserHint.Text = error;
                    return DownloadAttemptOutcome.Failed;
                });
        }, resetCookieSelectionAfterUse);
    }
    private string SelectedBrowserQuality(MediaCandidate candidate)
    {
        if (_mediaQualitySelections.TryGetValue(candidate.Source.AbsoluteUri, out var saved)) return saved;
        return "best";
    }

    private void SyncMediaQualityChoices(string? selectedQuality = null)
    {
        if (_mediaQualityBox is null) return;
        var wasUpdating = _updatingMediaQuality;
        _updatingMediaQuality = true;
        try
        {
            _mediaQualityBox.Items.Clear();
            _mediaQualityBox.Items.Add(ComboItem("Лучшее доступное", "best"));
            if ((_mediaCandidatesBox.SelectedItem as ComboBoxItem)?.Tag is MediaCandidate candidate
                && candidate.HlsManifest is { IsMaster: true } manifest)
            {
                foreach (var height in manifest.Variants.Where(v => v.Height is > 0)
                    .Select(v => v.Height!.Value).Distinct().OrderDescending())
                    _mediaQualityBox.Items.Add(ComboItem(height + "p", height + "p"));
                var saved = selectedQuality ?? _mediaQualitySelections.GetValueOrDefault(candidate.Source.AbsoluteUri, "best");
                _mediaQualityBox.SelectedIndex = _mediaQualityBox.Items.OfType<ComboBoxItem>()
                    .Select((item, index) => (item, index))
                    .FirstOrDefault(x => string.Equals(x.item.Tag?.ToString(), saved, StringComparison.Ordinal)).index;
            }
            if (_mediaQualityBox.SelectedIndex < 0) _mediaQualityBox.SelectedIndex = 0;
        }
        finally { _updatingMediaQuality = wasUpdating; }
    }

    private void StoreSelectedMediaQuality()
    {
        if (_updatingMediaQuality) return;
        if ((_mediaCandidatesBox.SelectedItem as ComboBoxItem)?.Tag is not MediaCandidate candidate) return;
        var quality = (_mediaQualityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best";
        _mediaQualitySelections[candidate.Source.AbsoluteUri] = quality;
    }

    private void QueueSelectedCandidate()
    {
        if ((_mediaCandidatesBox.SelectedItem as ComboBoxItem)?.Tag is not MediaCandidate candidate)
        {
            _browserHint.Text = "Выберите видео для очереди.";
            return;
        }
        var ordinal = candidate.PageOrdinal ?? Math.Max(1, _mediaCandidatesBox.SelectedIndex + 1);
        _browserDownloadQueue.AddOrUpdate(candidate, ordinal, SelectedBrowserQuality(candidate));
        RefreshDownloadQueueList();
    }

    private void QueueAllVisibleCandidates()
    {
        foreach (var (item, index) in _mediaCandidatesBox.Items.OfType<ComboBoxItem>().Select((item, index) => (item, index)))
        {
            if (item.Tag is not MediaCandidate candidate) continue;
            var ordinal = candidate.PageOrdinal ?? index + 1;
            _browserDownloadQueue.AddOrUpdate(candidate, ordinal, SelectedBrowserQuality(candidate));
        }
        RefreshDownloadQueueList();
    }

    private void MoveQueuedCandidate(int delta)
    {
        var index = _downloadQueueList.SelectedIndex;
        if (!_browserDownloadQueue.Move(index, delta)) return;
        RefreshDownloadQueueList(Math.Clamp(index + delta, 0, _browserDownloadQueue.Items.Count - 1));
    }

    private void RemoveQueuedCandidate()
    {
        var index = _downloadQueueList.SelectedIndex;
        if (!_browserDownloadQueue.RemoveAt(index)) return;
        RefreshDownloadQueueList(Math.Min(index, _browserDownloadQueue.Items.Count - 1));
    }

    private void RefreshDownloadQueueList(int selectedIndex = -1)
    {
        if (_downloadQueueList is null) return;
        _downloadQueueList.Items.Clear();
        foreach (var entry in _browserDownloadQueue.Items)
        {
            var label = MediaCandidatePresentation.DisplayName(entry.Candidate, entry.Ordinal, _browserMetadata);
            _downloadQueueList.Items.Add(new ListViewItem
            {
                Content = $"{label}  •  {entry.Quality}",
                Tag = entry
            });
        }
        if (_downloadQueueList.Items.Count > 0)
            _downloadQueueList.SelectedIndex = selectedIndex >= 0
                ? Math.Min(selectedIndex, _downloadQueueList.Items.Count - 1)
                : 0;
    }

    private void SyncQueuedCandidate(MediaCandidate candidate)
    {
        var queued = _browserDownloadQueue.Items.FirstOrDefault(item =>
            string.Equals(item.Candidate.Source.AbsoluteUri, candidate.Source.AbsoluteUri, StringComparison.Ordinal));
        if (queued is null) return;
        _browserDownloadQueue.AddOrUpdate(candidate, candidate.PageOrdinal ?? queued.Ordinal, queued.Quality);
        RefreshDownloadQueueList();
    }

    private async Task DownloadQueuedCandidatesAsync()
    {
        if (_operation is not null)
        {
            _queueRunRequested = true;
            _browserHint.Text = "\u041e\u0447\u0435\u0440\u0435\u0434\u044c \u0437\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u0441\u044f \u0430\u0432\u0442\u043e\u043c\u0430\u0442\u0438\u0447\u0435\u0441\u043a\u0438 \u043f\u043e\u0441\u043b\u0435 \u0442\u0435\u043a\u0443\u0449\u0435\u0439 \u0437\u0430\u0433\u0440\u0443\u0437\u043a\u0438.";
            return;
        }
        if (_queueRunnerActive) return;
        if (_browserDownloadQueue.Items.Count == 0)
        {
            _browserHint.Text = "Очередь пуста.";
            return;
        }
        var selectedCookies = (_cookiesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _queueRunnerActive = true;
        _queueRunRequested = false;
        try
        {
            var completed = 0;
            while (_browserDownloadQueue.Items.Count > 0)
            {
                var entry = _browserDownloadQueue.Items[0];
                _browserHint.Text = $"\u041E\u0447\u0435\u0440\u0435\u0434\u044C: \u0441\u043A\u0430\u0447\u0430\u043D\u043E {completed}. \u041E\u0441\u0442\u0430\u043B\u043E\u0441\u044C: {_browserDownloadQueue.Items.Count}.";
                var outcome = await DownloadCandidateAsync(entry.Candidate, entry.Ordinal,
                    resetCookieSelectionAfterUse: false, qualityOverride: entry.Quality);
                if (outcome == DownloadAttemptOutcome.Succeeded)
                {
                    completed++;
                    _browserDownloadQueue.RemoveBySource(entry.Candidate.Source);
                    RefreshDownloadQueueList();
                    continue;
                }
                if (outcome == DownloadAttemptOutcome.Cancelled)
                {
                    _browserHint.Text = "\u041E\u0447\u0435\u0440\u0435\u0434\u044C \u043E\u0441\u0442\u0430\u043D\u043E\u0432\u043B\u0435\u043D\u0430. \u041D\u0435\u0432\u044B\u043F\u043E\u043B\u043D\u0435\u043D\u043D\u044B\u0435 \u043F\u0443\u043D\u043A\u0442\u044B \u0441\u043E\u0445\u0440\u0430\u043D\u0435\u043D\u044B \u2014 \u043D\u0430\u0436\u043C\u0438\u0442\u0435 \u00AB\u0421\u043A\u0430\u0447\u0430\u0442\u044C \u043E\u0447\u0435\u0440\u0435\u0434\u044C / \u043F\u0440\u043E\u0434\u043E\u043B\u0436\u0438\u0442\u044C\u00BB.";
                    return;
                }
                _browserHint.Text = "\u0417\u0430\u0433\u0440\u0443\u0437\u043A\u0430 \u043F\u0443\u043D\u043A\u0442\u0430 \u043D\u0435 \u0437\u0430\u0432\u0435\u0440\u0448\u0435\u043D\u0430. \u041E\u043D \u043E\u0441\u0442\u0430\u0432\u043B\u0435\u043D \u0432 \u043E\u0447\u0435\u0440\u0435\u0434\u0438 \u0434\u043B\u044F \u043F\u043E\u0432\u0442\u043E\u0440\u043D\u043E\u0439 \u043F\u043E\u043F\u044B\u0442\u043A\u0438.";
                return;
            }
            _browserHint.Text = _browserDownloadQueue.Items.Count == 0
                ? "Очередь завершена. Все пункты скачаны."
                : $"Проход очереди завершён. Для повтора осталось: {_browserDownloadQueue.Items.Count}.";
        }
        finally
        {
            _queueRunnerActive = false;
            if (BrowserDownloadSessionPolicy.ShouldResetAfterUse(selectedCookies)) _cookiesBox.SelectedIndex = 0;
        }
    }

    private async Task DownloadAllVisibleCandidatesAsync()
    {
        if (_operation is not null) return;
        QueueAllVisibleCandidates();
        if (_browserDownloadQueue.Items.Count == 0)
        {
            _browserHint.Text = "Сначала дождитесь обнаружения видео.";
            return;
        }
        await DownloadQueuedCandidatesAsync();
    }
}
