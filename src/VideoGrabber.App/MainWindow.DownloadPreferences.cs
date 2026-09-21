using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private bool _downloadPreferenceApplying;
    private bool _completionActionScheduled;

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(
        bool hibernate,
        bool forceCritical,
        bool disableWakeEvent);

    private void InitializeDownloadPreferences()
    {
        if (_outputFolderBox is null || _completionActionBox is null)
            return;

        _downloadPreferenceApplying = true;
        try
        {
            var folder = ResolveInitialDownloadFolder();
            _outputFolderBox.Text = folder;
            _preferences.DownloadFolder = folder;

            var requested = NormalizeCompletionAction(
                _preferences.CompletionAction);
            var items = _completionActionBox.Items
                .OfType<ComboBoxItem>()
                .ToArray();
            var index = Array.FindIndex(
                items,
                item => string.Equals(
                    item.Tag?.ToString(),
                    requested,
                    StringComparison.OrdinalIgnoreCase));
            _completionActionBox.SelectedIndex =
                index >= 0 ? index : 0;
            _preferences.CompletionAction =
                (_completionActionBox.SelectedItem as ComboBoxItem)
                    ?.Tag?.ToString()
                ?? "none";
        }
        finally
        {
            _downloadPreferenceApplying = false;
        }

        PersistUiPreferences();
    }

    private string ResolveInitialDownloadFolder()
    {
        var saved = _preferences.DownloadFolder?.Trim().Trim('"');
        if (TryEnsureDownloadFolder(saved, out var resolved))
            return resolved;

        var preferred = Directory.Exists(@"D:\")
            ? @"D:\VideoGrabber"
            : @"C:\VideoGrabber";

        if (TryEnsureDownloadFolder(preferred, out resolved))
            return resolved;

        var fallback = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyVideos),
            "VideoGrabber");
        Directory.CreateDirectory(fallback);
        return Path.GetFullPath(fallback);
    }

    private static bool TryEnsureDownloadFolder(
        string? path,
        out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var full = Path.GetFullPath(path.Trim().Trim('"'));
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root)
                || !Directory.Exists(root))
                return false;

            Directory.CreateDirectory(full);
            resolved = full;
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return false;
        }
    }

    private void SaveSelectedDownloadFolder(string path)
    {
        if (!TryEnsureDownloadFolder(path, out var resolved))
        {
            SetDownloadState(
                "Не удалось использовать выбранную папку.",
                path,
                true);
            return;
        }

        _outputFolderBox.Text = resolved;
        _preferences.DownloadFolder = resolved;
        PersistUiPreferences();
        SetDownloadState(
            "Папка загрузок сохранена.",
            resolved);
    }

    private void CompletionActionSelectionChanged()
    {
        if (_downloadPreferenceApplying
            || _completionActionBox?.SelectedItem
                is not ComboBoxItem item)
            return;

        _preferences.CompletionAction =
            NormalizeCompletionAction(
                item.Tag?.ToString());
        PersistUiPreferences();
    }

    private static string NormalizeCompletionAction(
        string? action)
        => action?.Trim().ToLowerInvariant() switch
        {
            "shutdown" => "shutdown",
            "restart" => "restart",
            "sleep" => "sleep",
            _ => "none"
        };

    private void PersistUiPreferences()
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(PreferencesPath)!);
            File.WriteAllText(
                PreferencesPath,
                JsonSerializer.Serialize(
                    _preferences,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or JsonException)
        {
            DiagnosticHub.Log.Write(
                "preferences",
                "failed",
                "Cannot save preferences: "
                + ex.GetType().Name);
        }
    }

    private void ScheduleCompletionActionAfterDownloads(
        string source)
    {
        var action = NormalizeCompletionAction(
            _preferences.CompletionAction);
        if (action == "none"
            || _completionActionScheduled)
            return;

        _completionActionScheduled = true;
        DiagnosticHub.Log.Write(
            "completion.action",
            "scheduled",
            $"source={source} action={action}");

        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                // Give all operation/final-state cleanup a moment to finish.
                for (var attempt = 0;
                     attempt < 40;
                     attempt++)
                {
                    if (!_operations.IsBusy
                        && !_courseDownloadActive
                        && !_queueRunnerActive)
                        break;
                    await Task.Delay(250);
                }

                if (_windowLifetime.IsCancellationRequested
                    || _operations.IsBusy
                    || _courseDownloadActive
                    || _queueRunnerActive
                    || _browserDownloadQueue.Items.Count > 0)
                {
                    DiagnosticHub.Log.Write(
                        "completion.action",
                        "observed",
                        "Deferred action cancelled because work is still pending.");
                    return;
                }

                ExecuteCompletionAction(action);
            }
            finally
            {
                _completionActionScheduled = false;
            }
        });
    }

    private void ExecuteCompletionAction(string action)
    {
        var normalized =
            NormalizeCompletionAction(action);
        if (normalized == "none")
            return;

        var label = normalized switch
        {
            "shutdown" => "выключение компьютера",
            "restart" => "перезагрузка компьютера",
            "sleep" => "переход в спящий режим",
            _ => "завершение"
        };

        SetDownloadState(
            "Все загрузки завершены на 100%.",
            "Выполняю: " + label + ".");

        try
        {
            switch (normalized)
            {
                case "shutdown":
                    StartWindowsCommand(
                        "shutdown.exe",
                        "/s",
                        "/t",
                        "5",
                        "/c",
                        "VideoGrabber: все загрузки завершены.");
                    break;

                case "restart":
                    StartWindowsCommand(
                        "shutdown.exe",
                        "/r",
                        "/t",
                        "5",
                        "/c",
                        "VideoGrabber: все загрузки завершены.");
                    break;

                case "sleep":
                    if (!SetSuspendState(
                            hibernate: false,
                            forceCritical: false,
                            disableWakeEvent: false))
                        throw new System.ComponentModel.Win32Exception(
                            Marshal.GetLastWin32Error());
                    break;
            }

            DiagnosticHub.Log.Write(
                "completion.action",
                "succeeded",
                "action=" + normalized);
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write(
                "completion.action",
                "failed",
                ex.GetType().Name);
            SetDownloadState(
                "Загрузки завершены, но системное действие не выполнено.",
                SensitiveDataRedactor.Redact(ex.Message),
                true);
        }
    }

    private static void StartWindowsCommand(
        string fileName,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        Process.Start(startInfo);
    }
}
