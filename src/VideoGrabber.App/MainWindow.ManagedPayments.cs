using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
#if VIDEOGRABBER_MANAGED
    private ComboBox _managedPaymentProduct = null!;
    private CheckBox _managedPaymentRecurring = null!;
    private TextBlock _managedPaymentStatus = null!;
    private StackPanel _managedSubscriptionsPanel = null!;

    private FrameworkElement BuildManagedPaymentsCard()
    {
        var panel = Vertical(8);
        panel.Children.Add(SectionHeading("Тарифы и оплата"));
        panel.Children.Add(MutedText(
            "Тариф общий для сайта, Windows и Telegram. Выбор и оплата — на сайте."));
        _managedPaymentProduct = new ComboBox
        {
            PlaceholderText = "Выберите тариф",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(_managedPaymentProduct);
        _managedPaymentRecurring = new CheckBox
        {
            Content = "Автопродление, если доступно для тарифа"
        };
        panel.Children.Add(_managedPaymentRecurring);

        var refresh = SecondaryButton("Обновить тарифы");
        refresh.Click += async (_, _) =>
        {
            await RefreshManagedPaymentProductsAsync();
            await RefreshManagedSubscriptionsAsync();
        };
        var buy = PrimaryButton("Выбрать тариф на сайте");
        buy.Click += (_, _) =>
        {
            var planId = (_managedPaymentProduct.SelectedItem as DesktopPaymentProduct)?.PlanId;
            OpenPricingPage(planId);
        };
        panel.Children.Add(Horizontal(refresh, buy));
        _managedPaymentStatus = MutedText(
            "Войдите, чтобы увидеть свой тариф и подписки.");
        panel.Children.Add(_managedPaymentStatus);
        panel.Children.Add(SectionHeading("Подписки"));
        _managedSubscriptionsPanel = Vertical(6);
        _managedSubscriptionsPanel.Children.Add(MutedText("После входа здесь появятся активные подписки и дата оплаченного периода."));
        panel.Children.Add(_managedSubscriptionsPanel);
        return Card(panel);
    }

    private async Task RefreshManagedPaymentProductsAsync()
    {
        if (string.IsNullOrWhiteSpace(_managedAccessToken))
        {
            SetManagedPaymentStatus("Сначала войдите в аккаунт.");
            return;
        }
        try
        {
            var catalog = await ManagedGetAsync<DesktopPaymentCatalog>(
                "/v1/payment-products?surface=desktop",
                _windowLifetime.Token);
            var products = catalog.Products
                .Where(x => x.Prices.ContainsKey("yookassa"))
                .ToArray();
            AccountUi(() =>
            {
                _managedPaymentProduct.Items.Clear();
                foreach (var product in products)
                    _managedPaymentProduct.Items.Add(product);
                if (_managedPaymentProduct.Items.Count > 0)
                    _managedPaymentProduct.SelectedIndex = 0;
                _managedPaymentProduct.DisplayMemberPath = nameof(DesktopPaymentProduct.Display);
            });
            SetManagedPaymentStatus(
                products.Length == 0
                    ? "Каталог сейчас недоступен. Посмотрите тарифы на сайте."
                    : "Актуальные тарифы загружены.");
        }
        catch (Exception)
        {
            SetManagedPaymentStatus("Не удалось загрузить тарифы. Проверьте интернет или откройте тарифы на сайте.");
        }
    }

    private async Task RefreshManagedSubscriptionsAsync()
    {
        if (string.IsNullOrWhiteSpace(_managedAccessToken)) return;
        var rows = await ManagedGetAsync<SubscriptionView[]>(
            "/v1/subscriptions", _windowLifetime.Token);
        AccountUi(() =>
        {
            _managedSubscriptionsPanel.Children.Clear();
            if (rows.Length == 0)
            {
                _managedSubscriptionsPanel.Children.Add(MutedText("Подписок пока нет."));
                return;
            }
            foreach (var subscription in rows)
            {
                var line = Vertical(4);
                line.Children.Add(MutedText(
                    $"Оплачено до {subscription.PaidThrough.ToLocalTime():g}. Автопродление: {(subscription.AutoRenew ? "включено" : "выключено")}. " +
                    (subscription.State switch
                    {
                        "active" => "Подписка активна.",
                        "cancelled" => "Продление отменено.",
                        "expired" => "Подписка закончилась.",
                        "past_due" => "Не удалось продлить подписку. Проверьте оплату на сайте.",
                        "pending" => "Ожидается подтверждение оплаты.",
                        _ => "Проверьте состояние подписки на сайте."
                    })));
                if (subscription.AutoRenew && subscription.State == "active")
                {
                    var cancel = SecondaryButton("Отключить автопродление");
                    cancel.Click += async (_, _) => await CancelManagedSubscriptionAsync(subscription.SubscriptionId);
                    line.Children.Add(cancel);
                }
                _managedSubscriptionsPanel.Children.Add(line);
            }
        });
    }

    private async Task CancelManagedSubscriptionAsync(Guid subscriptionId)
    {
        try
        {
            using var request = ManagedRequest(
                HttpMethod.Post,
                $"/v1/subscriptions/{subscriptionId:D}/cancel");
            request.Content = JsonContent.Create(
                new CancelSubscriptionRequest(Guid.NewGuid()));
            using var response = await _managedHttp.SendAsync(
                request, _windowLifetime.Token);
            response.EnsureSuccessStatusCode();
            var view = await response.Content.ReadFromJsonAsync<SubscriptionView>(
                cancellationToken: _windowLifetime.Token)
                ?? throw new InvalidDataException("Subscription response is empty.");
            SetManagedPaymentStatus(
                $"Автопродление отключено. Оплаченный период сохранён до {view.PaidThrough.ToLocalTime():g}.");
            await RefreshManagedSubscriptionsAsync();
        }
        catch (Exception)
        {
            SetManagedPaymentStatus("Не удалось отключить автопродление. Проверьте интернет и повторите попытку.");
        }
    }
    private async Task StartManagedYooKassaPurchaseAsync()
    {
        if (string.IsNullOrWhiteSpace(_managedAccessToken))
        {
            SetManagedPaymentStatus("Сначала войдите в аккаунт.");
            return;
        }
        if (_managedPaymentProduct.SelectedItem is not DesktopPaymentProduct product)
        {
            await RefreshManagedPaymentProductsAsync();
            if (_managedPaymentProduct.SelectedItem is not DesktopPaymentProduct refreshed)
            {
                SetManagedPaymentStatus("Доступных тарифов пока нет. Посмотрите тарифы на сайте.");
                return;
            }
            product = refreshed;
        }
        var recurring = _managedPaymentRecurring.IsChecked == true;
        if (recurring && !product.RecurringAllowed)
        {
            SetManagedPaymentStatus("Для этого тарифа автопродление недоступно.");
            return;
        }

        try
        {
            using var request = ManagedRequest(HttpMethod.Post, "/v1/payments");
            request.Content = JsonContent.Create(new PurchaseRequest(
                product.Sku, "yookassa", Guid.NewGuid(), recurring));
            using var response = await _managedHttp.SendAsync(
                request, _windowLifetime.Token);
            response.EnsureSuccessStatusCode();
            var checkout = await response.Content.ReadFromJsonAsync<PaymentCheckout>(
                cancellationToken: _windowLifetime.Token)
                ?? throw new InvalidDataException("Payment checkout response is empty.");
            if (checkout.RedirectUri is null
                || checkout.RedirectUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("Payment confirmation URL is invalid.");

            Process.Start(new ProcessStartInfo
            {
                FileName = checkout.RedirectUri.AbsoluteUri,
                UseShellExecute = true
            });
            SetManagedPaymentStatus(
                "Страница оплаты открыта в браузере. " +
                "После оплаты нажмите «Обновить данные». Доступ появится после подтверждения оплаты.");
        }
        catch (Exception)
        {
            SetManagedPaymentStatus("Не удалось открыть оплату. Проверьте интернет и попробуйте ещё раз.");
        }
    }

    private void SetManagedPaymentStatus(string message)
        => AccountUi(() =>
        {
            if (_managedPaymentStatus is not null)
                _managedPaymentStatus.Text = message;
        });

    private sealed record DesktopPaymentCatalog(
        string Version,
        string Environment,
        DesktopPaymentProduct[] Products);

    private sealed record DesktopPaymentProduct(
        string Sku,
        string Kind,
        long Credits,
        int Days,
        bool RecurringAllowed,
        Dictionary<string, DesktopPaymentPrice> Prices,
        string? PlanId = null)
    {
        public string Display
        {
            get
            {
                if (!Prices.TryGetValue("yookassa", out var price))
                    return string.IsNullOrWhiteSpace(PlanId) ? "Пакет доступа" : FriendlyPlanName(PlanId);
                var rub = price.MinorUnits / 100m;
                return $"{(string.IsNullOrWhiteSpace(PlanId) ? (Days > 0 ? $"Доступ на {Days} дней" : $"Пакет на {Credits} загрузок") : FriendlyPlanName(PlanId))} • {rub:0.00} {price.Currency}";
            }
        }
    }

    private sealed record DesktopPaymentPrice(
        long MinorUnits,
        string Currency);
#endif
}
