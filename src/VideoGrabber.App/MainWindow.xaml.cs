using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    private static readonly SolidColorBrush CardBrush = new(ColorHelper.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush CardBorderBrush = new(ColorHelper.FromArgb(255, 216, 224, 234));
    private static readonly SolidColorBrush AccentBrush = new(ColorHelper.FromArgb(255, 45, 125, 255));
    private static readonly SolidColorBrush MutedBrush = new(ColorHelper.FromArgb(255, 81, 93, 111));
    private static readonly SolidColorBrush TextBrush = new(ColorHelper.FromArgb(255, 31, 41, 55));

    private readonly ToolLocator _tools = new();
    private readonly YtDlpDownloader _downloader;
    private readonly FfmpegVideoEditor _editor;
    private readonly List<string> _joinFiles = [];

    private Grid _titleBar = null!;
    private Grid _rootHost = null!;
    private ScrollViewer _downloadPage = null!;
    private ScrollViewer _editorPage = null!;
    private ScrollViewer _settingsPage = null!;
    private TextBox _urlBox = null!;
    private TextBox _outputFolderBox = null!;
    private ComboBox _qualityBox = null!;
    private ComboBox _cookiesBox = null!;
    private CheckBox _audioOnlyBox = null!;
    private Button _downloadButton = null!;
    private Button _cancelButton = null!;
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
    private WebView2? _mediaBrowser;
    private CancellationTokenSource? _operation;
    private bool _isInstallingComponents;
    private bool _allowWindowClose;
    private int _lastLoggedProgressBucket = -1;

    public MainWindow()
    {
        AppDiagnostics.Write("MainWindow constructor started");
        _rootHost = new Grid
        {
            Background = RootBackgroundBrush,
            RequestedTheme = ElementTheme.Default
        };
        Content = _rootHost;
        AppDiagnostics.Write("Code-only host initialized");

        var runner = new ProcessRunner();
        _downloader = new YtDlpDownloader(runner, _tools);
        _editor = new FfmpegVideoEditor(runner, _tools);
        _rootHost.Children.Add(BuildShell());
        InitializeTheme();

        Title = "VideoGrabber";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "VideoGrabber.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
        AppWindow.Resize(new SizeInt32(1100, 760));
        if (_outputFolderBox is not null)
        {
            _outputFolderBox.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "VideoGrabber");
        }
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
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(9),
            Background = AccentBrush,
            Child = new FontIcon { Glyph = "\uE896", Foreground = new SolidColorBrush(Colors.White) }
        });
        brand.Children.Add(new TextBlock
        {
            Text = "VideoGrabber",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        brand.Children.Add(new Border
        {
            Padding = new Thickness(9, 4, 9, 4),
            CornerRadius = new CornerRadius(9),
            Background = BadgeBrush,
            Child = new TextBlock { Text = "локально на вашем ПК", FontSize = 11, Foreground = TextBrush }
        });
        _titleBar.Children.Add(brand);
        shell.Children.Add(_titleBar);

        var contentArea = new Grid();
        contentArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        contentArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(contentArea, 1);
        var sidebar = new Grid { Padding = new Thickness(14, 18, 14, 18) };
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var navigation = Vertical(8);
        var downloadItem = NavigationButton("↓  Загрузчик");
        var editorItem = NavigationButton("✂  Редактор");
        var settingsItem = NavigationButton("⚙  Компоненты");
        var infoItem = NavigationButton("ⓘ  Информация");
        downloadItem.Click += (_, _) => ShowPage("download");
        editorItem.Click += (_, _) => ShowPage("editor");
        settingsItem.Click += (_, _) => ShowPage("settings");
        infoItem.Click += (_, _) => ShowPage("info");
        navigation.Children.Add(downloadItem);
        navigation.Children.Add(editorItem);
        navigation.Children.Add(settingsItem);
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
        _infoPage = BuildInformationPage();
        _editorPage.Visibility = Visibility.Collapsed;
        _settingsPage.Visibility = Visibility.Collapsed;
        _infoPage.Visibility = Visibility.Collapsed;
        pageHost.Children.Add(_downloadPage);
        pageHost.Children.Add(_editorPage);
        pageHost.Children.Add(_settingsPage);
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
        _downloadButton = PrimaryButton("Скачать");
        _downloadButton.Click += Download_Click;
        _cancelButton = SecondaryButton("Отменить");
        _cancelButton.IsEnabled = false;
        _cancelButton.Click += (_, _) => CancelOperation();
        var browserButton = SecondaryButton("Открыть во встроенном браузере");
        browserButton.Click += OpenBrowser_Click;
        var openFolderButton = SecondaryButton("Открыть папку загрузок");
        openFolderButton.Click += OpenOutputFolder_Click;

        var downloadForm = Vertical(14);
        downloadForm.Children.Add(_urlBox);
        downloadForm.Children.Add(TwoColumn(_outputFolderBox, chooseFolder, secondAuto: true));
        downloadForm.Children.Add(TwoColumn(_qualityBox, _cookiesBox));
        downloadForm.Children.Add(_audioOnlyBox);
        downloadForm.Children.Add(Horizontal(_downloadButton, _cancelButton, openFolderButton));
        downloadForm.Children.Add(browserButton);
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
        body.Children.Add(_browserCard);
        body.Children.Add(BuildMediaActionsCard());

        return new ScrollViewer { Content = body };
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
        var cancelEdit = SecondaryButton("Отменить обработку");
        cancelEdit.Click += (_, _) => CancelOperation();
        body.Children.Add(cancelEdit);
        return new ScrollViewer { Content = body };
    }

    private ScrollViewer BuildSettingsPage()
    {
        var body = PageStack();
        body.Children.Add(PageHeading(
            "Компоненты",
            "Инструменты хранятся в профиле текущего пользователя и обновляются по вашему нажатию."));
        _ytDlpStatus = new TextBlock();
        _ffmpegStatus = new TextBlock();
        _ffprobeStatus = new TextBlock();
        _denoStatus = new TextBlock();
        var content = Vertical(12);
        content.Children.Add(SectionHeading("Локальные инструменты"));
        content.Children.Add(_ytDlpStatus);
        content.Children.Add(_ffmpegStatus);
        content.Children.Add(_ffprobeStatus);
        content.Children.Add(_denoStatus);
        content.Children.Add(MutedText("Встроенный браузер работает в InPrivate. Cookies передаются только при вашем выборе, через временный файл. Секреты скрываются в журналах."));
        var install = PrimaryButton("Установить или обновить");
        install.Click += InstallComponents_Click;
        var refresh = SecondaryButton("Обновить статус");
        refresh.Click += (_, _) => RefreshComponentStatus();
        content.Children.Add(Horizontal(install, refresh));
        body.Children.Add(Card(content));
        body.Children.Add(BuildNetworkCard());
        body.Children.Add(BuildDiagnosticsCard());
        body.Children.Add(BuildTranscriptionSettingsCard());
        return new ScrollViewer { Content = body };
    }

    private void ShowPage(string? tag)
    {
        _downloadPage.Visibility = tag is null or "download" ? Visibility.Visible : Visibility.Collapsed;
        _editorPage.Visibility = tag == "editor" ? Visibility.Visible : Visibility.Collapsed;
        _settingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        _infoPage.Visibility = tag == "info" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePicker(picker);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null) _outputFolderBox.Text = folder.Path;
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
        if (_joinFiles.Count < 2 || string.IsNullOrWhiteSpace(_joinOutputBox.Text))
        {
            ShowEditorMessage("Выберите минимум два видео и путь сохранения.", InfoBarSeverity.Error);
            return;
        }
        await RunEditAsync(new VideoEditRequest(VideoEditMode.Join, _joinFiles, _joinOutputBox.Text));
    }

    private async Task RunEditAsync(VideoEditRequest request)
    {
        if (_operations.IsBusy || _isInstallingComponents)
        {
            ShowEditorMessage("Другая операция уже выполняется. Сначала завершите или отмените её.", InfoBarSeverity.Error);
            return;
        }
        if (!_operations.TryBegin()) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime.Token);
        var outcome = OperationOutcome.Failed;
        _operation = operation;
        SetOperationControls(true);
        ShowEditorMessage("FFmpeg обрабатывает видео…", InfoBarSeverity.Informational);
        try
        {
            await _editor.EditAsync(request, operation.Token);
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

        var stagingDirectory = Path.Combine(Path.GetTempPath(),
            "VideoGrabber-component-stage-" + Guid.NewGuid().ToString("N"));
        _isInstallingComponents = true;
        _ytDlpStatus.Text = "Подготавливаю компоненты…";
        _ffmpegStatus.Text = "Загрузка и проверка во временный staging";
        _ffprobeStatus.Text = "Live-набор не изменяется до безопасной активации";
        _denoStatus.Text = "Операцию можно отменить";
        AppDiagnostics.Write("Component preparation started");

        try
        {
            var installer = new ComponentInstaller(new ProcessRunner(), new DeferredComponentInstallTransaction());
            var result = await installer.InstallAsync(script, stagingDirectory, cancellationToken);
            if (!result.IsSuccess)
            {
                var details = LastNonEmptyLine(result.StandardError) ?? LastNonEmptyLine(result.StandardOutput)
                    ?? "Неизвестная ошибка подготовки компонентов.";
                _ytDlpStatus.Text = $"Ошибка подготовки: {details}";
                AppDiagnostics.Write("Component preparation failed");
                return false;
            }

            RefreshComponentStatus();
            return RequiredComponentsAvailable();
        }
        catch (OperationCanceledException)
        {
            AppDiagnostics.Write("Component preparation cancelled after recovery");
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
            TryDeleteOwnedComponentStage(stagingDirectory);
        }
    }

    private static void TryDeleteOwnedComponentStage(string destination)
    {
        try
        {
            var full = Path.GetFullPath(destination);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                && leaf.StartsWith("VideoGrabber-component-stage-", StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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

        var details = string.Join(
            "  •  ",
            new[] { value.Speed, value.Eta }.Where(item => !string.IsNullOrWhiteSpace(item)));
        SetDownloadState(value.Status, details);
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

    private static Button NavigationButton(string text) => new()
    {
        Content = text,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Padding = new Thickness(14, 11, 14, 11),
        CornerRadius = new CornerRadius(8),
        Background = new SolidColorBrush(Colors.Transparent)
    };

    private static Border BuildAuthorCard()
    {
        var content = Vertical(3);
        content.Children.Add(new TextBlock
        {
            Text = "Создано Валерием",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        var telegramLink = new HyperlinkButton
        {
            Content = "Telegram: @Velkoshkin",
            NavigateUri = new Uri("https://t.me/Velkoshkin"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(0, 4, 0, 4),
            MinHeight = 32,
            FontWeight = FontWeights.SemiBold,
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
