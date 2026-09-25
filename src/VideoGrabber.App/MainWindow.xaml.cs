using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Text;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Editing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Editing;
using VideoGrabber.Infrastructure.Processes;
using Windows.Graphics;
using Windows.Storage.Pickers;

namespace VideoGrabber.App;

public sealed partial class MainWindow : Window
{
    private static string BrandLogoAssetPath => Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "VideoGrabber.png");
    private static readonly SolidColorBrush CardBrush = new(ColorHelper.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush CardBorderBrush = new(ColorHelper.FromArgb(255, 216, 224, 234));
    private static readonly SolidColorBrush AccentBrush = new(ColorHelper.FromArgb(255, 45, 125, 255));
    private static readonly SolidColorBrush MutedBrush = new(ColorHelper.FromArgb(255, 81, 93, 111));
    private static readonly SolidColorBrush TextBrush = new(ColorHelper.FromArgb(255, 31, 41, 55));

    private readonly IProcessRunner _componentRunner = new ProcessRunner();
    private ComponentServices _componentServices = null!;
    private ToolLocator _tools => Volatile.Read(ref _componentServices).Tools;
    private YtDlpDownloader _downloader => Volatile.Read(ref _componentServices).Downloader;
    private FfmpegVideoEditor _editor => Volatile.Read(ref _componentServices).Editor;
    private readonly List<string> _joinFiles = [];

    private Grid _titleBar = null!;
    private Grid _rootHost = null!;
    private ScrollViewer _downloadPage = null!;
    private ScrollViewer _editorPage = null!;
    private ScrollViewer _settingsPage = null!;
#if VIDEOGRABBER_MANAGED
    private ScrollViewer _accountPage = null!;
#endif
    private TextBox _urlBox = null!;
    private TextBox _outputFolderBox = null!;
    private ComboBox _qualityBox = null!;
    private ComboBox _cookiesBox = null!;
    private ComboBox _completionActionBox = null!;
    private CheckBox _audioOnlyBox = null!;
    private Button _downloadButton = null!;
    private Button _cancelButton = null!;
    private readonly List<Button> _pauseButtons = [];
    private bool _operationPaused;
    private Grid _downloadProgressTrack = null!;
    private Border _downloadProgressFill = null!;
    private TextBlock _downloadProgressLabel = null!;
    private double _downloadPercent;
    private TextBlock _downloadStatus = null!;
    private TextBlock _downloadDetails = null!;
    private Border _browserCard = null!;
    private Grid _browserHost = null!;
    private TextBlock _browserHint = null!;
    private TextBox _trimInputBox = null!;
    private TextBox _trimStartBox = null!;
    private TextBox _trimDurationBox = null!;
    private TextBox _trimOutputBox = null!;
    private ListView _joinFilesList = null!;
    private TextBox _joinOutputBox = null!;
    private Border _editorInfo = null!;
    private TextBlock _editorInfoText = null!;
    private TextBlock _ytDlpStatus = null!;
    private TextBlock _ffmpegStatus = null!;
    private TextBlock _ffprobeStatus = null!;
    private TextBlock _denoStatus = null!;
    private TextBlock _whisperStatus = null!;
    private TextBlock _whisperModelStatus = null!;
    private WebView2? _mediaBrowser;
    private CancellationTokenSource? _operation;
    private bool _isInstallingComponents;
    private bool _allowWindowClose;
    private int _lastLoggedProgressBucket = -1;

    public MainWindow()
    {
        AppDiagnostics.Write("MainWindow constructor started");
        InitializeManagedServices();
        _rootHost = new Grid
        {
            Background = RootBackgroundBrush,
            RequestedTheme = ElementTheme.Default
        };
        Content = _rootHost;
        AppDiagnostics.Write("Code-only host initialized");

        var componentRoot = Path.Combine(AppDataRoot, "tools");
        var initialTools = new ToolLocator(AppContext.BaseDirectory, componentRoot);
        if (!initialTools.UsesBundledRuntime)
        {
            var transaction = new ComponentInstallTransaction(_componentRunner);
            transaction.AbortRecoveryAsync(componentRoot, CancellationToken.None).GetAwaiter().GetResult();
            initialTools = new ToolLocator(AppContext.BaseDirectory, componentRoot);
        }
        _componentServices = ComponentServiceFactory.Create(initialTools, _componentRunner, _egressRegistry);
        _rootHost.Children.Add(BuildShell());
        SetOperationControls(false);
#if VIDEOGRABBER_MANAGED
        StartManagedAccountRestore();
#endif
        InitializeTheme();

        Title = AppDisplayName;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "VideoGrabber.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
        AppWindow.Resize(new SizeInt32(1100, 760));
        InitializeDownloadPreferences();
        if (_ytDlpStatus is not null)
        {
            RefreshComponentStatus();
        }
        AppWindow.Closing += (_, args) =>
        {
            if (_allowWindowClose) return;
            if (_operations.IsBusy || _isInstallingComponents)
            {
                args.Cancel = true;
                _operations.RequestClose();
                _browserOperation?.RequestClose();
                _windowLifetime.Cancel();
                SetDownloadState("\u0417\u0430\u0432\u0435\u0440\u0448\u0430\u044e \u0442\u0435\u043a\u0443\u0449\u0443\u044e \u0437\u0430\u0433\u0440\u0443\u0437\u043a\u0443\u2026", "\u041f\u043e\u0441\u043b\u0435 \u043e\u0441\u0442\u0430\u043d\u043e\u0432\u043a\u0438 \u043f\u0440\u043e\u0446\u0435\u0441\u0441\u0430 \u043e\u043a\u043d\u043e \u0437\u0430\u043a\u0440\u043e\u0435\u0442\u0441\u044f \u0430\u0432\u0442\u043e\u043c\u0430\u0442\u0438\u0447\u0435\u0441\u043a\u0438.");
                _operation?.Cancel();
                return;
            }
            _allowWindowClose = true;
        };
        Closed += (_, _) =>
        {
            _windowLifetime.Cancel();
            _logTimer?.Stop();
            DestroyBrowser(forWindowClose: true);
            _routeProxy?.Dispose();
            _routeProxy = null;
        };
        AppDiagnostics.Write("MainWindow constructor completed");
    }

    private Grid BuildShell()
    {
        var shell = new Grid();
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _titleBar = new Grid { Padding = new Thickness(18, 0, 18, 0) };
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        brand.Children.Add(new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Colors.Transparent),
            Child = new Image
            {
                Source = new BitmapImage(new Uri(BrandLogoAssetPath)),
                Stretch = Stretch.Uniform
            }
        });
        brand.Children.Add(new TextBlock
        {
            Text = AppDisplayName,
            FontSize = 18,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        _titleBar.Children.Add(brand);
        shell.Children.Add(_titleBar);

        var contentArea = new Grid();
        contentArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(228) });
        contentArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(contentArea, 1);
        var sidebar = new Grid { Padding = new Thickness(14, 18, 14, 18) };
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var navigation = Vertical(8);
        var downloadItem = NavigationButton("\uE896", "Загрузчик");
        var editorItem = NavigationButton("\uE70F", "Редактор");
        var settingsItem = NavigationButton("\uE713", "Настройки");
#if VIDEOGRABBER_MANAGED
        var accountItem = NavigationButton("\uE77B", "Аккаунт");
#endif
        var infoItem = NavigationButton("\uE946", "Информация");
        downloadItem.Click += (_, _) => ShowPage("download");
        editorItem.Click += (_, _) => ShowPage("editor");
        settingsItem.Click += (_, _) => ShowPage("settings");
#if VIDEOGRABBER_MANAGED
        accountItem.Click += (_, _) => ShowPage("account");
#endif
        infoItem.Click += (_, _) => ShowPage("info");
        navigation.Children.Add(downloadItem);
        navigation.Children.Add(editorItem);
        navigation.Children.Add(settingsItem);
#if VIDEOGRABBER_MANAGED
        navigation.Children.Add(accountItem);
#endif
        navigation.Children.Add(infoItem);
        sidebar.Children.Add(navigation);
        var authorCard = BuildAuthorCard();
        Grid.SetRow(authorCard, 1);
        sidebar.Children.Add(authorCard);
        contentArea.Children.Add(new Border
        {
            Background = CardBrush,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sidebar
        });
        var pageHost = new Grid { Padding = new Thickness(28, 18, 28, 28) };
        Grid.SetColumn(pageHost, 1);
        _downloadPage = BuildDownloadPage();
        _editorPage = BuildEditorPage();
        _settingsPage = BuildSettingsPage();
#if VIDEOGRABBER_MANAGED
        _accountPage = BuildAccountPage();
#endif
        _infoPage = BuildInformationPage();
        _editorPage.Visibility = Visibility.Collapsed;
        _settingsPage.Visibility = Visibility.Collapsed;
#if VIDEOGRABBER_MANAGED
        _accountPage.Visibility = Visibility.Collapsed;
#endif
        _infoPage.Visibility = Visibility.Collapsed;
        pageHost.Children.Add(_downloadPage);
        pageHost.Children.Add(_editorPage);
        pageHost.Children.Add(_settingsPage);
#if VIDEOGRABBER_MANAGED
        pageHost.Children.Add(_accountPage);
#endif
        pageHost.Children.Add(_infoPage);
        contentArea.Children.Add(pageHost);
        shell.Children.Add(contentArea);
        return shell;
    }

    private ScrollViewer BuildDownloadPage()
    {
        var body = PageStack();
        body.Children.Add(PageHeading(
            "Скачать видео",
            "Вставьте ссылку на страницу или прямой поток. Защищённые DRM-потоки не обходятся."));

        _urlBox = new TextBox { Header = "Ссылка на видео", PlaceholderText = "https://…" };
        AttachPasteContextMenu(_urlBox);
        _outputFolderBox = new TextBox { Header = "Папка сохранения", IsReadOnly = true };
        var chooseFolder = SecondaryButton("Выбрать…");
        chooseFolder.Click += BrowseOutputFolder_Click;

        _qualityBox = new ComboBox { Header = "Качество", SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        _qualityBox.Items.Add(ComboItem("Лучшее доступное", "best"));
        _qualityBox.Items.Add(ComboItem("До 4K", "4K"));
        _qualityBox.Items.Add(ComboItem("До 1080p", "1080p"));
        _qualityBox.Items.Add(ComboItem("До 720p", "720p"));
        _qualityBox.Items.Add(ComboItem("До 480p", "480p"));
        _qualityBox.Items.Add(ComboItem("До 360p", "360p"));

        _cookiesBox = new ComboBox { Header = "Вход на сайте", SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        _cookiesBox.Items.Add(ComboItem("Не использовать cookies", ""));
        _cookiesBox.Items.Add(ComboItem("Chrome — только для этой загрузки", "chrome"));
        _cookiesBox.Items.Add(ComboItem("Edge — только для этой загрузки", "edge"));
        _cookiesBox.Items.Add(ComboItem("Firefox — только для этой загрузки", "firefox"));
        _cookiesBox.Items.Add(ComboItem("Встроенный браузер — только эта загрузка", "embedded"));

        _audioOnlyBox = new CheckBox { Content = "Скачать MP3 (только звук)" };
        _audioOnlyBox.Checked += (_, _) => _downloadButton.Content = "Скачать MP3";
        _audioOnlyBox.Unchecked += (_, _) => _downloadButton.Content = "Скачать";

        _completionActionBox = new ComboBox
        {
            Header = "После завершения всех загрузок",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _completionActionBox.Items.Add(ComboItem("Ничего не делать", "none"));
        _completionActionBox.Items.Add(ComboItem("Выключить компьютер", "shutdown"));
        _completionActionBox.Items.Add(ComboItem("Перезагрузить компьютер", "restart"));
        _completionActionBox.Items.Add(ComboItem("Перевести компьютер в спящий режим", "sleep"));
        _completionActionBox.SelectionChanged += (_, _) =>
            CompletionActionSelectionChanged();

        _downloadButton = PrimaryButton("Скачать");
        _downloadButton.Click += Download_Click;
        _cancelButton = DangerButton("Отменить всё");
        _cancelButton.IsEnabled = true;
        _cancelButton.Click += async (_, _) => await CancelOrExplainAsync();
        var openFolderButton = PrimaryButton("Открыть папку загрузок");
        var topPauseButton = PauseButton();
        openFolderButton.Click += OpenOutputFolder_Click;

        var downloadForm = Vertical(14);
        downloadForm.Children.Add(_urlBox);
        downloadForm.Children.Add(TwoColumn(_outputFolderBox, chooseFolder, secondAuto: true));
        downloadForm.Children.Add(TwoColumn(_qualityBox, _cookiesBox));
        downloadForm.Children.Add(_completionActionBox);
        downloadForm.Children.Add(MutedText(
            "Действие выполняется только после успешного завершения всех загрузок на 100%."));
        downloadForm.Children.Add(ResponsiveActions(
            _downloadButton,
            topPauseButton,
            _cancelButton,
            openFolderButton));
        body.Children.Add(Card(downloadForm));

        _downloadStatus = new TextBlock { Text = "Готово к работе", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontWeight = FontWeights.SemiBold };
        _downloadProgressTrack = new Grid
        {
            Height = 8,
            Background = ProgressTrackBrush
        };
        _downloadProgressFill = new Border
        {
            Background = AccentBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(4)
        };
        _downloadProgressTrack.Children.Add(_downloadProgressFill);
        _downloadProgressTrack.SizeChanged += (_, _) => UpdateProgressWidth();
        _downloadProgressLabel = MutedText("0%");
        _downloadDetails = MutedText("Компоненты проверяются перед первой загрузкой.");
        var status = Vertical(10);
        status.Children.Add(_downloadStatus);
        status.Children.Add(_downloadProgressTrack);
        status.Children.Add(_downloadProgressLabel);
        status.Children.Add(_downloadDetails);
        _downloadDetails.IsTextSelectionEnabled = true;
        body.Children.Add(Card(status));

        _browserHint = MutedText("Войдите на сайте и включите видео. Найденные потоки появятся в отдельном списке.");
        _browserHost = new Grid { Height = 440 };
        var closeBrowser = SecondaryButton("Закрыть и выйти");
        closeBrowser.Click += CloseBrowser_Click;
        var browserHeader = new Grid();
        browserHeader.Children.Add(new TextBlock { Text = "Встроенный браузер и поиск потока", FontWeight = FontWeights.SemiBold });
        closeBrowser.HorizontalAlignment = HorizontalAlignment.Right;
        browserHeader.Children.Add(closeBrowser);
        var browserContent = Vertical(10);
        browserContent.Children.Add(browserHeader);
        browserContent.Children.Add(_browserHint);
        AddBrowserControls(browserContent);
        browserContent.Children.Add(_browserHost);
        _browserCard = Card(browserContent);
        _browserCard.Visibility = Visibility.Collapsed;

        body.Children.Add(MutedText(
            "Ниже находятся дополнительные возможности: MP3, курсы GetCourse, отдельные видео со страниц и локальная транскрибация. " +
            "Они скрыты по умолчанию, чтобы основной загрузчик оставался простым."));

        var advancedToggle = SecondaryButton("▾  Развернуть дополнительные возможности");
        advancedToggle.HorizontalAlignment = HorizontalAlignment.Stretch;

        var advancedPanel = Vertical(12);
        advancedPanel.Visibility = Visibility.Collapsed;

        var audioDownload = Vertical(8);
        audioDownload.Children.Add(SectionHeading("MP3 по ссылке"));
        audioDownload.Children.Add(MutedText(
            "Отметьте режим MP3, затем используйте основную кнопку «Скачать» наверху. " +
            "Если функция не входит в тариф, VideoGrabber покажет подходящий тариф."));
        audioDownload.Children.Add(_audioOnlyBox);
        advancedPanel.Children.Add(Card(audioDownload));

        advancedPanel.Children.Add(BuildCourseToolsCard());
        advancedPanel.Children.Add(_browserCard);
        advancedPanel.Children.Add(BuildMediaActionsCard());

        advancedToggle.Click += (_, _) =>
        {
            var expand = advancedPanel.Visibility != Visibility.Visible;
            advancedPanel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
            advancedToggle.Content = expand
                ? "▴  Свернуть дополнительные возможности"
                : "▾  Развернуть дополнительные возможности";
        };

        body.Children.Add(advancedToggle);
        body.Children.Add(advancedPanel);

        return PageScrollViewer(body);
    }

    private ScrollViewer BuildEditorPage()
    {
        var body = PageStack();
        body.Children.Add(PageHeading(
            "Редактор FFmpeg",
            "Быстрая обрезка и склейка без повторного кодирования. Исходные файлы не изменяются."));

        _trimInputBox = new TextBox { Header = "Исходный файл", IsReadOnly = true };
        var chooseInput = SecondaryButton("Выбрать…");
        chooseInput.Click += BrowseTrimInput_Click;
        _trimStartBox = new TextBox { Header = "Начало (чч:мм:сс)", Text = "00:00:00" };
        _trimDurationBox = new TextBox { Header = "Длительность (чч:мм:сс)", Text = "00:00:30" };
        _trimOutputBox = new TextBox { Header = "Новый файл", IsReadOnly = true };
        var chooseTrimOutput = SecondaryButton("Сохранить как…");
        chooseTrimOutput.Click += BrowseTrimOutput_Click;
        var trim = Vertical(12);
        trim.Children.Add(SectionHeading("Обрезать видео"));
        trim.Children.Add(TwoColumn(_trimInputBox, chooseInput, secondAuto: true));
        trim.Children.Add(TwoColumn(_trimStartBox, _trimDurationBox));
        trim.Children.Add(TwoColumn(_trimOutputBox, chooseTrimOutput, secondAuto: true));
        var trimButton = PrimaryButton("Обрезать");
        trimButton.Click += Trim_Click;
        trim.Children.Add(trimButton);
        body.Children.Add(Card(trim));

        _joinFilesList = new ListView { Height = 130, SelectionMode = ListViewSelectionMode.None };
        var addFiles = SecondaryButton("Добавить файлы…");
        addFiles.Click += AddJoinFiles_Click;
        _joinOutputBox = new TextBox { Header = "Новый файл", IsReadOnly = true };
        var chooseJoinOutput = SecondaryButton("Сохранить как…");
        chooseJoinOutput.Click += BrowseJoinOutput_Click;
        var join = Vertical(12);
        join.Children.Add(SectionHeading("Склеить видео"));
        join.Children.Add(_joinFilesList);
        join.Children.Add(addFiles);
        join.Children.Add(TwoColumn(_joinOutputBox, chooseJoinOutput, secondAuto: true));
        var joinButton = PrimaryButton("Склеить");
        joinButton.Click += Join_Click;
        join.Children.Add(joinButton);
        body.Children.Add(Card(join));

        _editorInfoText = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _editorInfo = Card(_editorInfoText);
        _editorInfo.Visibility = Visibility.Collapsed;
        body.Children.Add(_editorInfo);
        var cancelEdit = DangerButton("Отменить всё");
        cancelEdit.Click += async (_, _) => await CancelOrExplainAsync();
        body.Children.Add(cancelEdit);
        return PageScrollViewer(body);
    }

    private ScrollViewer BuildSettingsPage()
    {
        var body = PageStack();
        body.Children.Add(PageHeading(
            "Настройки VideoGrabber",
            "Компоненты, подключение, диагностика и параметры обработки в одном месте."));
        _ytDlpStatus = new TextBlock();
        _ffmpegStatus = new TextBlock();
        _ffprobeStatus = new TextBlock();
        _denoStatus = new TextBlock();
        _whisperStatus = new TextBlock();
        _whisperModelStatus = new TextBlock();
        var content = Vertical(12);
        content.Children.Add(SectionHeading("Комплект VideoGrabber"));
        content.Children.Add(_ytDlpStatus);
        content.Children.Add(_ffmpegStatus);
        content.Children.Add(_ffprobeStatus);
        content.Children.Add(_denoStatus);
        content.Children.Add(_whisperStatus);
        content.Children.Add(_whisperModelStatus);
        content.Children.Add(MutedText(
            "Эти файлы поставляются вместе с VideoGrabber. Программа не должна скачивать их во время обычной работы. Обновляются они вместе с новой версией приложения."));
        var refresh = SecondaryButton("Проверить встроенные инструменты");
        refresh.Click += (_, _) => RefreshComponentStatus();
        content.Children.Add(refresh);
        body.Children.Add(Card(content));
        body.Children.Add(BuildNetworkCard());
        body.Children.Add(BuildDiagnosticsCard());
        body.Children.Add(BuildTranscriptionSettingsCard());
        return PageScrollViewer(body);
    }
    private void ShowPage(string? tag)
    {
        _downloadPage.Visibility = tag is null or "download" ? Visibility.Visible : Visibility.Collapsed;
        _editorPage.Visibility = tag == "editor" ? Visibility.Visible : Visibility.Collapsed;
        _settingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
#if VIDEOGRABBER_MANAGED
        _accountPage.Visibility = tag == "account" ? Visibility.Visible : Visibility.Collapsed;
#endif
        _infoPage.Visibility = tag == "info" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePicker(picker);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            SaveSelectedDownloadFolder(folder.Path);
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = _outputFolderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetDownloadState("Папка загрузок не выбрана.", null, true);
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "explorer.exe"),
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add(folder);
            Process.Start(startInfo);
            AppDiagnostics.Write("Output folder opened");
        }
        catch (Exception exception)
        {
            SetDownloadState("Не удалось открыть папку загрузок.", exception.Message, true);
            AppDiagnostics.Write($"Output folder open failed error={exception.Message}");
        }
    }

    private async void BrowseTrimInput_Click(object sender, RoutedEventArgs e)
    {
        var files = await PickVideoFilesAsync(false);
        if (files.Count != 1) return;
        _trimInputBox.Text = files[0];
        _trimOutputBox.Text = Path.Combine(
            Path.GetDirectoryName(files[0]) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(files[0])}-fragment.mp4");
    }

    private async void BrowseTrimOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickSavePathAsync("fragment.mp4");
        if (path is not null) _trimOutputBox.Text = path;
    }

    private async void Trim_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureFeatureAccessAsync(
                FeatureAccessKind.PaidTools,
                "Обрезка видео доступна на платных тарифах"))
            return;

        if (!File.Exists(_trimInputBox.Text) ||
            string.IsNullOrWhiteSpace(_trimOutputBox.Text) ||
            !TimeSpan.TryParse(_trimStartBox.Text, out var start) ||
            !TimeSpan.TryParse(_trimDurationBox.Text, out var duration))
        {
            ShowEditorMessage("Проверьте исходный файл, путь и время.", InfoBarSeverity.Error);
            return;
        }
        await RunEditAsync(new VideoEditRequest(
            VideoEditMode.FastTrim, new[] { _trimInputBox.Text }, _trimOutputBox.Text, start, duration));
    }

    private async void AddJoinFiles_Click(object sender, RoutedEventArgs e)
    {
        var files = await PickVideoFilesAsync(true);
        if (files.Count == 0) return;
        _joinFiles.AddRange(files);
        _joinFilesList.ItemsSource = _joinFiles.Select(Path.GetFileName).ToList();
        _joinOutputBox.Text = Path.Combine(
            Path.GetDirectoryName(_joinFiles[0]) ?? Environment.CurrentDirectory,
            "joined-video.mp4");
    }

    private async void BrowseJoinOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickSavePathAsync("joined-video.mp4");
        if (path is not null) _joinOutputBox.Text = path;
    }

    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureFeatureAccessAsync(
                FeatureAccessKind.PaidTools,
                "Склейка видео доступна на платных тарифах"))
            return;

        if (_joinFiles.Count < 2 || string.IsNullOrWhiteSpace(_joinOutputBox.Text))
        {
            ShowEditorMessage("Выберите минимум два видео и путь сохранения.", InfoBarSeverity.Error);
            return;
        }
        await RunEditAsync(new VideoEditRequest(VideoEditMode.Join, _joinFiles, _joinOutputBox.Text));
    }

    private async Task RunEditAsync(VideoEditRequest request)
    {
        var managed = CreateLocalOperation("edit",
            request.Mode.ToString(), request.OutputPath,
            string.Join("|", request.Inputs), request.Start?.ToString() ?? "", request.Duration?.ToString() ?? "");
        try
        {
            await _managedCoordinator.RunAsync(managed,
                token => RunOnUiThreadAsync(() => RunAuthorizedEditAsync(request, token)),
                ManagedReport, _windowLifetime.Token);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowEditorMessage("Доступ к редактированию не разрешён: " + ex.Message, InfoBarSeverity.Error);
            await ShowFeatureAccessDialogAsync(
                FeatureAccessKind.PaidTools,
                "Редактор недоступен на текущем тарифе",
                ex.Message);
        }
        catch (OperationCanceledException) { }
    }

    private async Task<OperationOutcome> RunAuthorizedEditAsync(VideoEditRequest request, CancellationToken managedToken)
    {
        if (_operations.IsBusy || _isInstallingComponents)
        {
            ShowEditorMessage("Другая операция уже выполняется. Сначала завершите или отмените её.", InfoBarSeverity.Error);
            return OperationOutcome.Failed;
        }
        if (!_operations.TryBegin()) return OperationOutcome.Failed;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime.Token, managedToken);
        var outcome = OperationOutcome.Failed;
        _operation = operation;
        SetOperationControls(true);
        var components = Volatile.Read(ref _componentServices);
        ShowEditorMessage("FFmpeg обрабатывает видео…", InfoBarSeverity.Informational);
        try
        {
            await components.Editor.EditAsync(request, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            outcome = OperationOutcome.Succeeded;
            ShowEditorMessage($"Проверено: {request.OutputPath}", InfoBarSeverity.Success);
        }
        catch (OperationCanceledException) { outcome = OperationOutcome.Cancelled; ShowEditorMessage("Обработка отменена.", InfoBarSeverity.Informational); }
        catch (Exception exception)
        {
            ShowEditorMessage(SensitiveDataRedactor.Redact(exception.Message), InfoBarSeverity.Error);
            AppDiagnostics.Write("Editor failed: " + exception.Message);
        }
        finally { CompleteOperation(_operations.Complete(outcome)); }
        return outcome;
    }

    private async Task<IReadOnlyList<string>> PickVideoFilesAsync(bool multiple)
    {
        var picker = new FileOpenPicker();
        foreach (var extension in new[] { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".ts", ".m4v" })
            picker.FileTypeFilter.Add(extension);
        InitializePicker(picker);
        if (multiple)
            return (await picker.PickMultipleFilesAsync()).Select(file => file.Path).ToList();
        var file = await picker.PickSingleFileAsync();
        return file is null ? [] : new[] { file.Path };
    }

    private async Task<string?> PickSavePathAsync(string suggestedName)
    {
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName) };
        picker.FileTypeChoices.Add("Видео MP4", new List<string> { ".mp4" });
        InitializePicker(picker);
        return (await picker.PickSaveFileAsync())?.Path;
    }

    private async void InstallComponents_Click(object sender, RoutedEventArgs e)
    {
        if (_operations.IsBusy || _isInstallingComponents)
        {
            _ytDlpStatus.Text = "Сначала завершите текущую операцию.";
            return;
        }
        if (!_operations.TryBegin()) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime.Token);
        _operation = operation;
        SetOperationControls(true);
        var outcome = OperationOutcome.Failed;
        try
        {
            var installed = await InstallComponentsAsync(forceUpdate: true, operation.Token);
            outcome = installed ? OperationOutcome.Succeeded : OperationOutcome.Failed;
        }
        catch (OperationCanceledException)
        {
            outcome = OperationOutcome.Cancelled;
            _ytDlpStatus.Text = "Установка компонентов отменена.";
        }
        finally
        {
            CompleteOperation(_operations.Complete(outcome));
        }
    }

    private async Task<bool> InstallComponentsAsync(bool forceUpdate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!forceUpdate && RequiredComponentsAvailable()) return true;
        if (_isInstallingComponents) return false;

        var script = FindInstallationScript();
        if (script is null)
        {
            _ytDlpStatus.Text = "Установщик компонентов не найден рядом с приложением.";
            AppDiagnostics.Write("Component installer script is missing");
            return false;
        }

        var componentRoot = _tools.LocalToolsDirectory;
        _isInstallingComponents = true;
        _ytDlpStatus.Text = "Подготавливаю проверенный набор компонентов…";
        _ffmpegStatus.Text = "Новый generation не влияет на текущие операции";
        _ffprobeStatus.Text = "Активация выполняется одним pointer swap";
        _denoStatus.Text = "Отмена ждёт rollback/recovery";
        AppDiagnostics.Write("Component transactional preparation started");

        try
        {
            var transaction = new ComponentInstallTransaction(_componentRunner);
            var installer = new ComponentInstaller(_componentRunner, transaction);
            var result = await installer.InstallAsync(script, componentRoot, cancellationToken);
            if (!result.IsSuccess)
            {
                var details = LastNonEmptyLine(result.StandardError) ?? LastNonEmptyLine(result.StandardOutput)
                    ?? "Неизвестная ошибка подготовки компонентов.";
                _ytDlpStatus.Text = $"Ошибка подготовки: {details}";
                return false;
            }

            ComponentServices nextServices;
            try
            {
                var nextTools = new ToolLocator(AppContext.BaseDirectory, componentRoot);
                nextServices = ComponentServiceFactory.Create(nextTools, _componentRunner, _egressRegistry);
            }
            catch
            {
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await transaction.AbortRecoveryAsync(componentRoot, recovery.Token);
                throw;
            }
            Interlocked.Exchange(ref _componentServices, nextServices);
            RefreshComponentStatus();
            AppDiagnostics.Write("Component generation activated in current window");
            return RequiredComponentsAvailable();
        }
        catch (OperationCanceledException)
        {
            AppDiagnostics.Write("Component installation cancelled after recovery");
            throw;
        }
        catch (Exception exception)
        {
            var message = SensitiveDataRedactor.Redact(exception.Message);
            AppDiagnostics.Write("Component installation exception=" + message);
            _ytDlpStatus.Text = $"Ошибка установки: {message}";
            return false;
        }
        finally
        {
            _isInstallingComponents = false;
        }
    }

    private bool RequiredComponentsAvailable() =>
        File.Exists(_tools.YtDlp) &&
        File.Exists(_tools.Ffmpeg) &&
        File.Exists(_tools.Ffprobe) &&
        File.Exists(_tools.Deno);

    private static string? LastNonEmptyLine(string value) =>
        value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

    private void RefreshComponentStatus()
    {
        _ytDlpStatus.Text = ToolStatus("yt-dlp", _tools.YtDlp);
        _ffmpegStatus.Text = ToolStatus("FFmpeg", _tools.Ffmpeg);
        _ffprobeStatus.Text = ToolStatus("FFprobe", _tools.Ffprobe);
        _denoStatus.Text = ToolStatus("Deno", _tools.Deno);
        if (_whisperStatus is not null)
            _whisperStatus.Text = ToolStatus("Whisper", _tools.WhisperCli);
        if (_whisperModelStatus is not null)
            _whisperModelStatus.Text = ToolStatus("Модель Whisper", _tools.WhisperModel);
    }

    private static string ToolStatus(string name, string path) =>
        File.Exists(path) ? $"✓ {name}: {path}" : $"○ {name}: не найден";

    private static string? FindInstallationScript()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "scripts", "Install-Components.ps1");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return null;
    }

    private void InitializePicker(object picker)
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
    }

    private void SetDownloadState(string status, string? details, bool isError = false)
    {
        _downloadStatus.Text = status;
        _downloadDetails.Text = string.IsNullOrWhiteSpace(details) ? " " : details;
        _downloadStatus.Foreground = isError ? new SolidColorBrush(Colors.IndianRed) : TextBrush;
    }

    private void ApplyDownloadProgress(DownloadProgress value)
    {
        if (value.Percent is not null)
        {
            SetProgress(value.Percent.Value);
            var bucket = (int)Math.Floor(Math.Clamp(value.Percent.Value, 0, 100) / 10d);
            if (bucket != _lastLoggedProgressBucket)
            {
                _lastLoggedProgressBucket = bucket;
                AppDiagnostics.Write($"Download progress percent={bucket * 10}");
            }
        }

        UpdateCourseFileProgress(value.Percent);

        var details = string.Join(
            "  •  ",
            new[]
            {
                _courseDownloadActive && !string.IsNullOrWhiteSpace(_courseCurrentVideoName)
                    ? "Файл: " + _courseCurrentVideoName
                    : null,
                value.Speed,
                value.Eta
            }.Where(item => !string.IsNullOrWhiteSpace(item)));

        var status =
            _courseDownloadActive
            && !string.IsNullOrWhiteSpace(_courseCurrentVideoName)
            && string.Equals(value.Status, "Загрузка", StringComparison.OrdinalIgnoreCase)
                ? "Скачиваю видео"
                : value.Status;
        SetDownloadState(status, details);
    }

    private static void AttachPasteContextMenu(TextBox textBox)
    {
        var paste = new MenuFlyoutItem { Text = "Вставить" };
        paste.Click += (_, _) => textBox.PasteFromClipboard();
        var menu = new MenuFlyout();
        menu.Items.Add(paste);
        textBox.ContextFlyout = menu;
    }

    private void ShowEditorMessage(string message, InfoBarSeverity severity)
    {
        _editorInfoText.Text = message;
        _editorInfo.BorderBrush = new SolidColorBrush(severity switch
        {
            InfoBarSeverity.Error => Colors.IndianRed,
            InfoBarSeverity.Success => Colors.MediumSeaGreen,
            _ => Colors.DodgerBlue
        });
        _editorInfo.Visibility = Visibility.Visible;
    }

    private void SetProgress(double? percent)
    {
        if (percent is null)
        {
            _downloadPercent = 0;
            _downloadProgressLabel.Text = "Выполняется…";
        }
        else
        {
            _downloadPercent = Math.Clamp(percent.Value, 0, 100);
            _downloadProgressLabel.Text = $"{_downloadPercent:0}%";
        }
        UpdateProgressWidth();
    }

    private void UpdateProgressWidth()
    {
        if (_downloadProgressTrack is null || _downloadProgressFill is null)
        {
            return;
        }
        _downloadProgressFill.Width = _downloadProgressTrack.ActualWidth * _downloadPercent / 100d;
    }

    private static ComboBoxItem ComboItem(string text, string tag) => new() { Content = text, Tag = tag };

    private static StackPanel PageStack() => new()
    {
        MaxWidth = 900,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Spacing = 18
    };

    private static StackPanel Vertical(double spacing) => new() { Spacing = spacing };

    private static StackPanel Horizontal(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private static Grid ResponsiveActions(params Button[] buttons)
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        for (var index = 0; index < buttons.Length; index++)
        {
            var row = index / 2;
            while (grid.RowDefinitions.Count <= row)
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = GridLength.Auto
                });

            var button = buttons[index];
            button.Width = double.NaN;
            button.MinWidth = 0;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetRow(button, row);
            Grid.SetColumn(button, index % 2);
            if (index == buttons.Length - 1 && buttons.Length % 2 == 1)
                Grid.SetColumnSpan(button, 2);
            grid.Children.Add(button);
        }

        return grid;
    }

    private static ScrollViewer PageScrollViewer(UIElement content)
    {
        var viewer = new ScrollViewer
        {
            Content = content,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        return viewer;
    }

    private static Grid TwoColumn(FrameworkElement first, FrameworkElement second, bool secondAuto = false)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = secondAuto ? GridLength.Auto : new GridLength(1, GridUnitType.Star)
        });
        second.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(second, 1);
        grid.Children.Add(first);
        grid.Children.Add(second);
        return grid;
    }

    private static Border Card(UIElement child) => new()
    {
        Background = CardBrush,
        BorderBrush = CardBorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(16),
        Padding = new Thickness(22),
        Child = child
    };

    private static StackPanel PageHeading(string title, string subtitle)
    {
        var panel = Vertical(6);
        panel.Children.Add(new TextBlock { Text = title, FontSize = 25, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(MutedText(subtitle));
        return panel;
    }

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeights.SemiBold,
        Foreground = TextBrush
    };

    private static TextBlock MutedText(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = MutedBrush
    };

    private Button PauseButton()
    {
        var button = SecondaryButton("⏸  Пауза");
        button.Width = 145;
        button.MinWidth = 145;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.FontWeight = FontWeights.SemiBold;
        button.IsEnabled = true;
        button.IsHitTestVisible = true;
        button.IsTabStop = true;
        button.Tag = false;
        button.Opacity = 1;
        ToolTipService.SetToolTip(
            button,
            "Во время операции — поставить на паузу. Если работа ещё не запущена, нажмите для пояснения.");
        button.Click += async (_, _) => await TogglePauseOrExplainAsync();
        _pauseButtons.Add(button);
        UpdatePauseButtonVisual(button);
        return button;
    }

    private void TogglePause()
    {
        if (!_operations.IsBusy && !_courseDownloadActive)
            return;
        _operationPaused = !_operationPaused;
        if (_operationPaused)
            ProcessPauseRegistry.PauseAll();
        else
            ProcessPauseRegistry.ResumeAll();
        foreach (var button in _pauseButtons)
            UpdatePauseButtonVisual(button);
    }

    private void ResetPauseState()
    {
        _operationPaused = false;
        ProcessPauseRegistry.ResumeAll();
        foreach (var button in _pauseButtons)
            UpdatePauseButtonVisual(button);
    }

    private void UpdatePauseButtonsAvailability(bool busy)
    {
        foreach (var button in _pauseButtons)
        {
            // Keep the caption fully visible even while pause is unavailable.
            // WinUI dims disabled button content too aggressively on Windows 10.
            button.IsEnabled = true;
            button.IsHitTestVisible = true;
            button.IsTabStop = true;
            button.Tag = busy;
            button.Opacity = 1;
            UpdatePauseButtonVisual(button);
        }
        if (!busy && _operationPaused)
            ResetPauseState();
    }

    private static void UpdatePauseButtonVisual(Button button)
    {
        if (ProcessPauseRegistry.IsPaused)
        {
            button.Content = "▶  Продолжить";
            button.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 22, 163, 74));
            button.Foreground = new SolidColorBrush(Colors.White);
        }
        else if (button.Tag is true)
        {
            button.Content = "⏸  Пауза";
            button.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 250, 204, 21));
            button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 31, 41, 55));
        }
        else
        {
            button.Content = "⏸  Пауза";
            button.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 71, 85, 105));
            button.Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private async Task WaitIfPausedAsync(CancellationToken token)
    {
        while (_operationPaused)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(120, token);
        }
    }
    private static Button PrimaryButton(string text) => new()
    {
        Content = text,
        Background = AccentBrush,
        Foreground = new SolidColorBrush(Colors.White),
        HorizontalAlignment = HorizontalAlignment.Left,
        Padding = new Thickness(18, 9, 18, 9),
        CornerRadius = new CornerRadius(8)
    };

    private static Button SecondaryButton(string text) => new()
    {
        Content = text,
        Padding = new Thickness(16, 8, 16, 8),
        CornerRadius = new CornerRadius(8)
    };

    private static Button DangerButton(string text) => new()
    {
        Content = text,
        Background = new SolidColorBrush(Colors.IndianRed),
        Foreground = new SolidColorBrush(Colors.White),
        Padding = new Thickness(16, 8, 16, 8),
        CornerRadius = new CornerRadius(8)
    };

    private static Button BrowserActionButton(string text) => new()
    {
        Content = text,
        Background = new SolidColorBrush(
            ColorHelper.FromArgb(255, 24, 94, 61)),
        Foreground = new SolidColorBrush(Colors.White),
        Padding = new Thickness(18, 9, 18, 9),
        CornerRadius = new CornerRadius(8)
    };

    private static Button NavigationButton(string glyph, string text)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 17,
            Foreground = AccentBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        return new Button
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 11, 14, 11),
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Colors.Transparent)
        };
    }

    private static Border BuildAuthorCard()
    {
        var content = Vertical(6);
        content.Children.Add(new TextBlock
        {
            Text = "Создано Валерием",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush
        });
        var telegramContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        telegramContent.Children.Add(new FontIcon
        {
            Glyph = "\uE724",
            FontSize = 16,
            Foreground = AccentBrush
        });
        telegramContent.Children.Add(new TextBlock
        {
            Text = "Telegram · @Velkoshkin",
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var telegramLink = new HyperlinkButton
        {
            Content = telegramContent,
            NavigateUri = new Uri("https://t.me/Velkoshkin"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(0, 2, 0, 2),
            MinHeight = 32,
            Foreground = AccentBrush
        };
        ToolTipService.SetToolTip(telegramLink, "Открыть контакт @Velkoshkin в Telegram");
        content.Children.Add(telegramLink);

        return new Border
        {
            Margin = new Thickness(0, 18, 0, 0),
            Padding = new Thickness(12, 10, 12, 8),
            CornerRadius = new CornerRadius(10),
            Background = AuthorBackgroundBrush,
            BorderBrush = AuthorBorderBrush,
            BorderThickness = new Thickness(1),
            Child = content
        };
    }
}
