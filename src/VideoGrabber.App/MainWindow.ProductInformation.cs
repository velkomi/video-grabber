using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private string ProductInformationUrl(string document)
        => new Uri(_activeClientServices.WebsiteBase, "/info/?document=" + Uri.EscapeDataString(document)).AbsoluteUri;

    private FrameworkElement BuildProductInformationCard()
    {
        var body = Vertical(8);
        body.Children.Add(SectionHeading("Помощь и документы"));
        var support = StudioButton("Написать в поддержку", true);
        support.Click += async (_, _) => await ShowSupportAsync();
        body.Children.Add(support);
        body.Children.Add(StudioLink("Как пользоваться ↗", ProductInformationUrl("help")));
        body.Children.Add(StudioLink("Условия использования ↗", ProductInformationUrl("terms")));
        body.Children.Add(StudioLink("Конфиденциальность ↗", ProductInformationUrl("privacy")));
        body.Children.Add(StudioLink("Подписка и возвраты ↗", ProductInformationUrl("subscription")));
        body.Children.Add(StudioLink("Правила приглашений ↗", ProductInformationUrl("referrals")));
        body.Children.Add(StudioLink("Компоненты и лицензии ↗", ProductInformationUrl("components")));
        body.Children.Add(StudioLink("Контакты и сайт разработчика ↗", ProductInformationUrl("contacts")));
        return Details("Помощь, контакты и условия", body);
    }

    private async Task<bool> ConfirmCourseRightsAsync(Guid intentId)
    {
        if (_rootHost.XamlRoot is null) return false;
#if VIDEOGRABBER_MANAGED
        string? version = null;
        string? documentHash = null;
        try
        {
            using var response = await _managedHttp.GetAsync("/v1/documents", _windowLifetime.Token);
            response.EnsureSuccessStatusCode();
            using var catalog = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_windowLifetime.Token));
            var documents = catalog.RootElement.ValueKind == JsonValueKind.Array
                ? catalog.RootElement : catalog.RootElement.GetProperty("documents");
            var terms = documents.EnumerateArray().First(document => document.GetProperty("id").GetString() == "terms");
            version = terms.GetProperty("version").GetString();
            documentHash = terms.GetProperty("sha256").GetString();
            if (string.IsNullOrWhiteSpace(version)) throw new InvalidDataException();
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception)
        {
            await ShowOperationalHelpAsync("Не удалось открыть условия", "Проверьте соединение и повторите загрузку курса.", showInformation: false);
            return false;
        }
#endif
        var content = Vertical(12);
        content.Children.Add(MutedText("Сохраняйте курс, если автор разрешил копирование или материалы принадлежат вам. Доступ к урокам сам по себе не даёт права сохранить весь курс."));
        content.Children.Add(StudioLink("Правила использования ↗", ProductInformationUrl("terms")));
        var agreement = new CheckBox
        {
            Content = new TextBlock { Text = "У меня есть право сохранить эти материалы", TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            IsChecked = false
        };
        content.Children.Add(agreement);
        var dialog = new ContentDialog
        {
            XamlRoot = _rootHost.XamlRoot,
            Title = "Сохранение курса",
            Content = content,
            PrimaryButtonText = "Продолжить",
            CloseButtonText = "Отмена",
            IsPrimaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.Close
        };
        agreement.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        agreement.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || agreement.IsChecked != true) return false;
#if VIDEOGRABBER_MANAGED
        try
        {
            await RefreshManagedSensitiveSessionAsync();
            using var request = ManagedRequest(HttpMethod.Post, "/v1/consents");
            request.Content = JsonContent.Create(new { documentId = "terms", version, documentHash, decision = "accepted", purpose = "course_rights", intentId });
            using var response = await _managedHttp.SendAsync(request, _windowLifetime.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception)
        {
            await ShowOperationalHelpAsync("Подтверждение не сохранено", "Проверьте соединение и повторите загрузку курса. Скачивание ещё не началось.", showInformation: false);
            return false;
        }
#endif
        return true;
    }
}
