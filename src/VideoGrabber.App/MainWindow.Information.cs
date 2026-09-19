using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VideoGrabber.Infrastructure.Settings;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private static readonly SolidColorBrush RootBackgroundBrush = new(ColorHelper.FromArgb(255, 245, 247, 251));
    private static readonly SolidColorBrush ProgressTrackBrush = new(ColorHelper.FromArgb(255, 226, 232, 240));
    private static readonly SolidColorBrush AuthorBackgroundBrush = new(ColorHelper.FromArgb(255, 238, 244, 255));
    private static readonly SolidColorBrush AuthorBorderBrush = new(ColorHelper.FromArgb(255, 205, 220, 248));
    private static readonly SolidColorBrush BadgeBrush = new(ColorHelper.FromArgb(255, 232, 240, 255));
    private ScrollViewer _infoPage = null!;
    private ComboBox _themeBox = null!;
    private bool _themeApplying;

    private static string ThemeSettingsPath => Path.Combine(AppDataRoot, "ui-settings.json");

    private ScrollViewer BuildInformationPage()
    {
        var body = PageStack();
        body.Children.Add(PageHeading("ⓘ Информация", "О программе, поддерживаемых источниках и правильном скачивании."));

        var about = Vertical(8);
        about.Children.Add(SectionHeading("О программе"));
        about.Children.Add(new TextBlock { Text = "VideoGrabber " + CurrentVersion(), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        about.Children.Add(MutedText("Создано Валерием · Telegram: @Velkoshkin"));
        about.Children.Add(MutedText("Видео обрабатывается локально. Пароли приложение не читает и не сохраняет. DRM не обходится."));
        body.Children.Add(Card(about));

        var capabilities = Vertical(8);
        capabilities.Children.Add(SectionHeading("Что умеет VideoGrabber"));
        capabilities.Children.Add(MutedText("• GetCourse, включая школы на собственных доменах: поиск HLS master, выбор качества и загрузка через встроенный браузер."));
        capabilities.Children.Add(MutedText("• Kinescope и обычный HLS/DASH/MP4 без DRM; split video/audio объединяется FFmpeg."));
        capabilities.Children.Add(MutedText("• Другие сайты, которые поддерживает установленный yt-dlp, если у пользователя есть доступ к видео."));
        capabilities.Children.Add(MutedText("• MP3, быстрая обрезка/склейка, локальная расшифровка встроенным Whisper и точечная маршрутизация сайтов."));
        capabilities.Children.Add(MutedText("• Полный комплект уже содержит yt-dlp, FFmpeg, FFprobe, Deno, Whisper и модель распознавания — отдельная установка для обычной работы не требуется."));
        body.Children.Add(Card(capabilities));
        var getCourse = Vertical(8);
        getCourse.Children.Add(SectionHeading("Как скачать с GetCourse"));
        getCourse.Children.Add(MutedText("1. Вставьте ссылку на урок и нажмите «Открыть во встроенном браузере»."));
        getCourse.Children.Add(MutedText("2. Если сайт просит вход, введите логин и пароль прямо на странице GetCourse. VideoGrabber пароль не получает."));
        getCourse.Children.Add(MutedText("3. Дождитесь списка «Видео 01, Видео 02…». Запускать каждое видео вручную не требуется."));
        getCourse.Children.Add(MutedText("4. Для отдельного видео выберите нужное качество. Для всего курса есть отдельный предел: до 360p, до 480p, до 720p или лучшее доступное; если точной высоты нет, берётся ближайшая доступная ниже предела."));
        getCourse.Children.Add(MutedText("5. Нажмите «Скачать выбранное видео» или «Скачать все найденные». Перед загрузкой выбранная HLS-дорожка автоматически проверяется."));
        getCourse.Children.Add(MutedText("6. «Скачать весь курс» проходит модули и уроки, сохраняет Word/HTML/изображения/вложения/видео, перед запуском сверяет уже скачанное, а в конце выполняет полную проверку и автоматически повторяет пропуски. Кнопка «Пауза» не отменяет работу и превращается в «Продолжить»."));
        getCourse.Children.Add(MutedText("Если master получить не удалось, запустите ролик на несколько секунд: это включает резервное обнаружение media-потока."));
        body.Children.Add(Card(getCourse));

        var limits = Vertical(8);
        limits.Children.Add(SectionHeading("Авторизация и ограничения"));
        limits.Children.Add(MutedText("Для закрытых уроков используйте встроенный InPrivate-браузер. Cookies передаются только для выбранной загрузки и затем сбрасываются."));
        limits.Children.Add(MutedText("DRM, ключи шифрования и обход ограничений доступа не поддерживаются. Если сайт запрещает сохранение или выдаёт зашифрованный поток, VideoGrabber его блокирует."));
        body.Children.Add(Card(limits));

        var appearance = Vertical(8);
        appearance.Children.Add(SectionHeading("Оформление"));
        _themeBox = new ComboBox { Header = "Тема приложения", HorizontalAlignment = HorizontalAlignment.Stretch };
        _themeBox.Items.Add(ComboItem("Как в Windows", "system"));
        _themeBox.Items.Add(ComboItem("Светлая", "light"));
        _themeBox.Items.Add(ComboItem("Тёмная", "dark"));
        _themeBox.SelectionChanged += (_, _) => ThemeSelectionChanged();
        appearance.Children.Add(_themeBox);
        appearance.Children.Add(MutedText("Тема встроенной страницы сайта определяется самим сайтом; VideoGrabber не вмешивается в стили чужого плеера."));
        body.Children.Add(Card(appearance));
        return new ScrollViewer { Content = body };
    }

    private void InitializeTheme()
    {
        var mode = AppThemeSettings.Load(ThemeSettingsPath);
        _themeApplying = true;
        _themeBox.SelectedIndex = mode switch { AppThemeMode.Light => 1, AppThemeMode.Dark => 2, _ => 0 };
        _themeApplying = false;
        ApplyTheme(mode, save: false);
    }

    private void ThemeSelectionChanged()
    {
        if (_themeApplying || _themeBox.SelectedItem is not ComboBoxItem item) return;
        var mode = AppThemeSettings.Parse(item.Tag?.ToString());
        ApplyTheme(mode, save: true);
    }

    private void ApplyTheme(AppThemeMode mode, bool save)
    {
        var dark = mode == AppThemeMode.Dark ||
            (mode == AppThemeMode.System && Application.Current.RequestedTheme == ApplicationTheme.Dark);
        _rootHost.RequestedTheme = mode switch
        {
            AppThemeMode.Dark => ElementTheme.Dark,
            AppThemeMode.Light => ElementTheme.Light,
            _ => ElementTheme.Default
        };
        if (dark) ApplyDarkPalette(); else ApplyLightPalette();
        _rootHost.Background = RootBackgroundBrush;
        if (save) AppThemeSettings.Save(ThemeSettingsPath, mode);
    }

    private static void ApplyDarkPalette()
    {
        RootBackgroundBrush.Color = ColorHelper.FromArgb(255, 17, 24, 39);
        CardBrush.Color = ColorHelper.FromArgb(255, 31, 41, 55);
        CardBorderBrush.Color = ColorHelper.FromArgb(255, 55, 65, 81);
        TextBrush.Color = ColorHelper.FromArgb(255, 243, 244, 246);
        MutedBrush.Color = ColorHelper.FromArgb(255, 203, 213, 225);
        ProgressTrackBrush.Color = ColorHelper.FromArgb(255, 55, 65, 81);
        AuthorBackgroundBrush.Color = ColorHelper.FromArgb(255, 30, 58, 95);
        AuthorBorderBrush.Color = ColorHelper.FromArgb(255, 59, 130, 246);
        BadgeBrush.Color = ColorHelper.FromArgb(255, 30, 58, 95);
    }

    private static void ApplyLightPalette()
    {
        RootBackgroundBrush.Color = ColorHelper.FromArgb(255, 245, 247, 251);
        CardBrush.Color = ColorHelper.FromArgb(255, 255, 255, 255);
        CardBorderBrush.Color = ColorHelper.FromArgb(255, 216, 224, 234);
        TextBrush.Color = ColorHelper.FromArgb(255, 31, 41, 55);
        MutedBrush.Color = ColorHelper.FromArgb(255, 81, 93, 111);
        ProgressTrackBrush.Color = ColorHelper.FromArgb(255, 226, 232, 240);
        AuthorBackgroundBrush.Color = ColorHelper.FromArgb(255, 238, 244, 255);
        AuthorBorderBrush.Color = ColorHelper.FromArgb(255, 205, 220, 248);
        BadgeBrush.Color = ColorHelper.FromArgb(255, 232, 240, 255);
    }

    private static string CurrentVersion()
    {
        var info = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info)) return info.Split('+')[0];
        return typeof(App).Assembly.GetName().Version?.ToString() ?? "неизвестная версия";
    }
}
