using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Licensing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private bool _updatingMediaQuality;
    private bool _queueRunnerActive;

    private async Task<OperationOutcome> DownloadCandidateAsync(MediaCandidate candidate, int ordinal,
        bool resetCookieSelectionAfterUse = true, string? qualityOverride = null)
    {
        var intent = CaptureDownloadIntent(candidate.Source, qualityOverride ?? SelectedBrowserQuality(candidate));
        return await RunDownloadOperationAsync(intent, new BrowserDownloadPreparation(this, candidate, ordinal), resetCookieSelectionAfterUse, managedKind: "browser_candidate");
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
        _browserDownloadQueue.AddOrUpdate(candidate, ordinal, SelectedBrowserQuality(candidate), CaptureQueueContext(candidate));
        PersistManagedQueueSnapshot();
        RefreshDownloadQueueList();
    }

    private void QueueAllVisibleCandidates()
    {
        foreach (var (item, index) in _mediaCandidatesBox.Items.OfType<ComboBoxItem>().Select((item, index) => (item, index)))
        {
            if (item.Tag is not MediaCandidate candidate) continue;
            var ordinal = candidate.PageOrdinal ?? index + 1;
            _browserDownloadQueue.AddOrUpdate(candidate, ordinal, SelectedBrowserQuality(candidate), CaptureQueueContext(candidate));
        }
        PersistManagedQueueSnapshot();
        RefreshDownloadQueueList();
    }

    private void MoveQueuedCandidate(int delta)
    {
        var index = _downloadQueueList.SelectedIndex;
#if VIDEOGRABBER_MANAGED
        if ((_downloadQueueList.SelectedItem as ListViewItem)?.Tag is SavedQueueItem)
        {
            MoveManagedRestoredQueue(index, delta);
            return;
        }
#endif
        if (!_browserDownloadQueue.Move(index, delta)) return;
        PersistManagedQueueSnapshot();
        RefreshDownloadQueueList(Math.Clamp(index + delta, 0, _browserDownloadQueue.Items.Count - 1));
    }

    private void RemoveQueuedCandidate()
    {
        var index = _downloadQueueList.SelectedIndex;
#if VIDEOGRABBER_MANAGED
        if ((_downloadQueueList.SelectedItem as ListViewItem)?.Tag is SavedQueueItem)
        {
            RemoveManagedRestoredQueue(index);
            return;
        }
#endif
        if (!_browserDownloadQueue.RemoveAt(index)) return;
        PersistManagedQueueSnapshot();
        RefreshDownloadQueueList(Math.Min(index, _browserDownloadQueue.Items.Count - 1));
    }

    private void RefreshDownloadQueueList(int selectedIndex = -1)
    {
        if (_downloadQueueList is null) return;
        _downloadQueueList.Items.Clear();
        foreach (var entry in _browserDownloadQueue.Items)
        {
            var label = MediaCandidatePresentation.DisplayName(entry.Candidate, entry.Ordinal, entry.Context?.Metadata ?? BrowserPageMetadata.Empty);
            var status = entry.Context?.SessionEpoch == _browserSessionEpoch ? "" : "  •  Требуется повторный выбор сессии";
            _downloadQueueList.Items.Add(new ListViewItem
            {
                Content = $"{label}  •  {entry.Quality}{status}",
                Tag = entry
            });
        }
#if VIDEOGRABBER_MANAGED
        if (_browserDownloadQueue.Items.Count == 0 && _managedRestoredQueue.Items.Length > 0)
        {
            foreach (var saved in _managedRestoredQueue.Items)
                _downloadQueueList.Items.Add(new ListViewItem
                {
                    Content = $"Пункт {saved.Ordinal}  •  {saved.Quality}  •  needs_revalidation",
                    Tag = saved
                });
        }
#endif
        if (_downloadQueueList.Items.Count > 0)
            _downloadQueueList.SelectedIndex = selectedIndex >= 0
                ? Math.Min(selectedIndex, _downloadQueueList.Items.Count - 1)
                : 0;
    }

    private BrowserQueueContext CaptureQueueContext(MediaCandidate candidate)
        => new(_browserPageUri ?? candidate.Referer, _browserSessionEpoch, _browserMetadata)
        {
            CookieSelection = (_cookiesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
        };

    private async Task DownloadQueuedCandidatesAsync()
    {
        if (_windowLifetime.IsCancellationRequested) return;
        if (_operations.IsBusy)
        {
            _operations.RequestQueue();
            _browserHint.Text = "\u041e\u0447\u0435\u0440\u0435\u0434\u044c \u0437\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u0441\u044f \u0430\u0432\u0442\u043e\u043c\u0430\u0442\u0438\u0447\u0435\u0441\u043a\u0438 \u043f\u043e\u0441\u043b\u0435 \u0442\u0435\u043a\u0443\u0449\u0435\u0439 \u0437\u0430\u0433\u0440\u0443\u0437\u043a\u0438.";
            return;
        }
        if (_queueRunnerActive) return;
#if VIDEOGRABBER_MANAGED
        if (_browserDownloadQueue.Items.Count == 0 && _managedRestoredQueue.Items.Length > 0)
        {
            if (!await TryRevalidateManagedQueueAsync()) return;
        }
#endif
        if (_browserDownloadQueue.Items.Count == 0)
        {
            _browserHint.Text = "Очередь пуста.";
            return;
        }
        var selectedCookies = (_cookiesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var navigationVersion = _queueNavigationVersion;
        _queueRunnerActive = true;
        try
        {
            var completed = 0;
            while (_browserDownloadQueue.Items.Count > 0)
            {
                if (navigationVersion != _queueNavigationVersion) return;
                var entry = _browserDownloadQueue.Items[0];
                if (entry.Context?.SessionEpoch != _browserSessionEpoch)
                {
                    _browserHint.Text = "Требуется повторный выбор сессии. Пункт сохранён; выберите сессию и явно добавьте видео заново.";
                    RefreshDownloadQueueList();
                    return;
                }
                PersistManagedQueueSnapshot(entry, "failed");
                _browserHint.Text = $"\u041E\u0447\u0435\u0440\u0435\u0434\u044C: \u0441\u043A\u0430\u0447\u0430\u043D\u043E {completed}. \u041E\u0441\u0442\u0430\u043B\u043E\u0441\u044C: {_browserDownloadQueue.Items.Count}.";
                var intent = CaptureDownloadIntent(entry.Candidate.Source, entry.Quality) with
                {
                    AudioOnly = ManagedIntent(entry).OutputMode == "audio",
                    SessionEpoch = entry.Context.SessionEpoch,
                    CookieSelection = entry.Context.CookieSelection
                };
                PersistManagedQueueSnapshot(entry, "running");
                var outcome = await RunDownloadOperationAsync(intent,
                    new BrowserDownloadPreparation(this, entry.Candidate, entry.Ordinal, entry.Context),
                    resetCookieSelectionAfterUse: false, queuedEntry: entry, managedKind: "queue_selected");
                if (navigationVersion != _queueNavigationVersion) return;
                if (outcome == OperationOutcome.Succeeded)
                {
                    completed++;
                    _browserDownloadQueue.RemoveBySource(entry.Candidate.Source);
                    PersistManagedQueueSnapshot();
                    RefreshDownloadQueueList();
                    continue;
                }
                if (outcome == OperationOutcome.Cancelled)
                {
                    PersistManagedQueueSnapshot();
                    _browserHint.Text = "\u041E\u0447\u0435\u0440\u0435\u0434\u044C \u043E\u0441\u0442\u0430\u043D\u043E\u0432\u043B\u0435\u043D\u0430. \u041D\u0435\u0432\u044B\u043F\u043E\u043B\u043D\u0435\u043D\u043D\u044B\u0435 \u043F\u0443\u043D\u043A\u0442\u044B \u0441\u043E\u0445\u0440\u0430\u043D\u0435\u043D\u044B \u2014 \u043D\u0430\u0436\u043C\u0438\u0442\u0435 \u00AB\u0421\u043A\u0430\u0447\u0430\u0442\u044C \u043E\u0447\u0435\u0440\u0435\u0434\u044C / \u043F\u0440\u043E\u0434\u043E\u043B\u0436\u0438\u0442\u044C\u00BB.";
                    return;
                }
                PersistManagedQueueSnapshot(entry, "failed");
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
            if (_browserDownloadQueue.Items.Count == 0 && BrowserDownloadSessionPolicy.ShouldResetAfterUse(selectedCookies))
                _browserSession.RunProgrammaticSelectionCleanup(() => _cookiesBox.SelectedIndex = 0);
        }
    }

    private void PersistManagedQueueSnapshot(BrowserDownloadQueueItem? active = null, string activeState = "pending")
    {
#if VIDEOGRABBER_MANAGED
        if (_managedAccountId is not Guid accountId || accountId == Guid.Empty) return;
        var items = _browserDownloadQueue.Items.Select(entry =>
        {
            var page = entry.Context?.Page ?? entry.Candidate.Referer;
            var state = ReferenceEquals(entry, active) ? activeState : "pending";
            return new SavedQueueItem(
                ManagedIntentId(entry),
                page.AbsoluteUri,
                ManagedMediaIdentity(entry.Candidate, entry.Ordinal),
                entry.Ordinal,
                entry.Quality,
                "video",
                state);
        }).ToArray();
        _ = PersistManagedQueueSnapshotAsync(new SavedQueue(1, accountId, items));
#endif
    }

    private static string ManagedMediaIdentity(MediaCandidate candidate, int ordinal)
        => HashManagedRequest(string.Join("\n",
            candidate.Kind,
            candidate.FrameId ?? string.Empty,
            candidate.PageSectionTitle ?? string.Empty,
            candidate.PageOrdinal ?? ordinal));
#if VIDEOGRABBER_MANAGED
    private async Task PersistManagedQueueSnapshotAsync(SavedQueue queue)
    {
        await _managedQueueWriteLock.WaitAsync(_windowLifetime.Token);
        try
        {
            await _managedQueueStore.SaveAsync(queue, _windowLifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() => _browserHint.Text = "Не удалось сохранить очередь: " + ex.Message);
        }
        finally { _managedQueueWriteLock.Release(); }
    }

    private async Task RestoreManagedQueueAsync(Guid accountId)
    {
        _managedRestoredQueue = await _managedQueueStore.LoadAsync(accountId, _windowLifetime.Token);
        DispatcherQueue.TryEnqueue(() => RefreshDownloadQueueList());
    }
    private void MoveManagedRestoredQueue(int index, int delta)
    {
        var items = _managedRestoredQueue.Items.ToList();
        var target = index + delta;
        if (index < 0 || index >= items.Count || target < 0 || target >= items.Count) return;
        (items[index], items[target]) = (items[target], items[index]);
        _managedRestoredQueue = _managedRestoredQueue with { Items = items.ToArray() };
        _ = PersistManagedRestoredQueueAsync();
        RefreshDownloadQueueList(target);
    }

    private void RemoveManagedRestoredQueue(int index)
    {
        var items = _managedRestoredQueue.Items.ToList();
        if (index < 0 || index >= items.Count) return;
        items.RemoveAt(index);
        _managedRestoredQueue = _managedRestoredQueue with { Items = items.ToArray() };
        _ = PersistManagedRestoredQueueAsync();
        RefreshDownloadQueueList(Math.Min(index, items.Count - 1));
    }
    private async Task PersistManagedRestoredQueueAsync()
    {
        await _managedQueueWriteLock.WaitAsync(_windowLifetime.Token);
        try { await _managedQueueStore.SaveAsync(_managedRestoredQueue, _windowLifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() => _browserHint.Text = "Не удалось сохранить очередь: " + ex.Message);
        }
        finally { _managedQueueWriteLock.Release(); }
    }
    private async Task<bool> TryRevalidateManagedQueueAsync()
    {
        if (_managedRestoredQueue.Items.Length == 0) return true;
        if (_browserPageUri is null || _mediaCandidatesBox.Items.Count == 0)
        {
            _browserHint.Text = "Откройте исходную страницу, запустите видео и снова нажмите «Скачать очередь / продолжить».";
            return false;
        }
        var page = ManagedPageOriginPath(_browserPageUri);
        if (_managedRestoredQueue.Items.Any(x => !string.Equals(x.PageOriginPath, page, StringComparison.Ordinal)))
        {
            _browserHint.Text = "Сохранённая очередь относится к другой странице. Откройте исходную страницу и повторите продолжение.";
            return false;
        }
        var current = _mediaCandidatesBox.Items.OfType<ComboBoxItem>()
            .Select((item, index) => (Candidate: item.Tag as MediaCandidate, Ordinal: index + 1))
            .Where(x => x.Candidate is not null)
            .Select(x => (Candidate: x.Candidate!, Ordinal: x.Candidate!.PageOrdinal ?? x.Ordinal))
            .ToArray();
        var matches = new List<(SavedQueueItem Saved, MediaCandidate Candidate, int Ordinal)>();
        foreach (var saved in _managedRestoredQueue.Items)
        {
            var match = current.FirstOrDefault(x =>
                string.Equals(ManagedMediaIdentity(x.Candidate, x.Ordinal), saved.MediaIdentity, StringComparison.Ordinal));
            if (match.Candidate is null || !CandidateSupportsSavedQuality(match.Candidate, saved.Quality))
            {
                _browserHint.Text = $"Пункт {saved.Ordinal} не совпал с текущим видео/качеством. Выберите его заново — списание не выполнено.";
                return false;
            }
            matches.Add((saved, match.Candidate, match.Ordinal));
        }
        foreach (var match in matches)
        {
            _browserDownloadQueue.AddOrUpdate(match.Candidate, match.Ordinal, match.Saved.Quality, CaptureQueueContext(match.Candidate));
            var live = _browserDownloadQueue.Items.First(x =>
                string.Equals(x.Candidate.Source.AbsoluteUri, match.Candidate.Source.AbsoluteUri, StringComparison.Ordinal));
            BindManagedIntent(live, match.Saved.IntentId, match.Saved.OutputMode);
        }
        _managedRestoredQueue = new SavedQueue(1, _managedAccountId ?? Guid.Empty, []);
        PersistManagedQueueSnapshot();
        RefreshDownloadQueueList();
        _browserHint.Text = "Очередь перепроверена по свежим данным. Запускаю только совпавшие пункты.";
        await Task.CompletedTask;
        return true;
    }
    private static string ManagedPageOriginPath(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Managed queue page must use HTTPS without user info.");
        var clean = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri.AbsoluteUri;
        return clean.EndsWith('/') && uri.AbsolutePath != "/" ? clean.TrimEnd('/') : clean;
    }

    private static bool CandidateSupportsSavedQuality(MediaCandidate candidate, string quality)
    {
        if (!DownloadQuality.TryParse(quality, out var parsed)) return false;
        if (parsed.MaximumHeight is null) return true;
        if (candidate.HlsManifest is not { IsMaster: true } manifest) return false;
        return manifest.Variants.Any(v => v.Height is > 0 && v.Height.Value <= parsed.MaximumHeight.Value);
    }
#endif
    private async Task DownloadAllVisibleCandidatesAsync()
    {
        if (_operations.IsBusy) return;
        QueueAllVisibleCandidates();
        if (_browserDownloadQueue.Items.Count == 0)
        {
            _browserHint.Text = "Сначала дождитесь обнаружения видео.";
            return;
        }
        await DownloadQueuedCandidatesAsync();
    }
}
