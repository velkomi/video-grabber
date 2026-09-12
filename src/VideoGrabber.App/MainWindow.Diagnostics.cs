using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private TextBlock _logStatus = null!;
    private TextBlock _logWarning = null!;
    private DispatcherTimer? _logTimer;
    private bool _reportRunning;

    private Border BuildDiagnosticsCard()
    {
        var panel = Vertical(12);
        panel.Children.Add(SectionHeading("Журналы и диагностика"));
        var mode = new ComboBox { Header = "Режим журналирования", HorizontalAlignment = HorizontalAlignment.Stretch };
        mode.Items.Add(ComboItem("Полный — этапы, результаты и ошибки", "Full"));
        mode.Items.Add(ComboItem("Расширенный — отладочный вывод компонентов", "Debug"));
        mode.SelectedIndex = _preferences.LogMode == LogMode.Debug ? 1 : 0;
        DiagnosticHub.Log.Mode = _preferences.LogMode;
        mode.SelectionChanged += (_, _) => DiagnosticHub.Log.Mode = mode.SelectedIndex == 1 ? LogMode.Debug : LogMode.Full;
        panel.Children.Add(mode);
        panel.Children.Add(MutedText("Новые JSONL-журналы: не более 30 дней, до 2 МБ на файл, до 32 МБ всего. Cookies, пароли и параметры закрытых ссылок скрываются."));
        var open = SecondaryButton("Открыть журналы");
        open.Click += (_, _) => OpenDiagnosticFolder(DiagnosticHub.Log.DirectoryPath);
        var period = new ComboBox { Header = "Период отчёта", SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        period.Items.Add(ComboItem("Последние сутки", "daily"));
        period.Items.Add(ComboItem("Последние 7 дней", "weekly"));
        period.Items.Add(ComboItem("Последние 30 дней", "monthly"));
        var report = PrimaryButton("Сформировать отчёт");
        report.Click += async (_, _) => await RunDiagnosticScriptAsync("Analyze-Logs.ps1", (period.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "daily");
        var schedule = SecondaryButton("Включить проверки по расписанию");
        schedule.Click += async (_, _) => await RunDiagnosticScriptAsync("Install-DiagnosticsTasks.ps1", null);
        _logStatus = MutedText(DiagnosticHub.Log.DirectoryPath);
        _logWarning = MutedText("");
        panel.Children.Add(Horizontal(open, report));
        panel.Children.Add(period);
        panel.Children.Add(schedule);
        panel.Children.Add(MutedText("Расписание включается только этой кнопкой. Отчёт локальный; автоматического изменения кода и отправки в Telegram без настройки нет."));
        panel.Children.Add(_logStatus);
        panel.Children.Add(_logWarning);
        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _logTimer.Tick += (_, _) => _logWarning.Text = DiagnosticHub.Log.LastError ?? "";
        _logTimer.Start();
        return Card(panel);
    }

    private async Task RunDiagnosticScriptAsync(string name, string? period)
    {
        if (_reportRunning) return;
        _reportRunning = true;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "scripts", name);
            if (!File.Exists(path)) { _logStatus.Text = "Скрипт диагностики отсутствует в сборке."; return; }
            var arguments = new List<string> { "-NoProfile", "-File", path };
            if (period is not null) arguments.AddRange(["-Period", period]);
            _logStatus.Text = "Выполняется локальная диагностика…";
            var result = await new ProcessRunner().RunAsync(new ProcessSpec(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
                arguments, Timeout: TimeSpan.FromMinutes(5)), null, CancellationToken.None);
            _logStatus.Text = result.IsSuccess ? "Выполнено. " + result.StandardOutput.Trim()
                : "Ошибка диагностики: " + SensitiveDataRedactor.Redact(result.StandardError);
        }
        catch (Exception ex) { _logStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
        finally { _reportRunning = false; }
    }

    private void OpenDiagnosticFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            info.ArgumentList.Add(path);
            Process.Start(info);
        }
        catch (Exception ex) { _logStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
    }
}
