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
        body.Children.Add(PageHeading("О VideoGrabber", "Ваше видео — в ваших руках."));

        var about = Vertical(8);
        about.Children.Add(SectionHeading("О программе"));
        about.Children.Add(new TextBlock { Text = "VideoGrabber " + CurrentVersion(), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        about.Children.Add(MutedText("Создано Валерием"));
        about.Children.Add(StudioLink("Сайт VideoGrabber ↗", "https://videograbber.srv1902378.hstgr.cloud/web/"));
        about.Children.Add(StudioLink("Бот @VideoGra_bot ↗", "https://t.me/VideoGra_bot"));
        about.Children.Add(StudioLink("Разработчик @Velkoshkin ↗", "https://t.me/Velkoshkin"));
        about.Children.Add(MutedText("Файлы сохраняются и обрабатываются на вашем компьютере. Пароли приложение не читает. Защищённое видео не скачивается."));
        body.Children.Add(Card(about));

        var capabilities = Vertical(8);
        capabilities.Children.Add(SectionHeading("Что умеет VideoGrabber"));
        capabilities.Children.Add(MutedText("• Видео с поддерживаемых сайтов: качество на выбор, пауза и продолжение загрузки."));
        capabilities.Children.Add(MutedText("• Курсы GetCourse: уроки, видео и вложения в отдельных папках."));
        capabilities.Children.Add(MutedText("• MP3, обрезка и склейка видео, расшифровка речи в текст."));
        capabilities.Children.Add(MutedText("• Проверка готовых файлов и повтор незавершённых загрузок."));
        body.Children.Add(Details("Видео, MP3 и курсы", capabilities));

        var surfaces = Vertical(8);
        surfaces.Children.Add(SectionHeading("Сайт, Windows и Telegram"));
        surfaces.Children.Add(MutedText("• Сайт сохраняет готовые публичные MP4/WebM по прямой ссылке. Telegram принимает небольшие готовые MP4."));
        surfaces.Children.Add(MutedText("• Для других ссылок, MP3, закрытых уроков и целых курсов используйте Windows-приложение."));
        surfaces.Children.Add(MutedText("• MP3, курсы и видео со страниц находятся в «Дополнительных возможностях»."));
        surfaces.Children.Add(MutedText("• Открытое приложение принимает задания, отправленные именно на этот компьютер. Отключить приём можно в «Аккаунте»."));
        body.Children.Add(Details("Где пользоваться VideoGrabber", surfaces));

#if VIDEOGRABBER_MANAGED
        var plans = Vertical(10);
        plans.Children.Add(SectionHeading("Тарифы и доступ"));
        plans.Children.Add(MutedText(
            "Один аккаунт и один тариф действуют одновременно на сайте, в Windows VideoGrabber и в Telegram."));
        plans.Children.Add(new TextBlock
        {
            Text = "Free — 0 ₽",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        plans.Children.Add(MutedText(
            "10 обычных загрузок видео за всё время аккаунта. Полный курс, MP3, редактор и транскрибация не входят."));
        plans.Children.Add(new TextBlock
        {
            Text = "Start — 1 500 ₽ / 30 дней",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        plans.Children.Add(MutedText(
            "До 10 загрузок в сутки. Включены MP3, редактор и локальная транскрибация. Полный курс не входит."));
        plans.Children.Add(new TextBlock
        {
            Text = "Unlimited Video — 2 500 ₽ / 30 дней",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        plans.Children.Add(MutedText(
            "Отдельные видео без лимита + MP3, редактор и локальная транскрибация. Полный курс не входит."));
        plans.Children.Add(new TextBlock
        {
            Text = "Full Course — 5 000 ₽ / 30 дней",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush
        });
        plans.Children.Add(MutedText(
            "Видео без лимита, MP3, редактор, расшифровка речи и целые курсы GetCourse. Текст уроков можно сохранять рядом с видео."));
        plans.Children.Add(MutedText(
            "Если функция не входит в тариф, приложение предложит подходящий. Оплата требует вашего подтверждения на сайте."));
        var plansButton = PrimaryButton("Тарифы и оплата на сайте");
        plansButton.Click += (_, _) => OpenPricingPage();
        plans.Children.Add(plansButton);
        body.Children.Add(Details("Тарифы и возможности", plans));
#endif

        var controlsHelp = Vertical(8);
        controlsHelp.Children.Add(SectionHeading("Пауза и отмена"));
        controlsHelp.Children.Add(MutedText(
            "• «Пауза» приостанавливает работу. Нажмите «Продолжить», чтобы возобновить её."));
        controlsHelp.Children.Add(MutedText(
            "• «Отменить всё» останавливает работу. Готовые файлы остаются на компьютере."));
        controlsHelp.Children.Add(MutedText(
            "• Если тариф не подходит, приложение объяснит ограничение и предложит другой."));
        controlsHelp.Children.Add(MutedText(
            "• Если нужен файл или открытый урок, появится подсказка с нужным шагом."));
        body.Children.Add(Details("Кнопки и управление", controlsHelp));

        var getCourse = Vertical(8);
        getCourse.Children.Add(SectionHeading("Как скачать с GetCourse"));
        getCourse.Children.Add(MutedText("1. Вставьте ссылку на урок и нажмите «Открыть во встроенном браузере»."));
        getCourse.Children.Add(MutedText("2. Войдите на странице GetCourse, если урок закрытый. VideoGrabber пароль не получает."));
        getCourse.Children.Add(MutedText("3. Дождитесь списка «Видео 01, Видео 02…». Запускать каждое видео вручную не требуется."));
        getCourse.Children.Add(MutedText("4. Выберите качество видео. Для всего курса задайте предел: 360p, 480p, 720p или лучшее доступное. Если нужного качества нет, берётся ближайшее ниже предела."));
        getCourse.Children.Add(MutedText("5. Нажмите «Скачать выбранное видео» или «Скачать все найденные»."));
        getCourse.Children.Add(MutedText("6. «Скачать весь курс» сохраняет уроки, вложения и видео по папкам. Готовые файлы проверяются, пропуски повторяются. Работу можно поставить на паузу и продолжить."));
        getCourse.Children.Add(MutedText("Если видео не определяется сразу, запустите его на странице на несколько секунд — VideoGrabber попробует найти его повторно."));
        body.Children.Add(Details("Как скачать курс", getCourse));

        var limits = Vertical(8);
        limits.Children.Add(SectionHeading("Вход и ограничения"));
        limits.Children.Add(MutedText("Для закрытых уроков войдите во встроенном браузере. Данные входа используются только для выбранной загрузки и затем сбрасываются."));
        limits.Children.Add(MutedText("Скачивайте материалы, к которым у вас есть доступ. Видео с защитой от копирования не поддерживается."));
        body.Children.Add(Details("Ограничения", limits));

        return PageScrollViewer(body);
    }

    private FrameworkElement BuildAppearanceCard()
    {
        var appearance = Vertical(8);
        appearance.Children.Add(SectionHeading("Оформление"));
        _themeBox = new ComboBox { Header = "Тема приложения", HorizontalAlignment = HorizontalAlignment.Stretch };
        _themeBox.Items.Add(ComboItem("Как в Windows", "system"));
        _themeBox.Items.Add(ComboItem("Светлая", "light"));
        _themeBox.Items.Add(ComboItem("Тёмная", "dark"));
        _themeBox.SelectionChanged += (_, _) => ThemeSelectionChanged();
        appearance.Children.Add(_themeBox);
        appearance.Children.Add(MutedText("Оформление открытого сайта остаётся таким, каким его сделал сам сайт."));
        return Card(appearance);
    }

    private void InitializeTheme()
    {
        var mode = File.Exists(ThemeSettingsPath) ? AppThemeSettings.Load(ThemeSettingsPath) : AppThemeMode.Dark;
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
        StudioLinkBrush.Color = ColorHelper.FromArgb(255, 97, 168, 255);
        RootBackgroundBrush.Color = ColorHelper.FromArgb(255, 6, 11, 23);
        CardBrush.Color = ColorHelper.FromArgb(255, 12, 24, 48);
        CardBorderBrush.Color = ColorHelper.FromArgb(255, 42, 64, 99);
        TextBrush.Color = ColorHelper.FromArgb(255, 247, 249, 255);
        MutedBrush.Color = ColorHelper.FromArgb(255, 169, 183, 211);
        ProgressTrackBrush.Color = ColorHelper.FromArgb(255, 42, 64, 99);
        AuthorBackgroundBrush.Color = ColorHelper.FromArgb(255, 18, 40, 74);
        AuthorBorderBrush.Color = ColorHelper.FromArgb(255, 59, 130, 246);
        BadgeBrush.Color = ColorHelper.FromArgb(255, 18, 40, 74);
    }

    private static void ApplyLightPalette()
    {
        StudioLinkBrush.Color = ColorHelper.FromArgb(255, 24, 91, 173);
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
