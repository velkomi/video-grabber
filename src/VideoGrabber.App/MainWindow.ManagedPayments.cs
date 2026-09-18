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

    private FrameworkElement BuildManagedPaymentsCard()
    {
        var panel = Vertical(8);
        panel.Children.Add(SectionHeading("Покупка доступа"));
        panel.Children.Add(MutedText(
            "На Windows внешний checkout открывается только через серверный YooKassa URL. " +
            "Возврат браузера сам по себе не подтверждает оплату — доступ обновляется только после проверки провайдера сервером."));
        _managedPaymentProduct = new ComboBox
        {
            PlaceholderText = "Выберите товар",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(_managedPaymentProduct);
        _managedPaymentRecurring = new CheckBox
        {
            Content = "Автопродление, если разрешено товаром"
        };
        panel.Children.Add(_managedPaymentRecurring);

        var refresh = SecondaryButton("Обновить товары");
        refresh.Click += async (_, _) => await RefreshManagedPaymentProductsAsync();
        var buy = PrimaryButton("Перейти к оплате YooKassa");
        buy.Click += async (_, _) => await StartManagedYooKassaPurchaseAsync();
        panel.Children.Add(Horizontal(refresh, buy));
        _managedPaymentStatus = MutedText(
            "До входа покупка недоступна. Live-каталог не включается автоматически.");
        panel.Children.Add(_managedPaymentStatus);
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
                    ? "YooKassa товары не настроены сервером."
                    : $"Каталог {catalog.Version}, среда {catalog.Environment}. " +
                      "Цена берётся только с сервера.");
        }
        catch (Exception ex)
        {
            SetManagedPaymentStatus("Не удалось загрузить товары: " + ex.Message);
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
                SetManagedPaymentStatus("Нет доступного YooKassa товара.");
                return;
            }
            product = refreshed;
        }
        var recurring = _managedPaymentRecurring.IsChecked == true;
        if (recurring && !product.RecurringAllowed)
        {
            SetManagedPaymentStatus("Для этого товара автопродление не разрешено.");
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
                $"Заказ {checkout.PaymentId:D} открыт в браузере. " +
                "После оплаты нажмите «Обновить данные»; только серверная проверка изменит доступ.");
        }
        catch (Exception ex)
        {
            SetManagedPaymentStatus("Checkout не создан: " + ex.Message);
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
        Dictionary<string, DesktopPaymentPrice> Prices)
    {
        public string Display
        {
            get
            {
                if (!Prices.TryGetValue("yookassa", out var price))
                    return Sku;
                var rub = price.MinorUnits / 100m;
                return $"{Sku} • {rub:0.00} {price.Currency}";
            }
        }
    }

    private sealed record DesktopPaymentPrice(
        long MinorUnits,
        string Currency);
#endif
}
