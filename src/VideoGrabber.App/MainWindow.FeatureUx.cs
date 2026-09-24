using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private const string VideoGrabberPricingUrl =
        "https://videograbber.srv1902378.hstgr.cloud/web/?from=desktop";
    private const string VideoGrabberTelegramUrl =
        "https://t.me/Velkoshkin";

    private enum FeatureAccessKind
    {
        IndividualDownload,
        PaidTools,
        FullCourse
    }

    private bool HasFeatureAccess(FeatureAccessKind feature)
    {
#if VIDEOGRABBER_MANAGED
        if (string.IsNullOrWhiteSpace(_managedAccessToken)
            || _managedAccessSnapshot is null)
            return false;

        return feature switch
        {
            FeatureAccessKind.IndividualDownload => _managedAccessSnapshot.CanDownload,
            FeatureAccessKind.PaidTools => _managedAccessSnapshot.CanEdit,
            FeatureAccessKind.FullCourse => _managedAccessSnapshot.CanDownloadCourse,
            _ => false
        };
#else
        return true;
#endif
    }

    private async Task<bool> EnsureFeatureAccessAsync(
        FeatureAccessKind feature,
        string actionTitle)
    {
        if (HasFeatureAccess(feature))
            return true;

        await ShowFeatureAccessDialogAsync(feature, actionTitle);
        return false;
    }

    private async Task ShowFeatureAccessDialogAsync(
        FeatureAccessKind feature,
        string actionTitle,
        string? serverReason = null)
    {
#if VIDEOGRABBER_MANAGED
        var signedIn = !string.IsNullOrWhiteSpace(_managedAccessToken);
        var access = _managedAccessSnapshot;
        var recommendedPlan = feature switch
        {
            FeatureAccessKind.FullCourse => "Full Course",
            FeatureAccessKind.PaidTools => "Start, Unlimited Video или Full Course",
            _ => access?.PlanId == "free"
                ? "Start или Unlimited Video"
                : "доступный тариф"
        };
        var recommendedPlanId = feature switch
        {
            FeatureAccessKind.FullCourse => "full_course",
            FeatureAccessKind.PaidTools => "start",
            _ => "start"
        };

        var intro = feature switch
        {
            FeatureAccessKind.FullCourse =>
                "Скачивание полного курса — функция максимального тарифа Full Course. " +
                "Он включает видео без лимита и сохранение структуры курса, страниц, вложений и найденных видео.",
            FeatureAccessKind.PaidTools =>
                "Эта функция относится к расширенным инструментам VideoGrabber: MP3, редактор и локальная транскрибация. " +
                "Они доступны на платных тарифах.",
            _ =>
                "Обычные загрузки доступны бесплатно в пределах 10 lifetime-загрузок. " +
                "После исчерпания лимита можно перейти на Start или тариф с безлимитными видео."
        };

        var current = signedIn
            ? $"Текущий тариф: {FriendlyPlanName(access?.PlanId)}. " +
              $"Осталось загрузок: {access?.RemainingDownloads ?? 0}."
            : "Сейчас вход в VideoGrabber-аккаунт не выполнен.";

        var panel = new StackPanel { Spacing = 10, MaxWidth = 520 };
        panel.Children.Add(new TextBlock
        {
            Text = intro,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = current,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Подходящий вариант: {recommendedPlan}.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text =
                "Free — 10 обычных видео за всё время аккаунта.\n" +
                "Start — до 10 загрузок в сутки + MP3/редактор/ASR.\n" +
                "Unlimited Video — отдельные видео без лимита + MP3/редактор/ASR.\n" +
                "Full Course — всё выше + скачивание полного курса.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85
        });
        if (!string.IsNullOrWhiteSpace(serverReason))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Ответ сервера: " + serverReason,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                FontSize = 12
            });
        }

        if (_rootHost.XamlRoot is null)
        {
            OpenPricingPage(recommendedPlanId);
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = _rootHost.XamlRoot,
            Title = actionTitle,
            Content = panel,
            PrimaryButtonText = signedIn ? "Выбрать тариф на сайте" : "Войти в аккаунт",
            SecondaryButtonText = signedIn ? "Тарифы в приложении" : "Посмотреть тарифы",
            CloseButtonText = "Закрыть",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (signedIn)
                OpenPricingPage(recommendedPlanId);
            else
                ShowPage("account");
        }
        else if (result == ContentDialogResult.Secondary)
        {
            if (signedIn)
                ShowPage("info");
            else
                OpenPricingPage(recommendedPlanId);
        }
#endif
    }

    private async Task ShowOperationalHelpAsync(
        string title,
        string message,
        bool showInformation = true)
    {
        if (_rootHost.XamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = _rootHost.XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 500
            },
            PrimaryButtonText = showInformation ? "Открыть «Информация»" : "",
            CloseButtonText = "Понятно",
            DefaultButton = ContentDialogButton.Close
        };
        var result = await dialog.ShowAsync();
        if (showInformation && result == ContentDialogResult.Primary)
            ShowPage("info");
    }

    private async Task TogglePauseOrExplainAsync()
    {
        if (!_operations.IsBusy && !_courseDownloadActive && !_isInstallingComponents)
        {
            await ShowOperationalHelpAsync(
                "Когда нужна «Пауза»",
                "Пауза работает только во время активной загрузки, скачивания курса, обработки MP3, " +
                "редактирования или транскрибации. Она временно приостанавливает текущую работу, " +
                "не удаляет очередь и не отменяет результат. После нажатия кнопка меняется на «Продолжить».");
            return;
        }

        TogglePause();
    }

    private async Task CancelOrExplainAsync()
    {
        if (!_operations.IsBusy && !_courseDownloadActive && !_isInstallingComponents)
        {
            await ShowOperationalHelpAsync(
                "Сейчас нечего отменять",
                "Кнопка «Отменить всё» используется, когда уже идёт загрузка, курс, редактор, MP3 или транскрибация. " +
                "Она останавливает текущую операцию. Невыполненная очередь и уже готовые локальные файлы сохраняются.");
            return;
        }

        CancelOperation();
    }

    private async Task ExplainMissingDownloadedMediaAsync()
        => await ShowOperationalHelpAsync(
            "Сначала скачайте видео",
            "«Транскрибировать скачанное» работает с последним успешно загруженным видео. " +
            "Сначала скачайте ролик, затем эта кнопка автоматически будет использовать его как исходник.");

    private void OpenPricingPage(string? planId = null)
    {
        var target = VideoGrabberPricingUrl;
        if (!string.IsNullOrWhiteSpace(planId))
            target += "&plan=" + Uri.EscapeDataString(planId);
        target += "#pricing";
        OpenExternalUri(target);
    }

    private void OpenTelegramContact()
        => OpenExternalUri(VideoGrabberTelegramUrl);

    private static void OpenExternalUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private static string FriendlyPlanName(string? planId)
        => planId switch
        {
            "free" => "Free",
            "start" => "Start",
            "unlimited_video" => "Unlimited Video",
            "full_course" => "Full Course",
            null or "" => "не определён",
            _ => planId
        };
}
