using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Infrastructure.Licensing;
using VideoGrabber.Platform.Contracts;

using VideoGrabber.Core.Licensing;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
#if VIDEOGRABBER_MANAGED
    private TextBlock _accountStatus = null!;
    private TextBlock _accountProfileText = null!;
    private TextBlock _accountAccessText = null!;
    private TextBlock _accountProvidersText = null!;
    private StackPanel _accountIdentitiesPanel = null!;
    private StackPanel _accountDevicesPanel = null!;
    private bool _accountBusy;
    private bool _accountStartupAttempted;

    private ScrollViewer BuildAccountPage()
    {
        var body = PageStack();
        body.Children.Add(PageHeading("Аккаунт и доступ",
            "Вход открывается только в системном браузере. Сессия хранится через Windows DPAPI."));
        var auth = Vertical(10);
        auth.Children.Add(SectionHeading("Вход / привязка способа входа"));
        auth.Children.Add(MutedText("До входа кнопка открывает сессию. После входа — безопасно привязывает дополнительный способ через отдельный PKCE/challenge flow. Media WebView2 не используется."));
        auth.Children.Add(Horizontal(
            ProviderButton("Google", "google"),
            ProviderButton("Apple", "apple"),
            ProviderButton("Яндекс", "yandex")));
        auth.Children.Add(Horizontal(
            ProviderButton("Telegram", "telegram"),
            ProviderButton("Почта", "email")));
        var refresh = SecondaryButton("Обновить данные");
        refresh.Click += async (_, _) => await LoadManagedAccountAsync();
        var signOut = SecondaryButton("Выйти");
        signOut.Click += async (_, _) => await SignOutManagedAccountAsync();
        auth.Children.Add(Horizontal(refresh, signOut));
        _accountStatus = MutedText("Проверяю сохранённую сессию…");
        auth.Children.Add(_accountStatus);
        body.Children.Add(Card(auth));

        _accountProfileText = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        _accountProvidersText = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var profile = Vertical(8);
        profile.Children.Add(SectionHeading("Профиль"));
        profile.Children.Add(_accountProfileText);
        profile.Children.Add(_accountProvidersText);
        _accountIdentitiesPanel = Vertical(6);
        profile.Children.Add(_accountIdentitiesPanel);
        body.Children.Add(Card(profile));

        _accountAccessText = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var access = Vertical(8);
        access.Children.Add(SectionHeading("Доступ"));
        access.Children.Add(_accountAccessText);
        body.Children.Add(Card(access));
        body.Children.Add(BuildManagedPaymentsCard());

        _accountDevicesPanel = Vertical(8);
        _accountDevicesPanel.Children.Add(SectionHeading("Компьютеры"));
        _accountDevicesPanel.Children.Add(MutedText("После входа здесь появятся зарегистрированные Windows-устройства."));
        body.Children.Add(Card(_accountDevicesPanel));
        body.Children.Add(BuildDesktopWorkerCard());

        var view = new ScrollViewer { Content = body };
        view.Loaded += (_, _) => StartManagedAccountRestore();
        return view;
    }

    private void StartManagedAccountRestore()
    {
        if (_accountStartupAttempted) return;
        _accountStartupAttempted = true;
        _ = InitializeManagedAccountAsync();
    }
    private Button ProviderButton(string title, string provider)
    {
        var button = SecondaryButton(title);
        button.Click += async (_, _) =>
        {
            if (_managedAccountId is not null && !string.IsNullOrWhiteSpace(_managedAccessToken))
                await LinkManagedProviderAsync(provider);
            else
                await SignInProviderAsync(provider);
        };
        return button;
    }

    private async Task InitializeManagedAccountAsync()
    {
        if (_accountBusy) return;
        _accountBusy = true;
        try
        {
            await TryRestoreOfflineStateAsync();
            var lifecycle = new SystemBrowserSignIn(_managedHttp, "email",
                timeout: TimeSpan.FromMinutes(5), sessionStore: _managedSessionStore);
            var session = await lifecycle.RefreshAsync(_windowLifetime.Token);
            _managedAccessToken = session.AccessToken;
            RebuildManagedCoordinator();
            await LoadManagedAccountAsync();
        }
        catch (UnauthorizedAccessException)
        {
            SetManagedSignedOut("Войдите в аккаунт, чтобы использовать Managed-функции.");
        }
        catch (HttpRequestException ex)
        {
            SetAccountStatus("Сервер сейчас недоступен. Подписанный офлайн-доступ, если он ещё действителен, сохранён. " + ex.Message);
        }
        catch (Exception ex)
        {
            SetAccountStatus("Не удалось восстановить аккаунт: " + ex.Message);
        }
        finally { _accountBusy = false; }
    }

    private async Task SignInProviderAsync(string provider)
    {
        if (_accountBusy) return;
        _accountBusy = true;
        SetAccountStatus("Открываю системный браузер для входа…");
        try
        {
            var signIn = new SystemBrowserSignIn(_managedHttp, provider,
                timeout: TimeSpan.FromMinutes(5), sessionStore: _managedSessionStore);
            var session = await signIn.SignInAsync(_windowLifetime.Token);
            _managedAccessToken = session.AccessToken;
            RebuildManagedCoordinator();
            await LoadManagedAccountAsync();
        }
        catch (OperationCanceledException) { SetAccountStatus("Вход отменён."); }
        catch (Exception ex) { SetAccountStatus("Вход не выполнен: " + ex.Message); }
        finally { _accountBusy = false; }
    }

    private async Task SignOutManagedAccountAsync()
    {
        if (_accountBusy) return;
        _accountBusy = true;
        try
        {
            var lifecycle = new SystemBrowserSignIn(_managedHttp, "email",
                timeout: TimeSpan.FromMinutes(5), sessionStore: _managedSessionStore);
            await lifecycle.SignOutAsync(_windowLifetime.Token);
        }
        catch (HttpRequestException ex)
        {
            SetAccountStatus("Сервер не подтвердил выход: " + ex.Message);
            return;
        }
        finally { _accountBusy = false; }

        _managedAccessToken = null;
        _managedAccountId = null;
        _managedDeviceId = null;
        _managedOfflineCache = null;
        StopDesktopWorkerLoop();
        _managedRestoredQueue = new SavedQueue(1, Guid.Empty, []);
        RebuildManagedCoordinator();
        SetManagedSignedOut("Вы вышли из аккаунта. Локальные файлы не удалены.");
        RefreshDownloadQueueList();
    }

    private void SetManagedSignedOut(string message)
    {
        SetAccountStatus(message);
        AccountUi(() =>
        {
            _accountProfileText.Text = "Нет активной сессии.";
            _accountProvidersText.Text = "Привязанные способы входа недоступны без сессии.";
            _accountAccessText.Text = "Managed-операции заблокированы до входа.";
            RenderManagedIdentities([]);
            RenderManagedDevices([]);
        });
    }

    private async Task LoadManagedAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(_managedAccessToken))
        {
            SetManagedSignedOut("Сначала войдите в аккаунт.");
            return;
        }

        try
        {
            SetAccountStatus("Обновляю профиль и права доступа…");
            var profile = await ManagedGetAsync<AccountProfile>("/v1/me", _windowLifetime.Token);
            var access = await ManagedGetAsync<AccessSnapshot>("/v1/access", _windowLifetime.Token);
            var devices = await ManagedGetAsync<DeviceReceipt[]>("/v1/devices", _windowLifetime.Token);
            var identities = await ManagedGetAsync<LinkedIdentity[]>("/v1/identities", _windowLifetime.Token);
            _managedAccountId = profile.AccountId;

            await EnsureManagedDeviceAndLeaseAsync(profile, access, devices);
            devices = await ManagedGetAsync<DeviceReceipt[]>("/v1/devices", _windowLifetime.Token);
            await RestoreManagedQueueAsync(profile.AccountId);
            RebuildManagedCoordinator();
            StartDesktopWorkerIfEnrolled();

            AccountUi(() =>
            {
                _accountProfileText.Text = $"ID: {profile.AccountId:D}\nРоль: {profile.Role}\nБлокировка: {(profile.Blocked ? "да" : "нет")}";
                _accountProvidersText.Text = "Привязанные способы входа: " +
                    (profile.LinkedProviders.Length == 0 ? "нет" : string.Join(", ", profile.LinkedProviders)) +
                    $"\nTelegram: {(profile.LinkedProviders.Contains("telegram", StringComparer.OrdinalIgnoreCase) ? "привязан" : "не привязан")}";
                RenderManagedIdentities(identities);
                _accountAccessText.Text = FormatAccess(access);
                RenderManagedDevices(devices);
                _accountStatus.Text = "Данные аккаунта обновлены.";
            });
        }
        catch (UnauthorizedAccessException)
        {
            _managedAccessToken = null;
            RebuildManagedCoordinator();
            SetManagedSignedOut("Сессия истекла. Войдите снова.");
        }
        catch (Exception ex) { SetAccountStatus("Не удалось обновить аккаунт: " + ex.Message); }
    }

    private static string FormatAccess(AccessSnapshot access)
    {
        var until = access.ValidUntil?.ToLocalTime().ToString("g") ?? "—";
        var offline = access.OfflineUntil?.ToLocalTime().ToString("g") ?? "—";
        return $"Скачивание: {(access.CanDownload ? "разрешено" : "нет")}\n" +
               $"Редактор/ASR: {(access.CanEdit ? "разрешено" : "нет")}\n" +
               $"Безлимит: {(access.Unlimited ? "да" : "нет")}\n" +
               $"Осталось скачиваний: {access.RemainingDownloads}\n" +
               $"Доступ до: {until}\nОфлайн до: {offline}\nПричина: {access.Reason}";
    }

    private async Task<T> ManagedGetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = ManagedRequest(HttpMethod.Get, path);
        using var response = await _managedHttp.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("managed_session_expired");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Ответ сервера пуст.");
    }

    private HttpRequestMessage ManagedRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_managedAccessToken))
            throw new UnauthorizedAccessException("managed_sign_in_required");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _managedAccessToken);
        request.Headers.Add("X-VideoGrabber-Api-Version", "1");
        return request;
    }

    private async Task EnsureManagedDeviceAndLeaseAsync(
        AccountProfile profile, AccessSnapshot access, DeviceReceipt[] devices)
    {
        var secret = await ReadManagedDeviceSecretAsync();
        var storedDeviceIsUsable = secret is not null
            && secret.AccountId == profile.AccountId
            && devices.Any(x => x.DeviceId == secret.DeviceId && !x.Revoked);
        if (!storedDeviceIsUsable)
            secret = await RegisterManagedDeviceAsync(profile.AccountId);
        if (secret is null) throw new InvalidOperationException("Managed device registration failed.");

        _managedDeviceId = secret.DeviceId;
        var cache = new OfflineAccessCache(_managedLeaseStore, profile.AccountId, secret.DeviceId);
        try
        {
            await cache.LoadAsync(_windowLifetime.Token);
            _managedOfflineCache = cache;
        }
        catch (Exception ex) when (ex is InvalidDataException or CryptographicException)
        {
            await cache.ClearAsync(CancellationToken.None);
            _managedOfflineCache = null;
        }

        if (access.CanEdit)
        {
            try
            {
                await RefreshManagedOfflineLeaseAsync(secret, cache);
                _managedOfflineCache = cache;
            }
            catch (HttpRequestException) when (_managedOfflineCache is not null) { }
        }
    }

    private async Task<ManagedDeviceSecret?> ReadManagedDeviceSecretAsync()
    {
        var bytes = await _managedDeviceKeyStore.ReadAsync(_windowLifetime.Token);
        if (bytes is null) return null;
        try { return JsonSerializer.Deserialize<ManagedDeviceSecret>(bytes); }
        catch (JsonException) { return null; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task<ManagedDeviceSecret> RegisterManagedDeviceAsync(Guid accountId)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new DeviceRegistration(
            Environment.MachineName,
            "windows",
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            Guid.NewGuid());
        using var httpRequest = ManagedRequest(HttpMethod.Post, "/v1/devices");
        httpRequest.Content = JsonContent.Create(request);
        using var response = await _managedHttp.SendAsync(httpRequest, _windowLifetime.Token);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("Достигнут лимит устройств. Отзовите старый компьютер в аккаунте.");
        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<DeviceReceipt>(cancellationToken: _windowLifetime.Token)
            ?? throw new InvalidDataException("Сервер не вернул устройство.");
        var secret = new ManagedDeviceSecret(accountId, receipt.DeviceId,
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(secret);
        try { await _managedDeviceKeyStore.SaveAsync(bytes, _windowLifetime.Token); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        return secret;
    }

    private async Task RefreshManagedOfflineLeaseAsync(
        ManagedDeviceSecret secret, OfflineAccessCache cache)
    {
        using var challengeRequest = ManagedRequest(HttpMethod.Post,
            $"/v1/devices/{secret.DeviceId:D}/challenge");
        using var challengeResponse = await _managedHttp.SendAsync(challengeRequest, _windowLifetime.Token);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<DeviceChallenge>(cancellationToken: _windowLifetime.Token)
            ?? throw new InvalidDataException("Сервер не вернул challenge устройства.");

        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(secret.PrivateKeyPkcs8), out _);
        var signature = key.SignData(Encoding.UTF8.GetBytes(challenge.Nonce),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        using var leaseRequest = ManagedRequest(HttpMethod.Post,
            $"/v1/devices/{secret.DeviceId:D}/lease");
        leaseRequest.Content = JsonContent.Create(new DeviceLeaseProof(
            challenge.Nonce, Convert.ToBase64String(signature)));
        using var leaseResponse = await _managedHttp.SendAsync(leaseRequest, _windowLifetime.Token);
        leaseResponse.EnsureSuccessStatusCode();
        var lease = await leaseResponse.Content.ReadFromJsonAsync<SignedOfflineLease>(cancellationToken: _windowLifetime.Token)
            ?? throw new InvalidDataException("Сервер не вернул офлайн-лицензию.");

        using var keysResponse = await _managedHttp.GetAsync("/v1/lease-keys", _windowLifetime.Token);
        keysResponse.EnsureSuccessStatusCode();
        var published = await keysResponse.Content.ReadFromJsonAsync<LeasePublicKey[]>(cancellationToken: _windowLifetime.Token)
            ?? throw new InvalidDataException("Сервер не вернул ключи лицензии.");
        var keys = published.ToDictionary(x => x.KeyId, LeaseKeyToPem, StringComparer.Ordinal);
        var serverUtc = leaseResponse.Headers.Date ?? DateTimeOffset.UtcNow;
        await cache.SaveAsync(lease, keys, serverUtc, _windowLifetime.Token);
    }

    private static string LeaseKeyToPem(LeasePublicKey key)
    {
        if (!string.Equals(key.Algorithm, "ES256", StringComparison.Ordinal))
            throw new InvalidDataException("Неподдерживаемый алгоритм ключа лицензии.");
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = DecodeBase64Url(key.X), Y = DecodeBase64Url(key.Y) }
        };
        using var ecdsa = ECDsa.Create(parameters);
        return ecdsa.ExportSubjectPublicKeyInfoPem();
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private async Task TryRestoreOfflineStateAsync()
    {
        var secret = await ReadManagedDeviceSecretAsync();
        if (secret is null) return;
        _managedAccountId = secret.AccountId;
        _managedDeviceId = secret.DeviceId;
        var cache = new OfflineAccessCache(_managedLeaseStore, secret.AccountId, secret.DeviceId);
        try
        {
            await cache.LoadAsync(_windowLifetime.Token);
            _managedOfflineCache = cache;
            await RestoreManagedQueueAsync(secret.AccountId);
        }
        catch (Exception ex) when (ex is InvalidDataException or CryptographicException)
        {
            _managedOfflineCache = null;
        }
        RebuildManagedCoordinator();
    }

    private async Task RefreshManagedSensitiveSessionAsync()
    {
        var lifecycle = new SystemBrowserSignIn(_managedHttp, "email",
            timeout: TimeSpan.FromMinutes(5), sessionStore: _managedSessionStore);
        var session = await lifecycle.RefreshAsync(_windowLifetime.Token);
        _managedAccessToken = session.AccessToken;
        RebuildManagedCoordinator();
    }

    private async Task LinkManagedProviderAsync(string provider)
    {
        if (_accountBusy) return;
        _accountBusy = true;
        SetAccountStatus("Готовлю безопасную привязку способа входа…");
        try
        {
            await RefreshManagedSensitiveSessionAsync();
            using var begin = ManagedRequest(HttpMethod.Post, "/v1/identities/link");
            using var begun = await _managedHttp.SendAsync(begin, _windowLifetime.Token);
            if (begun.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException("fresh_session_required");
            begun.EnsureSuccessStatusCode();
            var challenge = await begun.Content.ReadFromJsonAsync<LinkChallenge>(cancellationToken: _windowLifetime.Token)
                ?? throw new InvalidDataException("Сервер не вернул challenge привязки.");
            var browser = new SystemBrowserSignIn(_managedHttp, provider, timeout: TimeSpan.FromMinutes(5));
            await browser.LinkAsync(challenge.ChallengeId, _windowLifetime.Token);
            SetAccountStatus("Способ входа привязан. Обновляю профиль…");
            await LoadManagedAccountAsync();
        }
        catch (OperationCanceledException) { SetAccountStatus("Привязка отменена."); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            SetAccountStatus("Этот способ входа уже принадлежит другому аккаунту или challenge уже использован.");
        }
        catch (Exception ex) { SetAccountStatus("Не удалось привязать способ входа: " + ex.Message); }
        finally { _accountBusy = false; }
    }

    private async Task UnlinkManagedIdentityAsync(Guid identityId)
    {
        if (_accountBusy) return;
        _accountBusy = true;
        try
        {
            await RefreshManagedSensitiveSessionAsync();
            using var request = ManagedRequest(HttpMethod.Delete, $"/v1/identities/{identityId:D}");
            using var response = await _managedHttp.SendAsync(request, _windowLifetime.Token);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                SetAccountStatus("Нельзя отвязать последний способ входа.");
                return;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                SetAccountStatus("Способ входа уже отсутствует.");
                await LoadManagedAccountAsync();
                return;
            }
            response.EnsureSuccessStatusCode();
            SetAccountStatus("Способ входа отвязан. Обновляю профиль…");
            await LoadManagedAccountAsync();
        }
        catch (Exception ex) { SetAccountStatus("Не удалось отвязать способ входа: " + ex.Message); }
        finally { _accountBusy = false; }
    }

    private void RenderManagedIdentities(IReadOnlyList<LinkedIdentity> identities)
    {
        _accountIdentitiesPanel.Children.Clear();
        _accountIdentitiesPanel.Children.Add(SectionHeading("Способы входа"));
        if (identities.Count == 0)
        {
            _accountIdentitiesPanel.Children.Add(MutedText("Нет доступных сведений о связанных способах входа."));
            return;
        }
        var canUnlink = identities.Count > 1;
        foreach (var identity in identities)
        {
            var label = new TextBlock
            {
                Text = identity.Provider +
                    (string.IsNullOrWhiteSpace(identity.VerifiedEmail) ? string.Empty : "  •  " + identity.VerifiedEmail),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var unlink = SecondaryButton("Отвязать");
            unlink.IsEnabled = canUnlink;
            var id = identity.IdentityId;
            unlink.Click += async (_, _) => await UnlinkManagedIdentityAsync(id);
            _accountIdentitiesPanel.Children.Add(TwoColumn(label, unlink, secondAuto: true));
        }
        _accountIdentitiesPanel.Children.Add(MutedText(
            "Привязка требует текущую сессию и отдельное подтверждение нового провайдера. Совпадение email само по себе аккаунты не объединяет."));
    }
    private void RenderManagedDevices(IEnumerable<DeviceReceipt> devices)
    {
        _accountDevicesPanel.Children.Clear();
        _accountDevicesPanel.Children.Add(SectionHeading("Компьютеры"));
        foreach (var device in devices)
        {
            var label = new TextBlock
            {
                Text = $"{device.Name}  •  {device.DeviceId:D}" +
                    (device.DeviceId == _managedDeviceId ? "  •  этот компьютер" : "") +
                    (device.Revoked ? "  •  отозван" : ""),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var revoke = SecondaryButton("Отозвать");
            revoke.IsEnabled = !device.Revoked;
            var id = device.DeviceId;
            revoke.Click += async (_, _) => await RevokeManagedDeviceAsync(id);
            _accountDevicesPanel.Children.Add(TwoColumn(label, revoke, secondAuto: true));
        }
        if (_accountDevicesPanel.Children.Count == 1)
            _accountDevicesPanel.Children.Add(MutedText("Нет зарегистрированных устройств."));
    }

    private async Task RevokeManagedDeviceAsync(Guid deviceId)
    {
        try
        {
            using var request = ManagedRequest(HttpMethod.Post, $"/v1/devices/{deviceId:D}/revoke");
            using var response = await _managedHttp.SendAsync(request, _windowLifetime.Token);
            response.EnsureSuccessStatusCode();
            if (deviceId == _managedDeviceId)
            {
                await _managedLeaseStore.DeleteAsync(CancellationToken.None);
                await _managedDeviceKeyStore.DeleteAsync(CancellationToken.None);
                _managedDeviceId = null;
                _managedOfflineCache = null;
                RebuildManagedCoordinator();
                SetAccountStatus("Этот компьютер отозван. Нажмите «Обновить данные», чтобы зарегистрировать его заново.");
                var devices = await ManagedGetAsync<DeviceReceipt[]>("/v1/devices", _windowLifetime.Token);
            var identities = await ManagedGetAsync<LinkedIdentity[]>("/v1/identities", _windowLifetime.Token);
                AccountUi(() => RenderManagedDevices(devices));
                return;
            }
            await LoadManagedAccountAsync();
        }
        catch (Exception ex) { SetAccountStatus("Не удалось отозвать устройство: " + ex.Message); }
    }

    private void SetAccountStatus(string message)
        => AccountUi(() => _accountStatus.Text = message);

    private void AccountUi(Action action)
    {
        if (!DispatcherQueue.TryEnqueue(() => action()))
            AppDiagnostics.Write("Account UI dispatch failed");
    }

    private sealed record ManagedDeviceSecret(
        Guid AccountId,
        Guid DeviceId,
        string PrivateKeyPkcs8);
#endif
}
