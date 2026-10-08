using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Platform.Contracts;
using Windows.ApplicationModel.DataTransfer;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
#if VIDEOGRABBER_MANAGED
    private StackPanel _managedReferralsPanel = null!;
    private FrameworkElement _managedReferralsCard = null!;
    private long _managedReferralsGeneration;

    private FrameworkElement BuildManagedReferralsCard()
    {
        _managedReferralsPanel = Vertical(8);
        _managedReferralsPanel.Children.Add(SectionHeading("Приглашения и бонусы"));
        _managedReferralsPanel.Children.Add(MutedText("Обновляю данные…"));
        _managedReferralsCard = Card(_managedReferralsPanel);
        _managedReferralsCard.Visibility = Visibility.Collapsed;
        return _managedReferralsCard;
    }

    private async Task RefreshManagedReferralsAsync()
    {
        if (string.IsNullOrWhiteSpace(_managedAccessToken)) return;
        var accountId = _managedAccountId;
        var generation = Interlocked.Increment(ref _managedReferralsGeneration);
        try
        {
            using var request = ManagedRequest(HttpMethod.Get, "/v1/referrals");
            using var response = await _managedHttp.SendAsync(request, _windowLifetime.Token);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                AccountUi(() =>
                {
                    if (generation == _managedReferralsGeneration && accountId == _managedAccountId) _managedReferralsCard.Visibility = Visibility.Collapsed;
                });
                return;
            }
            response.EnsureSuccessStatusCode();
            var summary = await response.Content.ReadFromJsonAsync<ReferralSummary>(cancellationToken: _windowLifetime.Token)
                ?? throw new InvalidDataException("Referral summary unavailable.");
            AccountUi(() =>
            {
                if (generation != _managedReferralsGeneration || accountId != _managedAccountId || string.IsNullOrWhiteSpace(_managedAccessToken)) return;
                RenderManagedReferrals(summary);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            AccountUi(() =>
            {
                if (generation != _managedReferralsGeneration || accountId != _managedAccountId || string.IsNullOrWhiteSpace(_managedAccessToken)) return;
                _managedReferralsPanel.Children.Clear();
                _managedReferralsPanel.Children.Add(SectionHeading("Приглашения и бонусы"));
                _managedReferralsPanel.Children.Add(MutedText("Не удалось обновить бонусы. Проверьте интернет и нажмите «Обновить данные»."));
                _managedReferralsCard.Visibility = Visibility.Visible;
            });
        }
    }

    private void RenderManagedReferrals(ReferralSummary summary)
    {
        _managedReferralsPanel.Children.Clear();
        _managedReferralsCard.Visibility = Visibility.Visible;
        _managedReferralsPanel.Children.Add(SectionHeading("Приглашения и бонусы"));
        _managedReferralsPanel.Children.Add(MutedText(
            "Другу — 10% на первую разовую оплату подписки. Вам — 10% от оплаченной суммы. Бонусы доступны через 14 дней и действуют 365 дней."));
        _managedReferralsPanel.Children.Add(MutedText($"Приглашено: {summary.Invited} · Оплатили: {summary.Paid}"));
        foreach (var balance in summary.Balances)
        {
            string Amount(long minor) => ReferralMoney(new Money(minor, balance.Currency));
            _managedReferralsPanel.Children.Add(MutedText(
                $"Доступно: {Amount(balance.AvailableMinor)} · Ожидает: {Amount(balance.PendingMinor)}"));
            if (balance.ReservedMinor > 0)
                _managedReferralsPanel.Children.Add(MutedText($"В счетах на оплату: {Amount(balance.ReservedMinor)}"));
            if (balance.DebtMinor > 0)
                _managedReferralsPanel.Children.Add(MutedText($"Возврат уменьшит будущие бонусы: {Amount(balance.DebtMinor)}"));
        }
        if (summary.Balances.Length == 0) _managedReferralsPanel.Children.Add(MutedText("Бонусов пока нет."));
        if (SafeReferralLink(summary.WebLink))
        {
            var copy = SecondaryButton("Копировать приглашение");
            copy.Click += (_, _) =>
            {
                try
                {
                    var data = new DataPackage();
                    data.SetText(summary.WebLink.AbsoluteUri);
                    Clipboard.SetContent(data);
                    SetAccountStatus("Ссылка приглашения скопирована.");
                }
                catch (Exception) { SetAccountStatus("Не удалось скопировать. Откройте ссылку приглашения и скопируйте адрес."); }
            };
            _managedReferralsPanel.Children.Add(copy);
            _managedReferralsPanel.Children.Add(StudioLink("Открыть ссылку приглашения ↗", summary.WebLink.AbsoluteUri));
        }
        if (SafeReferralLink(summary.TelegramLink) && summary.TelegramLink.Host == "t.me")
            _managedReferralsPanel.Children.Add(StudioLink("Поделиться в Telegram ↗",
                "https://t.me/share/url?url=" + Uri.EscapeDataString(summary.TelegramLink.AbsoluteUri)));
        if (summary.History.Length > 0)
        {
            var history = Vertical(6);
            foreach (var row in summary.History.Take(10))
            {
                var label = row.Kind switch
                {
                    "reward" or "earn" => "Начисление",
                    "spend" => "Оплата бонусами",
                    "refund" or "restore" => "Возврат бонусов",
                    "clawback" => "Пересчёт после возврата",
                    "expire" => "Истечение",
                    "offset" or "offset_debit" or "offset_credit" => "Зачёт возврата",
                    _ => "Изменение бонусов"
                };
                history.Children.Add(MutedText($"{row.CreatedAt.ToLocalTime():d} · {label} · {ReferralMoney(row.Amount)}" +
                    (row.AvailableAt is { } available ? $" · доступно с {available.ToLocalTime():d}" : "") +
                    (row.ExpiresAt is { } expires ? $" · до {expires.ToLocalTime():d}" : "")));
            }
            _managedReferralsPanel.Children.Add(Details("История бонусов", history));
        }
        _managedReferralsPanel.Children.Add(MutedText("Бонусы не выводятся. RUB и Stars учитываются отдельно. Скидки и бонусы вместе — до 30% цены разовой оплаты."));
        _managedReferralsPanel.Children.Add(StudioLink("Использовать бонусы и промокод на сайте ↗", "https://videograbber.srv1902378.hstgr.cloud/web/#pricing"));
    }

    private static bool SafeReferralLink(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
    private static string ReferralMoney(Money amount) => amount.Currency switch
    {
        "RUB" => (amount.MinorUnits / 100m).ToString("N2", CultureInfo.GetCultureInfo("ru-RU")) + " ₽",
        "XTR" => amount.MinorUnits.ToString(CultureInfo.InvariantCulture) + " Stars",
        _ => "—"
    };
#endif
}
