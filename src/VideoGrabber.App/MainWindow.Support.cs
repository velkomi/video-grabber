using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Infrastructure.ClientUpdates;
using VideoGrabber.Infrastructure.Licensing;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private SupportRequest? _supportDraft;

    private async Task ShowSupportAsync()
    {
        if (_rootHost.XamlRoot is null) return;
        var body = Vertical(10);
        body.Width = 480;
        body.Children.Add(MutedText("Опишите вопрос — мы ответим по оставленному контакту."));
        var topic = new ComboBox { Header = "Тема", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in new[] { ("sign_in", "Вход в аккаунт"), ("download", "Скачивание"),
            ("subscription", "Тариф и оплата"), ("suggestion", "Предложение"), ("other", "Другой вопрос") })
            topic.Items.Add(new ComboBoxItem { Tag = item.Item1, Content = item.Item2 });
        topic.SelectedIndex = 0;
        var contact = new TextBox { Header = "Контакт для ответа", PlaceholderText = "Почта или @имя в Telegram", MaxLength = 254, Text = _supportDraft?.Contact ?? "" };
        var question = new TextBox { Header = "Ваш вопрос", PlaceholderText = "Что произошло и как это повторить?", AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, MinHeight = 130, MaxHeight = 220, MaxLength = 4000, Text = _supportDraft?.Message ?? "" };
        if (_supportDraft is { } saved)
            topic.SelectedItem = topic.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == saved.Topic) ?? topic.Items[0];
        body.Children.Add(topic); body.Children.Add(contact); body.Children.Add(question);
        body.Children.Add(MutedText("Пароли, коды входа и платёжные данные присылать не нужно."));
        body.Children.Add(StudioLink("Подробнее о данных ↗", ProductInformationUrl("privacy")));
        var status = MutedText(""); body.Children.Add(status);
        ApplyStudioInputs(body);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime.Token);
        var dialog = new ContentDialog { XamlRoot = _rootHost.XamlRoot, Title = "Написать в поддержку", Content = body,
            PrimaryButtonText = "Отправить вопрос", CloseButtonText = "Отмена", DefaultButton = ContentDialogButton.Primary };
        var complete = false;
        dialog.CloseButtonClick += (_, _) => cancel.Cancel();
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            if (complete) return;
            args.Cancel = true;
            var deferral = args.GetDeferral();
            try
            {
                if (question.Text.Trim().Length < 20 || string.IsNullOrWhiteSpace(contact.Text))
                { status.Text = "Оставьте контакт и опишите вопрос: от 20 до 4000 символов."; return; }
                var chosen = (string)((ComboBoxItem)topic.SelectedItem).Tag;
                var same = _supportDraft is { } previous && previous.Topic == chosen
                    && previous.Contact == contact.Text.Trim() && previous.Message == question.Text.Trim();
                _supportDraft = new SupportRequest(same ? _supportDraft!.RequestId : Guid.NewGuid(), chosen, contact.Text.Trim(), question.Text.Trim());
                topic.IsEnabled = contact.IsEnabled = question.IsEnabled = false;
                dialog.IsPrimaryButtonEnabled = false;
                status.Text = "Проверяю и отправляю вопрос…";
#if VIDEOGRABBER_MANAGED
                var http = _managedHttp;
                var token = _managedAccessToken;
#else
                using var http = new HttpClient(new ClientDirectoryRequestGate(_clientReleaseCache, _clientServiceSelection,
                    BootstrapClientServices, new HttpClientHandler { AllowAutoRedirect = false })) { BaseAddress = _clientServiceSelection.Services.ApiBase };
                string? token = null;
#endif
                var accepted = await new SupportApiClient(http).SubmitAsync(_supportDraft, token, cancel.Token);
                status.Text = "Обращение сохранено. Номер VG-" + accepted.TicketId.ToString("N")[..8].ToUpperInvariant() + ".";
                _supportDraft = null; complete = true;
                dialog.PrimaryButtonText = "Готово"; dialog.CloseButtonText = "Закрыть";
            }
            catch (SupportClientException ex)
            { status.Text = ex.Status == 429 ? "Слишком много обращений. Пожалуйста, попробуйте позже." : "Не получилось отправить вопрос. Текст сохранён — попробуйте позже."; }
            catch (OperationCanceledException) { status.Text = "Отправка прервана. Текст сохранён."; }
            catch (Exception) { status.Text = "Не получилось отправить вопрос. Текст сохранён — попробуйте позже."; }
            finally
            {
                topic.IsEnabled = contact.IsEnabled = question.IsEnabled = !complete;
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };
        await dialog.ShowAsync();
    }
}
