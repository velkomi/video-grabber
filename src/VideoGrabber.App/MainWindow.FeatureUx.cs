using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private string VideoGrabberPricingUrl =>
        _activeClientServices.WebsiteBase.AbsoluteUri + "?from=desktop";
    private const string VideoGrabberTelegramUrl =
        "https://t.me/Velkoshkin";

    private enum FeatureAccessKind
    {
        IndividualDownload,
        PaidTools,
        FullCourse
    }

    private bool HasFeatureAccess(
        FeatureAccessKind feature,
        string? featureOverride = null)
    {
#if VIDEOGRABBER_MANAGED
        if (string.IsNullOrWhiteSpace(_managedAccessToken)
            || _managedAccessSnapshot is null)
            return false;

        if (!string.IsNullOrWhiteSpace(featureOverride)
            && _managedAccessSnapshot.FeatureOverrides.TryGetValue(
                featureOverride,
                out var overridden))
            return overridden;

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
        string actionTitle,
        string? featureOverride = null)
    {
        if (HasFeatureAccess(feature, featureOverride))
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
                "Для целого курса нужен Full Course. " +
                "Он включает видео без лимита, уроки и вложения по папкам.",
            FeatureAccessKind.PaidTools =>
                "MP3, редактор и расшифровка речи " +
                "доступны на платных тарифах.",
            _ =>
                "Бесплатно можно скачать 10 видео за всё время аккаунта. " +
                "Затем выберите Start или тариф с видео без лимита."
        };

        var current = signedIn
            ? $"Текущий тариф: {FriendlyPlanName(access?.PlanId)}. " +
              $"Осталось загрузок: {access?.RemainingDownloads ?? 0}."
            : "Сначала войдите в VideoGrabber.";

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
                "Start — до 10 загрузок в сутки, MP3, редактор и расшифровка речи.\n" +
                "Unlimited Video — видео без лимита, MP3, редактор и расшифровка речи.\n" +
                "Full Course — всё выше + скачивание полного курса.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85
        });
        if (!string.IsNullOrWhiteSpace(serverReason))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Действие сейчас недоступно. Обновите данные аккаунта и проверьте свой тариф.",
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
        if (!_operations.IsBusy && !_courseDownloadActive && !IsCourseTranscriptionBusy && !_isInstallingComponents)
        {
            await ShowOperationalHelpAsync(
                "Когда нужна «Пауза»",
                "Начните загрузку или обработку файла. «Пауза» приостановит её, " +
                "а «Продолжить» возобновит. Очередь и готовые файлы сохранятся.");
            return;
        }

        TogglePause();
    }

    private async Task CancelOrExplainAsync()
    {
        if (!_operations.IsBusy && !_courseDownloadActive && !IsCourseTranscriptionBusy && !_isInstallingComponents)
        {
            await ShowOperationalHelpAsync(
                "Сейчас нечего отменять",
                "«Отменить всё» останавливает текущую загрузку или обработку. " +
                "Очередь и готовые файлы сохраняются.");
            return;
        }

        CancelOperation();
    }

    private async Task ExplainMissingDownloadedMediaAsync()
        => await ShowOperationalHelpAsync(
            "Сначала скачайте видео",
            "Эта кнопка создаёт текст из последнего скачанного видео. Сначала загрузите ролик.");

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
            _ => "другой тариф"
        };
    private static string FriendlyProviderName(string provider) => provider.ToLowerInvariant() switch
    {
        "google" => "Google",
        "email" => "Почта",
        "telegram" => "Telegram",
        "apple" => "Apple",
        "yandex" => "Яндекс",
        _ => "Другой способ входа"
    };

}
